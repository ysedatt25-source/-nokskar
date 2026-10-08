(function(){
  'use strict';
  const text=n=>(n&&n.textContent||'').trim();
  function upgradeFooter(){document.querySelectorAll('footer.site-footer,.consult-strip').forEach(n=>n.remove());}
  function upgradeAdmin(){
    if(location.pathname!=='/admin')return;
    const nav=document.querySelector('.admin-sidebar nav');
    if(nav&&!nav.querySelector('a[href="/admin/system"]')){
      const a=document.createElement('a');a.href='/admin/system';a.className='r10-system-link';a.textContent='⚙ Sistem Durumu';nav.append(a);
    }
    document.querySelectorAll('.bulk-controls p').forEach(p=>{if(text(p).includes('binlik adımlarla'))p.textContent='Euro baz fiyatını değiştirin. TL fiyatı güncel kura göre hesaplanır; yayınlanan TL tutarı ancak fark 500 TL veya üzerindeyse otomatik değişir.';});
  }
  function run(){upgradeFooter();upgradeAdmin();}
  let queued=false;const schedule=()=>{if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;run();});};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',run);else run();
  new MutationObserver(schedule).observe(document.documentElement,{childList:true,subtree:true});
})();
