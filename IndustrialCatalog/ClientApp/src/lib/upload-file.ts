/** Upload deadline includes CSRF acquisition, sending and receiving the complete response. */
export async function uploadFile(file:File,kind:string,signal:AbortSignal,onProgress:(percent:number)=>void):Promise<string>{
 const auth=new AbortController();
 const cancelled=()=>auth.abort();signal.addEventListener('abort',cancelled,{once:true});
 const timer=window.setTimeout(()=>auth.abort(),15000);
 let token='';
 try{
  if(signal.aborted)throw new Error('Yükleme iptal edildi.');
  const response=await fetch('/api/security/csrf',{credentials:'same-origin',cache:'no-store',signal:auth.signal});
  if(!response.ok)throw new Error(response.status===401?'Oturumunuz sona erdi. Yeniden giriş yapın.':'Güvenlik anahtarı alınamadı. Tekrar deneyin.');
  token=String((await response.json()).token||'');if(!token)throw new Error('Güvenlik anahtarı alınamadı.');
 }catch(error){if(auth.signal.aborted)throw new Error(signal.aborted?'Yükleme iptal edildi.':'Güvenlik isteği zaman aşımına uğradı. Tekrar deneyin.');throw error;}
 finally{window.clearTimeout(timer);signal.removeEventListener('abort',cancelled);}
 return new Promise((resolve,reject)=>{
  const xhr=new XMLHttpRequest();let finished=false,idle=0;
  const finish=(error?:Error,url?:string)=>{if(finished)return;finished=true;window.clearTimeout(idle);signal.removeEventListener('abort',abort);error?reject(error):resolve(url!);};
  const abort=()=>{finish(new Error('Yükleme iptal edildi.'));xhr.abort();};
  const activity=()=>{window.clearTimeout(idle);idle=window.setTimeout(()=>{finish(new Error('Yükleme bağlantısı yanıt vermiyor. Tekrar deneyin.'));xhr.abort();},45000);};
  xhr.open('POST','/api/upload?kind='+encodeURIComponent(kind));xhr.timeout=300000;xhr.withCredentials=true;
  xhr.setRequestHeader('X-INOKSKAR-CSRF',token);
  xhr.upload.onprogress=e=>{activity();if(e.lengthComputable)onProgress(Math.min(99,Math.round(e.loaded/e.total*100)));};
  xhr.onprogress=activity;
  xhr.onload=()=>{let body:any;try{body=JSON.parse(xhr.responseText);}catch{finish(new Error('Sunucu geçerli bir yükleme yanıtı döndürmedi. Oturumunuzu ve bağlantınızı kontrol edin.'));return;}
   if(xhr.status<200||xhr.status>=300){finish(new Error(body.error||'Dosya yüklenemedi. Tekrar deneyin.'));return;}
   if(typeof body.url!=='string'||!body.url.startsWith('/api/files/')){finish(new Error('Sunucu dosya bağlantısını döndürmedi.'));return;}
   onProgress(100);finish(undefined,body.url);
  };
  xhr.onerror=()=>finish(new Error('Dosya yüklenirken bağlantı kesildi. Tekrar deneyin.'));
  xhr.ontimeout=()=>finish(new Error('Dosya yükleme zaman aşımına uğradı. Tekrar deneyin.'));
  xhr.onabort=()=>finish(new Error('Yükleme iptal edildi.'));
  signal.addEventListener('abort',abort,{once:true});if(signal.aborted){abort();return;}
  const form=new FormData();form.append('file',file);activity();try{xhr.send(form);}catch{finish(new Error('Dosya yükleme başlatılamadı. Tekrar deneyin.'));}
 });
}
