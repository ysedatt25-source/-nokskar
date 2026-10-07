(()=>{
  const protectedArea=location.pathname.startsWith('/admin')||location.pathname.startsWith('/teknik')||location.pathname.startsWith('/account')||location.pathname.startsWith('/hesabim');
  if(!protectedArea)return;
  const nativeFetch=window.fetch.bind(window);
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
    const method=methodOf(input,init);
    if(!unsafe.has(method)||!sameOrigin(input))return nativeFetch(input,init);
    const csrf=await ensureToken();
    const headers=new Headers(input instanceof Request?input.headers:undefined);
    new Headers(init.headers||{}).forEach((value,key)=>headers.set(key,value));
    headers.set('X-INOKSKAR-CSRF',csrf);
    return nativeFetch(input,{...init,headers,credentials:init.credentials||'same-origin'});
  };

  document.addEventListener('submit',event=>{
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
      HTMLFormElement.prototype.submit.call(form);
    }).catch(error=>{
      const region=document.querySelector('[aria-live="polite"],#access-notice,#mail-notice');
      if(region)region.textContent=error.message;
    });
  },true);

  window.__inokskarSecurity={refresh:()=>{token='';return ensureToken();}};
})();
