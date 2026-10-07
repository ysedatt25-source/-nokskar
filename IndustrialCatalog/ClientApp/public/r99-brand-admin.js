(function(){
  'use strict';

  const header=document.querySelector('.brand-admin-top.private-brand-header');
  const main=document.querySelector('.brand-admin-main');
  const staleSelector='.ui-interaction-shield,.admin-drawer-backdrop';

  function unlockStaleInteraction(){
    if(header&&header.classList.contains('menu-open'))return;
    document.querySelectorAll(staleSelector).forEach(x=>x.remove());
    document.body.classList.remove('ui-layer-locked','admin-drawer-open','storefront-layer-open');
    if(main)main.removeAttribute('inert');
    document.querySelectorAll('.brand-admin-main[inert]').forEach(x=>x.removeAttribute('inert'));
  }

  function installReliableMenu(){
    if(!header)return;
    const nav=header.querySelector(':scope>nav');
    if(!nav)return;

    // Replace any previously generated toggle so stale/duplicate event listeners cannot cancel each other.
    const old=header.querySelector(':scope>.private-header-toggle');
    const toggle=document.createElement('button');
    toggle.type='button';
    toggle.className='private-header-toggle';
    toggle.setAttribute('aria-label','Menüyü aç');
    toggle.setAttribute('aria-expanded','false');
    toggle.innerHTML='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 6h16M4 12h16M4 18h16"/></svg>';
    if(old)old.replaceWith(toggle);else header.insertBefore(toggle,nav);

    const close=()=>{
      header.classList.remove('menu-open');
      toggle.setAttribute('aria-expanded','false');
      toggle.setAttribute('aria-label','Menüyü aç');
      requestAnimationFrame(unlockStaleInteraction);
    };

    toggle.addEventListener('click',event=>{
      event.preventDefault();
      event.stopPropagation();
      unlockStaleInteraction();
      const open=header.classList.toggle('menu-open');
      toggle.setAttribute('aria-expanded',String(open));
      toggle.setAttribute('aria-label',open?'Menüyü kapat':'Menüyü aç');
    });

    nav.addEventListener('click',event=>{
      if(event.target.closest('a,button'))close();
    });

    document.addEventListener('keydown',event=>{if(event.key==='Escape')close();});
    window.addEventListener('resize',()=>{if(innerWidth>1100)close();},{passive:true});
    window.addEventListener('pageshow',()=>{close();unlockStaleInteraction();});
  }

  unlockStaleInteraction();
  installReliableMenu();
  document.addEventListener('pointerdown',unlockStaleInteraction,true);
  requestAnimationFrame(unlockStaleInteraction);

  const preview=document.getElementById('brand-preview');
  const file=document.getElementById('brand-file');
  const notice=document.getElementById('brand-admin-notice');
  const def=document.getElementById('brand-default');
  if(!preview||!file)return;

  let catalog=null,revision=0,ready=false;
  function msg(text,type){
    if(!notice)return;
    notice.className=type?'brand-admin-notice '+type:'brand-admin-notice';
    notice.textContent=text||'';
  }
  function setBusy(busy){
    file.disabled=busy;
    if(def)def.disabled=busy;
    document.body.classList.toggle('brand-admin-busy',busy);
  }
  async function load(){
    setBusy(true);
    try{
      const r=await fetch('/api/catalog?admin=1',{cache:'no-store'});
      if(r.status===401){location.href='/login';return;}
      const b=await r.json();
      if(!r.ok)throw new Error(b.error||'Ayarlar alınamadı.');
      catalog=b.data;revision=b.revision;ready=true;
      preview.src=(catalog.settings&&catalog.settings.headerImage)||'/inokskar-header-brand.png';
      msg('');
    }catch(e){
      ready=false;msg(e.message||'Ayarlar alınamadı.','error');
    }finally{
      setBusy(false);unlockStaleInteraction();
    }
  }
  async function save(url){
    if(!ready||!catalog)throw new Error('Ayarlar henüz hazır değil. Sayfayı yenileyip tekrar deneyin.');
    catalog.settings=catalog.settings||{};
    catalog.settings.headerImage=url;
    const r=await fetch('/api/catalog',{
      method:'POST',
      headers:{'Content-Type':'application/json'},
      body:JSON.stringify({data:catalog,revision,label:'Üst başlık marka görseli güncellendi'})
    });
    const b=await r.json().catch(()=>({}));
    if(r.status===401){location.href='/login';return false;}
    if(!r.ok)throw new Error(b.error||'Görsel ayarı kaydedilemedi.');
    catalog=b.data||catalog;revision=b.revision;preview.src=url;
    msg('Üst başlık görseli kaydedildi.','success');
    return true;
  }

  file.addEventListener('change',async()=>{
    const picked=file.files&&file.files[0];if(!picked)return;
    setBusy(true);msg('Görsel yükleniyor…');
    try{
      const fd=new FormData();fd.append('file',picked);
      const up=await fetch('/api/upload?kind=image',{method:'POST',body:fd});
      const ub=await up.json().catch(()=>({}));
      if(!up.ok)throw new Error(ub.error||'Görsel yüklenemedi.');
      await save(ub.url);
    }catch(e){
      msg(e.message||'Görsel yüklenemedi.','error');
    }finally{
      setBusy(false);file.value='';unlockStaleInteraction();
    }
  });

  if(def)def.addEventListener('click',async()=>{
    setBusy(true);
    try{await save('/inokskar-header-brand.png');}
    catch(e){msg(e.message||'Varsayılan görsel ayarlanamadı.','error');}
    finally{setBusy(false);unlockStaleInteraction();}
  });

  load();
})();