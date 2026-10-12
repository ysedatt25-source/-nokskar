(()=>{
  const protectedArea=location.pathname.startsWith('/admin')||location.pathname.startsWith('/teknik')||location.pathname.startsWith('/account')||location.pathname.startsWith('/hesabim');
  if(window.__inokskarHttpInstalled)return;window.__inokskarHttpInstalled=true;
  const nativeFetch=window.fetch.bind(window);
  let navigationEpoch=0;for(const name of ['inokskar:navigation','popstate','pagehide'])window.addEventListener(name,()=>navigationEpoch++);
  const unsafe=new Set(['POST','PUT','PATCH','DELETE']);
  let token='';
  let tokenPromise=null;

  const sameOrigin=value=>{
    try{return new URL(value instanceof Request?value.url:String(value),location.href).origin===location.origin;}
    catch{return false;}
  };
  const methodOf=(input,init)=>String(init?.method||(input instanceof Request?input.method:'GET')||'GET').toUpperCase();
  const ensureToken=()=>{
    if(token)return Promise.resolve(token);
    if(tokenPromise)return tokenPromise;
    tokenPromise=nativeFetch('/api/security/csrf',{credentials:'same-origin',cache:'no-store',headers:{Accept:'application/json'}})
      .then(async r=>{
        if(r.status===401){location.href='/login';throw new Error('Oturum süresi doldu.');}
        if(!r.ok)throw new Error('Güvenlik anahtarı alınamadı.');
        const body=await r.json();
        token=String(body.token||'');
        if(!token)throw new Error('Güvenlik anahtarı boş.');
        return token;
      })
      .finally(()=>{tokenPromise=null;});
    return tokenPromise;
  };

  window.fetch=async(input,init={})=>{
    const method=methodOf(input,init);let url;try{url=new URL(input instanceof Request?input.url:String(input),location.href);}catch{return nativeFetch(input,init);}
    const detail={id:crypto.randomUUID(),method,url:url.href,epoch:navigationEpoch};const local=url.origin===location.origin;
    if(local)window.dispatchEvent(new CustomEvent('inokskar:http-start',{detail}));
    try{
      let options=init;
      if(protectedArea&&unsafe.has(method)&&sameOrigin(input)){
        const csrf=await ensureToken();const headers=new Headers(input instanceof Request?input.headers:undefined);
        new Headers(init.headers||{}).forEach((value,key)=>headers.set(key,value));headers.set('X-INOKSKAR-CSRF',csrf);options={...init,headers,credentials:init.credentials||'same-origin'};
      }
      const response=await nativeFetch(input,options);
      if(local)window.dispatchEvent(new CustomEvent('inokskar:http-result',{detail:{...detail,response,currentEpoch:navigationEpoch}}));return response;
    }catch(error){if(local)window.dispatchEvent(new CustomEvent('inokskar:http-result',{detail:{...detail,error,currentEpoch:navigationEpoch}}));throw error;}
    finally{if(local)window.dispatchEvent(new CustomEvent('inokskar:http-end',{detail}));}
  };

  document.addEventListener('submit',event=>{
    if(!protectedArea||event.defaultPrevented)return;
    const form=event.target;
    if(!(form instanceof HTMLFormElement))return;
    const method=String(form.method||'get').toUpperCase();
    if(!unsafe.has(method))return;
    const action=new URL(form.action||location.href,location.href);
    if(action.origin!==location.origin)return;
    const existing=form.querySelector('input[name="__inokskar_csrf"]');
    if(existing&&existing.value)return;
    event.preventDefault();
    ensureToken().then(csrf=>{
      let input=form.querySelector('input[name="__inokskar_csrf"]');
      if(!input){input=document.createElement('input');input.type='hidden';input.name='__inokskar_csrf';form.appendChild(input);}
      input.value=csrf;
      window.InokskarLoading?.begin();
      HTMLFormElement.prototype.submit.call(form);
    }).catch(error=>{
      const region=document.querySelector('[aria-live="polite"],#access-notice,#mail-notice');
      if(region)region.textContent=error.message;
    });
  });

  window.__inokskarSecurity={refresh:()=>{token='';return ensureToken();}};
})();
