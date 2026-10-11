/* One shared, complete mirror pass over the actual logo image on each route. */
(()=>{
  if(window.__inokskarLogoNavigation)return;window.__inokskarLogoNavigation=true;
  const start=()=>{
    const selector='img[src*="inokskar-header-brand"],.site-header .brand img,.private-brand-link img,.admin-brand img,.admin-sidebar>a.brand img,.r126-auth .brand img,.r126-auth .mobile-brand img,.r126-auth .logo img,.shell>a img,.account-brand img';
    const hosts=new Map(),pending=new WeakSet();let scheduled=false;
    const route=()=>location.pathname+location.search;
    const layout=entry=>{
      const {host,img,layer,header}=entry;if(!host.isConnected||!img.isConnected)return;
      const h=host.getBoundingClientRect(),i=img.getBoundingClientRect();if(!h.width||!h.height||!i.width||!i.height)return;
      const sx=h.width/(host.offsetWidth||h.width),sy=h.height/(host.offsetHeight||h.height);
      Object.assign(layer.style,{left:((i.left-h.left)/sx-host.clientLeft)+'px',top:((i.top-h.top)/sy-host.clientTop)+'px',width:(i.width/sx)+'px',height:(i.height/sy)+'px'});
      if(header){const b=header.getBoundingClientRect(),bx=b.width/(header.offsetWidth||b.width),by=b.height/(header.offsetHeight||b.height);header.style.setProperty('--brand-center-x',((i.left+i.width/2-b.left)/bx)+'px');header.style.setProperty('--brand-center-y',((i.top+i.height/2-b.top)/by)+'px');header.style.setProperty('--brand-fade-width',Math.max(260,i.width/bx*2)+'px');header.style.setProperty('--brand-fade-height',Math.max(52,i.height/by*1.25)+'px');}
    };
    const sweep=entry=>{if(entry.lastRoute===route())return;entry.lastRoute=route();entry.layer.classList.remove('brand-mirror-active');void entry.layer.offsetWidth;entry.layer.classList.add('brand-mirror-active');};
    const resize=new ResizeObserver(()=>{for(const entry of hosts.values())layout(entry);});
    const mount=()=>{
      scheduled=false;
      for(const img of document.querySelectorAll(selector)){
        if(!img.complete||!img.naturalWidth){if(!pending.has(img)){pending.add(img);img.addEventListener('load',schedule,{once:true});}continue;}
        const host=img.closest('a')||img.parentElement;if(!host)continue;
        const header=img.closest('.site-header,.private-brand-header,header.top,.admin-sidebar');
        if(header){header.classList.add('brand-unified-header');header.querySelectorAll(':scope>.logo-mirror-layer').forEach(x=>x.remove());header.classList.remove('logo-mirror-host','logo-mirror-active');}
        let entry=hosts.get(host);
        if(!entry||entry.img!==img||!entry.layer.isConnected){if(entry){resize.unobserve(entry.img);entry.layer.remove();}host.classList.add('brand-mirror-host');const layer=document.createElement('span');layer.className='brand-mirror-layer';layer.setAttribute('aria-hidden','true');host.append(layer);entry={host,img,header,layer,lastRoute:null};hosts.set(host,entry);resize.observe(img);resize.observe(host);}
        layout(entry);sweep(entry);
      }
      for(const [host,entry]of hosts)if(!host.isConnected){resize.unobserve(entry.img);resize.unobserve(host);hosts.delete(host);}
    };
    function schedule(){if(!scheduled){scheduled=true;requestAnimationFrame(mount);}}
    for(const method of ['pushState','replaceState']){const original=history[method];history[method]=function(...args){const value=original.apply(this,args);schedule();return value;};}
    new MutationObserver(schedule).observe(document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['src']});
    window.addEventListener('popstate',schedule);window.addEventListener('inokskar:navigation',schedule);window.addEventListener('resize',schedule);window.addEventListener('pageshow',event=>{if(event.persisted){for(const entry of hosts.values())entry.lastRoute=null;schedule();}});mount();
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
