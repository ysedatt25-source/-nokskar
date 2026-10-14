(() => {
  'use strict';
  if(window.InokskarLoading)return;
  let appPending=true,appReady=false,domReady=false,initial=true,requests=0,generation=0,frame=0;
  let progress=.12,totalRequests=0,completedRequests=0;
  const setProgress=value=>{progress=Math.max(progress,Math.min(1,value));document.documentElement.style.setProperty('--page-load-progress',String(progress));if(progress===1)document.documentElement.setAttribute('data-page-loading-complete','');};
  setProgress(progress);
  const loader=()=>document.getElementById('inokskar-page-loader');
  const show=()=>{const node=loader();if(node){node.hidden=false;node.setAttribute('aria-busy','true');}};
  const hide=()=>{const node=loader();if(node){node.hidden=true;node.setAttribute('aria-busy','false');}initial=false;};
  const finish=()=>{if(domReady&&!appPending&&(!initial||requests===0))setProgress(1);const current=generation;cancelAnimationFrame(frame);frame=requestAnimationFrame(()=>{frame=requestAnimationFrame(()=>{if(current===generation&&domReady&&!appPending&&(!initial||requests===0))hide();});});};
  const begin=()=>{if(loader()?.hidden){progress=.12;document.documentElement.removeAttribute('data-page-loading-complete');document.documentElement.style.setProperty('--page-load-progress',String(progress));}generation++;show();};
  const ready=()=>{appReady=true;appPending=false;setProgress(.9);finish();};
  window.InokskarLoading=Object.freeze({begin,ready});
  // Track only initial server page data. Heartbeats and later actions are not navigation.
  const pendingRequests=new Set();
  window.addEventListener('inokskar:http-start',e=>{const {id,method,url}=e.detail;if(initial&&method==='GET'&&new URL(url).pathname.startsWith('/api/')&&!/^\/api\/(live-support|security)(\/|$)/.test(new URL(url).pathname)){if(!pendingRequests.has(id)){pendingRequests.add(id);requests++;totalRequests++;}}});
  window.addEventListener('inokskar:http-end',e=>{if(pendingRequests.delete(e.detail.id)){requests--;completedRequests++;setProgress(.45+.4*completedRequests/Math.max(1,totalRequests));finish();}});
  const spaComplete=()=>{begin();requestAnimationFrame(()=>requestAnimationFrame(()=>{if(!document.getElementById('root')||!document.querySelector('#root .brand-loading'))ready();}));};
  window.addEventListener('inokskar:navigation',spaComplete);
  window.addEventListener('popstate',spaComplete);
  window.addEventListener('pageshow',e=>{if(e.persisted){appPending=false;hide();}});
  document.addEventListener('click',e=>{if(e.defaultPrevented||e.button!==0||e.ctrlKey||e.metaKey||e.altKey||e.shiftKey)return;const a=e.target.closest?.('a[href]');if(!a||a.target||a.hasAttribute('download'))return;let url;try{url=new URL(a.href,location.href);}catch{return;}if(url.origin!==location.origin||url.pathname===location.pathname&&url.search===location.search)return;begin();setTimeout(()=>{if(e.defaultPrevented&&location.pathname!==url.pathname)finish();},0);},true);
  document.addEventListener('submit',e=>{if(e.defaultPrevented)return;const form=e.target;if(form instanceof HTMLFormElement){const url=new URL(form.action,location.href);if(url.origin===location.origin)begin();}});
  document.addEventListener('DOMContentLoaded',()=>{domReady=true;setProgress(.45);appPending=!!(document.getElementById('root')||document.querySelector('[data-live-support-desk]'))&&!appReady;if(!appPending)finish();},{once:true});
  // A stalled script must not permanently cover an error or recovery screen.
  window.addEventListener('error',e=>{if(e.target instanceof HTMLScriptElement){appPending=false;hide();}},true);
  setTimeout(()=>{if(loader()&&!loader().hidden){appPending=false;hide();}},15000);
})();
