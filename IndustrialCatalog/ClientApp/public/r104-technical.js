(function(){'use strict';
const host=document.getElementById('tech-list'),search=document.getElementById('tech-search'),count=document.getElementById('tech-count');
let rows=[],me={};
async function request(url,options={},timeout=30000){const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),timeout);try{return await fetch(url,{...options,signal:controller.signal});}catch(e){if(e?.name==='AbortError')throw new Error('İşlem zaman aşımına uğradı.');throw e;}finally{clearTimeout(timer);}}
const esc=s=>String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));
const canService=()=>me?.superAdmin||(me?.permissions||[]).includes('technical.edit')||(me?.permissions||[]).includes('service.edit');
function render(){
 const q=search.value.trim().toLocaleLowerCase('tr');
 const list=rows.filter(r=>!q||[r.businessName,r.productName,r.productCode,r.serialNumber].some(v=>String(v||'').toLocaleLowerCase('tr').includes(q)));
 count.textContent=list.length+' cihaz';
 host.innerHTML=list.length?list.map(r=>`<article class="device-row"><div><h3>${esc(r.productName)}</h3><p>${esc(r.businessName)}</p></div><div><strong>${esc(r.productCode)}</strong><small>Seri: ${esc(r.serialNumber)}</small></div><div><span class="status ${r.status==='active'?'active':'expired'}">${r.status==='active'?(r.daysRemaining+' gün garanti'):'Garanti bitti'}</span></div><div class="device-row-actions"><a class="button" href="/teknik/cihaz/${encodeURIComponent(r.id)}">Teknik dosya</a>${canService()?`<a class="small-btn" href="/teknik/cihaz/${encodeURIComponent(r.id)}?newService=1">+ Servis ekle</a>`:''}</div></article>`).join(''):'<div class="tech-empty"><strong>Eşleşen cihaz bulunamadı.</strong><span>İşletme, ürün kodu veya seri numarasıyla aramayı deneyin.</span></div>';
}
async function load(){
 host.innerHTML='<div class="tech-empty"><strong>Cihazlar yükleniyor…</strong></div>';
 try{
  const [r,mr]=await Promise.all([request('/api/technical/warranties',{cache:'no-store'}),request('/api/me',{cache:'no-store'})]);
  if(r.status===401){location.href='/login';return;}
  if(r.status===403){host.innerHTML='<div class="tech-empty"><strong>Yetkiniz yok.</strong><span>Teknik cihaz dosyalarını görüntüleme yetkisi gereklidir.</span></div>';return;}
  const b=await r.json().catch(()=>({}));me=await mr.json().catch(()=>({}));
  if(!r.ok)throw new Error(b.error||'Veriler alınamadı.');
  rows=b.records||[];render();
  const admin=document.getElementById('admin-link');if(admin&&!['Admin','SuperAdmin'].includes(me.role))admin.hidden=true;
 }catch(e){host.innerHTML='<div class="tech-empty"><strong>Teknik cihaz listesi alınamadı.</strong><span>'+esc(e.message||'Bağlantıyı kontrol edip tekrar deneyin.')+'</span></div>';}
}
search.addEventListener('input',render);load();})();