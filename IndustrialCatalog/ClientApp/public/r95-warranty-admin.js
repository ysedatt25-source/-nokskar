(function(){
  'use strict';
  const form=document.getElementById('warranty-admin-form');
  const productSelect=form&&form.elements.productId;
  const components=document.getElementById('warranty-components');
  const notice=document.getElementById('warranty-admin-notice');
  const issued=document.getElementById('issued-code');
  const datePreview=document.getElementById('warranty-date-preview');
  const legacyWarning=document.getElementById('legacy-code-warning');
  async function request(url,options={},timeout=45000){const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),timeout);try{return await fetch(url,{...options,signal:controller.signal});}catch(e){if(e?.name==='AbortError')throw new Error('İşlem zaman aşımına uğradı. Bağlantıyı kontrol edip tekrar deneyin.');throw e;}finally{clearTimeout(timer);}}
  if(!form||!productSelect||!components)return;
  let products=[],records=[],technicalPermission={view:false,edit:false,download:false},pendingFiles=[],attachedWarrantyId='';
  const privateBox=document.getElementById('warranty-private-files');
  const privateInput=document.getElementById('warranty-technical-files');
  const pendingHost=document.getElementById('warranty-pending-files');
  const existingHost=document.getElementById('warranty-existing-files');
  const privateDrop=document.getElementById('warranty-private-drop');
  const esc=s=>String(s??'').replace(/[&<>"']/g,x=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[x]));
  function refreshPending(){
    if(!pendingHost)return;
    pendingHost.replaceChildren();
    for(const file of pendingFiles){const row=document.createElement('div');row.className='warranty-pending-row';const name=document.createElement('span');name.textContent=file.name;const size=document.createElement('small');size.textContent=(file.size/1048576).toFixed(1)+' MB';row.append(name,size);
      const remove=document.createElement('button');remove.type='button';remove.className='small-btn';remove.textContent='Kaldır';remove.addEventListener('click',()=>{pendingFiles=pendingFiles.filter(f=>f!==file);refreshPending();});row.append(remove);pendingHost.append(row);
    }
  }
  function fileUrl(id,aid,kind){return '/api/technical/device/'+encodeURIComponent(id)+'/attachments/'+encodeURIComponent(aid)+'/'+kind;}
  async function refreshAttached(id){
    if(!existingHost||!technicalPermission.view)return;
    attachedWarrantyId=id||'';
    if(!id){existingHost.replaceChildren();return;}
    existingHost.textContent='Kayıtlı belgeler yükleniyor…';
    try{
      const resp=await request('/api/technical/device/'+encodeURIComponent(id),{cache:'no-store'},30000);
      const data=await resp.json().catch(()=>({}));
      if(!resp.ok)throw Error(data.error||'Teknik belgeler görüntülenemedi.');
      if(attachedWarrantyId!==id)return;
      existingHost.replaceChildren();
      const documents=data.profile?.attachments||[];
      if(!documents.length){existingHost.textContent='Bu garanti için henüz teknik belge eklenmedi.';return;}
      for(const file of documents){
        const row=document.createElement('div');row.className='warranty-private-file';
        const marker=document.createElement('span');marker.className='warranty-file-type';marker.textContent=file.mime==='application/pdf'?'PDF':'GÖRSEL';
        const label=document.createElement('span');label.className='warranty-file-name';label.textContent=file.title||file.originalName||'Teknik belge';
        const view=document.createElement('a');view.href=fileUrl(id,file.id,'view');view.textContent='Görüntüle';view.target='_blank';view.rel='noreferrer';view.className='small-btn';
        row.append(marker,label,view);
        if(technicalPermission.download){const download=document.createElement('a');download.href=fileUrl(id,file.id,'download');download.className='small-btn';download.textContent='İndir';row.append(download);}
        if(technicalPermission.edit){const remove=document.createElement('button');remove.type='button';remove.className='small-btn danger';remove.textContent='Sil';remove.addEventListener('click',async()=>{
          if(!confirm('Bu teknik belge kalıcı olarak silinsin mi?'))return;
          try{const res=await request(fileUrl(id,file.id,'').replace(/\/$/,'') ,{method:'DELETE'},45000);const body=await res.json().catch(()=>({}));if(!res.ok)throw Error(body.error||'Dosya silinemedi.');await refreshAttached(id);}catch(e){message(e.message||'Belge silinemedi.','error');}
        });row.append(remove);}
        existingHost.append(row);
      }
    }catch(e){existingHost.textContent=e.message||'Belgeler alınamadı.';}
  }
  async function uploadPending(id){
    if(!technicalPermission.edit||!pendingFiles.length)return {success:0,errors:[]};
    let success=0;const errors=[];
    for(const file of [...pendingFiles]){
      try{
        const formData=new FormData();formData.append('file',file);formData.append('title',file.name.slice(0,140));formData.append('description','Garanti kaydı sırasında eklendi.');
        const response=await request('/api/technical/device/'+encodeURIComponent(id)+'/attachments',{method:'POST',body:formData},120000);
        const data=await response.json().catch(()=>({}));
        if(!response.ok)throw Error(data.error||'Yüklenemedi');
        success++;pendingFiles=pendingFiles.filter(f=>f!==file);
      }catch(e){errors.push(file.name+': '+(e.message||'Yükleme hatası'));}
    }
    refreshPending();await refreshAttached(id);
    return {success,errors};
  }
  if(privateInput)privateInput.addEventListener('change',e=>{
    for(const file of Array.from(e.target.files||[])){
      if(!['image/jpeg','image/png','image/webp','image/gif','application/pdf'].includes(file.type)||file.size<=0||file.size>25*1024*1024){message(file.name+': PDF veya görsel dosyası seçin (en fazla 25 MB).','error');continue;}
      if(pendingFiles.length>=20){message('Bir defada en fazla 20 teknik dosya seçilebilir.','error');break;}
      pendingFiles.push(file);
    }
    privateInput.value='';refreshPending();
  });
  async function loadTechnicalPermissions(){
    try{
      const r=await request('/api/me',{cache:'no-store'},20000),me=await r.json().catch(()=>({}));
      const allow=a=>me.superAdmin===true||(Array.isArray(me.permissions)&&me.permissions.includes('technical.'+a));
      technicalPermission={view:allow('view'),edit:allow('edit'),download:allow('download')};
    }catch{technicalPermission={view:false,edit:false,download:false};}
    if(privateBox)privateBox.hidden=!technicalPermission.view;
    if(privateDrop)privateDrop.hidden=!technicalPermission.edit;
    if(privateInput)privateInput.disabled=!technicalPermission.edit;
  }
  const trDate=value=>{if(!value)return '—';const d=new Date(value+'T12:00:00');return Number.isNaN(d.getTime())?value:d.toLocaleDateString('tr-TR');};
  function message(text,type){if(!notice)return;notice.className=type?'warranty-admin-notice '+type:'warranty-admin-notice';notice.textContent=text||'';}
  function addMonthsIso(value,months){if(!/^\d{4}-\d{2}-\d{2}$/.test(value)||!(months>0))return '';const [y,m,d]=value.split('-').map(Number),index=(m-1)+months,ty=y+Math.floor(index/12),tm=((index%12)+12)%12,last=new Date(ty,tm+1,0).getDate(),day=Math.min(d,last);return String(ty).padStart(4,'0')+'-'+String(tm+1).padStart(2,'0')+'-'+String(day).padStart(2,'0');}
  function updateDatePreview(){if(!datePreview)return;const end=addMonthsIso(form.elements.deliveryDate.value,Number(form.elements.warrantyMonths.value||0));datePreview.textContent=end?'Hesaplanan garanti bitiş tarihi: '+trDate(end):'Teslim tarihi ve süre girildiğinde garanti bitiş tarihi burada hesaplanır.';}
  function showIssuedCode(code){if(!issued)return;if(!code){issued.hidden=true;issued.replaceChildren();return;}issued.hidden=false;issued.replaceChildren();const label=document.createElement('strong');label.textContent='Garanti doğrulama kodu: '+code;const note=document.createElement('span');note.textContent='Kod yönetici ekranında açık biçimde korunur ve gerektiğinde değiştirilebilir.';const copy=document.createElement('button');copy.type='button';copy.className='small-btn';copy.textContent='Kodu kopyala';copy.onclick=async()=>{try{await navigator.clipboard.writeText(code);copy.textContent='Kopyalandı';setTimeout(()=>copy.textContent='Kodu kopyala',1600);}catch{copy.textContent='Kopyalanamadı';}};issued.append(label,note,copy);}
  function addComponent(value){const v=value||{name:'',covered:true,months:Number(form.elements.warrantyMonths.value||24),note:''};const row=document.createElement('div');row.className='warranty-component-edit';row.innerHTML='<label>Bileşen adı<input class="wc-name" maxlength="100" required></label><label class="wc-covered"><span>Durum</span><select class="wc-covered-select"><option value="yes">Garanti kapsamında</option><option value="no">Garanti kapsamı dışında</option></select></label><label>Süre (Ay)<input class="wc-months" type="number" min="0" max="120" step="1"></label><label>Açıklama<input class="wc-note" maxlength="500" placeholder="Kapsam veya istisna açıklaması"></label><button type="button" class="small-btn danger wc-remove">Sil</button>';
    row.querySelector('.wc-name').value=v.name||'';row.querySelector('.wc-covered-select').value=v.covered===false?'no':'yes';row.querySelector('.wc-months').value=String(v.covered===false?0:(v.months||form.elements.warrantyMonths.value||24));row.querySelector('.wc-note').value=v.note||'';
    const sync=()=>{const covered=row.querySelector('.wc-covered-select').value==='yes';const input=row.querySelector('.wc-months'),overall=Math.max(1,Number(form.elements.warrantyMonths.value||24));input.disabled=!covered;input.max=String(overall);if(!covered)input.value='0';else if(Number(input.value)<=0||Number(input.value)>overall)input.value=String(overall);};row.querySelector('.wc-covered-select').addEventListener('change',sync);row.querySelector('.wc-remove').addEventListener('click',()=>row.remove());sync();components.append(row);
  }
  function componentValues(){return Array.from(components.querySelectorAll('.warranty-component-edit')).map(row=>({name:row.querySelector('.wc-name').value.trim(),covered:row.querySelector('.wc-covered-select').value==='yes',months:Number(row.querySelector('.wc-months').value||0),note:row.querySelector('.wc-note').value.trim()}));}
  function resetForm(){form.reset();form.elements.id.value='';form.elements.warrantyMonths.value='24';form.elements.businessName.value='';form.elements.invoiceNumber.value='';form.elements.deliveryDocumentNumber.value='';form.elements.warrantyNote.value='';components.replaceChildren();addComponent();document.getElementById('warranty-form-title').textContent='Yeni garanti kaydı';if(legacyWarning)legacyWarning.hidden=true;const tech=document.getElementById('technical-device-link');if(tech)tech.hidden=true;showIssuedCode('');message('');pendingFiles=[];refreshPending();void refreshAttached('');updateDatePreview();history.replaceState({},'', '/admin/warranties');}
  function editRecord(r){if(r.productId&&!Array.from(productSelect.options).some(o=>o.value===r.productId)){const archived=document.createElement('option');archived.value=r.productId;archived.textContent='Arşivlenmiş ürün · '+(r.productName||r.productCode||r.productId);archived.dataset.archived='1';productSelect.append(archived);}form.elements.id.value=r.id||'';form.elements.productId.value=r.productId||'';form.elements.productCode.value=r.productCode||'';form.elements.businessName.value=r.businessName||'';form.elements.invoiceNumber.value=r.invoiceNumber||'';form.elements.deliveryDocumentNumber.value=r.deliveryDocumentNumber||'';form.elements.warrantyNote.value=r.warrantyNote||'';form.elements.serialNumber.value=r.serialNumber||'';form.elements.verificationCode.value=r.verificationCode||'';form.elements.deliveryDate.value=r.deliveryDate||'';form.elements.warrantyMonths.value=String(r.warrantyMonths||24);components.replaceChildren();(r.components||[]).forEach(addComponent);if(!components.children.length)addComponent();document.getElementById('warranty-form-title').textContent='Garanti kaydını düzenle';if(legacyWarning)legacyWarning.hidden=!!r.verificationCode;const tech=document.getElementById('technical-device-link');if(tech){tech.href='/teknik/cihaz/'+encodeURIComponent(r.id);tech.hidden=false;}showIssuedCode(r.verificationCode||'');void refreshAttached(r.id);updateDatePreview();history.replaceState({},'', '/admin/warranties?edit='+encodeURIComponent(r.id));window.scrollTo({top:0,behavior:'smooth'});}
  async function load(){message('Veriler yükleniyor…');await loadTechnicalPermissions();try{const [catalogResponse,warrantyResponse]=await Promise.all([request('/api/warranty-products',{cache:'no-store'},30000),request('/api/warranties',{cache:'no-store'},30000)]);if(catalogResponse.status===401||warrantyResponse.status===401){location.href='/login';return;}const catalog=await catalogResponse.json(),warranty=await warrantyResponse.json();if(!catalogResponse.ok)throw new Error(catalog.error||'Ürünler alınamadı.');if(!warrantyResponse.ok)throw new Error(warranty.error||'Garanti kayıtları alınamadı.');products=catalog.products||[];records=warranty.records||[];productSelect.querySelectorAll('option:not(:first-child)').forEach(x=>x.remove());products.forEach(p=>{const o=document.createElement('option');o.value=p.id;o.textContent=(p.code||p.id)+' · '+p.name;productSelect.append(o);});message('');const editId=new URLSearchParams(location.search).get('edit');if(editId){const record=records.find(x=>x.id===editId);if(record)editRecord(record);else message('Düzenlenecek garanti kaydı bulunamadı.','error');}}catch(e){message(e.message||'Veriler alınamadı.','error');}}
  productSelect.addEventListener('change',()=>{if(form.elements.id.value)return;const p=products.find(x=>x.id===productSelect.value);if(p)form.elements.productCode.value=p.code||p.id||'';});
  form.elements.warrantyMonths.addEventListener('change',()=>{const months=Math.max(1,Number(form.elements.warrantyMonths.value||24));components.querySelectorAll('.warranty-component-edit').forEach(row=>{const covered=row.querySelector('.wc-covered-select').value==='yes',m=row.querySelector('.wc-months');m.max=String(months);if(covered&&(!Number(m.value)||Number(m.value)>months||Number(m.value)===24))m.value=String(months);});updateDatePreview();});
  form.elements.warrantyMonths.addEventListener('input',updateDatePreview);
  form.elements.deliveryDate.addEventListener('change',updateDatePreview);
  document.getElementById('add-component').addEventListener('click',()=>addComponent());
  document.getElementById('new-warranty').addEventListener('click',resetForm);
  document.getElementById('cancel-warranty-edit').addEventListener('click',resetForm);
  form.addEventListener('submit',async e=>{e.preventDefault();message('');const payload={id:form.elements.id.value.trim(),productId:form.elements.productId.value,productCode:form.elements.productCode.value.trim(),businessName:form.elements.businessName.value.trim(),invoiceNumber:form.elements.invoiceNumber.value.trim(),deliveryDocumentNumber:form.elements.deliveryDocumentNumber.value.trim(),warrantyNote:form.elements.warrantyNote.value.trim(),serialNumber:form.elements.serialNumber.value.trim(),verificationCode:form.elements.verificationCode.value.trim(),deliveryDate:form.elements.deliveryDate.value,warrantyMonths:Number(form.elements.warrantyMonths.value||0),components:componentValues()};if(!payload.businessName){message('Teslim edileceği işletme adını girin.','error');return;}if(!payload.components.length){message('En az bir garanti kapsamı bileşeni ekleyin.','error');return;}if(payload.id&&!payload.verificationCode){const current=records.find(x=>x.id===payload.id);if(current&&!current.verificationCode){message('Bu eski kaydın açık doğrulama kodu tutulmamış. Düzenlemeyi kaydetmek için yeni bir doğrulama kodu girin.','error');return;}}
    const submit=form.querySelector('button[type="submit"]');submit.disabled=true;submit.textContent='Kaydediliyor ve yayınlanıyor…';try{const response=await request('/api/warranties',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)},45000);const b=await response.json().catch(()=>({}));if(response.status===401){location.href='/login';return;}if(!response.ok)throw new Error(b.error||'Garanti kaydı kaydedilemedi.');const record=b.record;const index=records.findIndex(x=>x.id===record.id);if(index>=0)records[index]=record;else records.unshift(record);editRecord(record);
      const uploaded=await uploadPending(record.id);
      if(uploaded.errors.length){message('Garanti kaydı oluşturuldu ancak '+uploaded.errors.length+' belge yüklenemedi: '+uploaded.errors.join(' · '),'error');}
      else{message('Garanti kaydı kaydedildi.'+(uploaded.success?' '+uploaded.success+' teknik belge eklendi.':''),'success');}
      showIssuedCode(record.verificationCode||b.verificationCode||'');}catch(err){message(err.message||'Garanti kaydı kaydedilemedi.','error');}finally{submit.disabled=false;submit.textContent='Garanti kaydını kaydet ve yayınla';}});
  resetForm();load();
})();