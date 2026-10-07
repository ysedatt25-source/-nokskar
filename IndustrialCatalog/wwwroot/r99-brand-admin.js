(function(){
  'use strict';
  const preview=document.getElementById('brand-preview'),file=document.getElementById('brand-file'),notice=document.getElementById('brand-admin-notice'),def=document.getElementById('brand-default');
  if(!preview||!file)return;
  let catalog=null,revision=0;
  function msg(text,type){if(!notice)return;notice.className=type?'brand-admin-notice '+type:'brand-admin-notice';notice.textContent=text||'';}
  async function load(){try{const r=await fetch('/api/catalog?admin=1',{cache:'no-store'});if(r.status===401){location.href='/login';return;}const b=await r.json();if(!r.ok)throw new Error(b.error||'Ayarlar alınamadı.');catalog=b.data;revision=b.revision;preview.src=(catalog.settings&&catalog.settings.headerImage)||'/inokskar-header-brand.png';}catch(e){msg(e.message||'Ayarlar alınamadı.','error');}}
  async function save(url){catalog.settings.headerImage=url;const r=await fetch('/api/catalog',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({data:catalog,revision,label:'Üst başlık marka görseli güncellendi'})});const b=await r.json().catch(()=>({}));if(r.status===401){location.href='/login';return false;}if(!r.ok)throw new Error(b.error||'Görsel ayarı kaydedilemedi.');catalog=b.data||catalog;revision=b.revision;preview.src=url;msg('Üst başlık görseli kaydedildi.','success');return true;}
  file.addEventListener('change',async()=>{const picked=file.files&&file.files[0];if(!picked)return;file.disabled=true;msg('Görsel yükleniyor…');try{const fd=new FormData();fd.append('file',picked);const up=await fetch('/api/upload?kind=image',{method:'POST',body:fd});const ub=await up.json().catch(()=>({}));if(!up.ok)throw new Error(ub.error||'Görsel yüklenemedi.');await save(ub.url);}catch(e){msg(e.message||'Görsel yüklenemedi.','error');}finally{file.disabled=false;file.value='';}});
  def&&def.addEventListener('click',async()=>{try{await save('/inokskar-header-brand.png');}catch(e){msg(e.message||'Varsayılan görsel ayarlanamadı.','error');}});
  load();
})();