(function(){
  'use strict';
  const form=document.getElementById('warranty-admin-form');
  const productSelect=form&&form.elements.productId;
  const manualProductName=form&&form.elements.productName;
  const productSearch=document.getElementById('warranty-product-search');
  const productCount=document.getElementById('warranty-product-count');
  const productResults=document.getElementById('warranty-product-results');
  const catalogFields=document.getElementById('warranty-catalog-fields');
  const manualFields=document.getElementById('warranty-manual-fields');
  const components=document.getElementById('warranty-components');
  const notice=document.getElementById('warranty-admin-notice');
  const issued=document.getElementById('issued-code');
  const datePreview=document.getElementById('warranty-date-preview');
  const legacyWarning=document.getElementById('legacy-code-warning');
  async function request(url,options={},timeout=45000){const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),timeout);try{return await fetch(url,{...options,signal:controller.signal});}catch(e){if(e?.name==='AbortError')throw new Error('İşlem zaman aşımına uğradı. Bağlantıyı kontrol edip tekrar deneyin.');throw e;}finally{clearTimeout(timer);}}
  if(!form||!productSelect||!components)return;
  let products=[],records=[],catalogWarning='',technicalPermission={view:false,edit:false,download:false},pendingFiles=[],attachedWarrantyId='';
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
  function loadTechnicalPermissions(){
    // The protected warranty route calculates the exact rights before rendering.
    // No delayed /api/me call can leave the uploader permanently hidden.
    technicalPermission={
      view:privateBox?.dataset.canView==='true',
      edit:privateBox?.dataset.canView==='true'&&privateBox?.dataset.canEdit==='true',
      download:privateBox?.dataset.canView==='true'&&privateBox?.dataset.canDownload==='true'
    };
    if(privateBox)privateBox.hidden=!technicalPermission.view;
    if(privateDrop)privateDrop.hidden=!technicalPermission.edit;
    if(privateInput)privateInput.disabled=!technicalPermission.edit;
  }
  const trDate=value=>{if(!value)return '—';const d=new Date(value+'T12:00:00');return Number.isNaN(d.getTime())?value:d.toLocaleDateString('tr-TR');};
  function message(text,type){
    if(notice){notice.className=type?'warranty-admin-notice '+type:'warranty-admin-notice';notice.textContent=text||'';notice.setAttribute('role',type==='error'?'alert':'status');}
    if(text&&(type==='success'||type==='error'))window.InokskarFeedback?.show({tone:type,message:text});
  }
  function addMonthsIso(value,months){if(!/^\d{4}-\d{2}-\d{2}$/.test(value)||!(months>0))return '';const [y,m,d]=value.split('-').map(Number),index=(m-1)+months,ty=y+Math.floor(index/12),tm=((index%12)+12)%12,last=new Date(ty,tm+1,0).getDate(),day=Math.min(d,last);return String(ty).padStart(4,'0')+'-'+String(tm+1).padStart(2,'0')+'-'+String(day).padStart(2,'0');}
  function updateDatePreview(){if(!datePreview)return;const end=addMonthsIso(form.elements.deliveryDate.value,Number(form.elements.warrantyMonths.value||0));datePreview.textContent=end?'Hesaplanan garanti bitiş tarihi: '+trDate(end):'Teslim tarihi ve süre girildiğinde garanti bitiş tarihi burada hesaplanır.';}
  function showIssuedCode(code){if(!issued)return;if(!code){issued.hidden=true;issued.replaceChildren();return;}issued.hidden=false;issued.replaceChildren();const label=document.createElement('strong');label.textContent='Garanti doğrulama kodu: '+code;const note=document.createElement('span');note.textContent='Kod yönetici ekranında açık biçimde korunur ve gerektiğinde değiştirilebilir.';const copy=document.createElement('button');copy.type='button';copy.className='small-btn';copy.textContent='Kodu kopyala';copy.onclick=async()=>{try{await navigator.clipboard.writeText(code);copy.textContent='Kopyalandı';setTimeout(()=>copy.textContent='Kodu kopyala',1600);}catch{copy.textContent='Kopyalanamadı';}};issued.append(label,note,copy);}
  function addComponent(value){const v=value||{name:'',covered:true,months:Number(form.elements.warrantyMonths.value||24),note:''};const row=document.createElement('div');row.className='warranty-component-edit';row.innerHTML='<label>Bileşen adı<input class="wc-name" maxlength="100" required></label><label class="wc-covered"><span>Durum</span><select class="wc-covered-select"><option value="yes">Garanti kapsamında</option><option value="no">Garanti kapsamı dışında</option></select></label><label>Süre (Ay)<input class="wc-months" type="number" min="0" max="120" step="1"></label><label>Açıklama<input class="wc-note" maxlength="500" placeholder="Kapsam veya istisna açıklaması"></label><button type="button" class="small-btn danger wc-remove">Sil</button>';
    row.querySelector('.wc-name').value=v.name||'';row.querySelector('.wc-covered-select').value=v.covered===false?'no':'yes';row.querySelector('.wc-months').value=String(v.covered===false?0:(v.months||form.elements.warrantyMonths.value||24));row.querySelector('.wc-note').value=v.note||'';
    const sync=()=>{const covered=row.querySelector('.wc-covered-select').value==='yes';const input=row.querySelector('.wc-months'),overall=Math.max(1,Number(form.elements.warrantyMonths.value||24));input.disabled=!covered;input.max=String(overall);if(!covered)input.value='0';else if(Number(input.value)<=0||Number(input.value)>overall)input.value=String(overall);};row.querySelector('.wc-covered-select').addEventListener('change',sync);row.querySelector('.wc-remove').addEventListener('click',()=>row.remove());sync();components.append(row);
  }
  function componentValues(){return Array.from(components.querySelectorAll('.warranty-component-edit')).map(row=>({name:row.querySelector('.wc-name').value.trim(),covered:row.querySelector('.wc-covered-select').value==='yes',months:Number(row.querySelector('.wc-months').value||0),note:row.querySelector('.wc-note').value.trim()}));}
  // A functional typed picker replaces the mobile-native select; the native
  // select remains as a no-JS fallback, and server-side product validation remains.
  let activeResultIndex=-1;
  let displayedResults=[];
  function hideProductResults(){
    if(productResults)productResults.hidden=true;
    if(productSearch){productSearch.setAttribute('aria-expanded','false');productSearch.removeAttribute('aria-activedescendant');}
    activeResultIndex=-1;
  }
  function chooseCatalogProduct(product){
    if(!product)return;
    renderProductOptions(product.id);
    productSearch.value=(product.name||product.id)+(product.code?' · '+product.code:'');
    form.elements.productCode.value=product.code||product.id||'';
    hideProductResults();
    productCount.textContent='Seçili ürün: '+(product.name||product.id);
  }
  function displayProductResults(){
    if(!productResults||!productSearch||chosenMode()!=='catalog')return;
    productResults.replaceChildren();
    const q=productSearch.value.trim().toLocaleLowerCase('tr-TR');
    displayedResults=products.filter(p=>((p.name||'')+' '+(p.code||'')+' '+p.id).toLocaleLowerCase('tr-TR').includes(q)).slice(0,25);
    activeResultIndex=-1;
    if(!displayedResults.length){
      const empty=document.createElement('div');empty.className='warranty-results-empty';
      empty.textContent=products.length?'Eşleşen ürün bulunamadı. Aramayı değiştirin veya elle ürün girin.':'Katalog ürünleri alınamadı veya kayıtlı ürün yok. Elle giriş kullanabilirsiniz.';
      productResults.append(empty);
    }else for(let i=0;i<displayedResults.length;i++){
      const item=displayedResults[i];
      const option=document.createElement('button');option.type='button';option.className='warranty-result-option';option.setAttribute('role','option');
      option.id='warranty-product-option-'+i;option.setAttribute('aria-selected',String(productSelect.value===item.id));
      const name=document.createElement('strong');name.textContent=item.name||item.id;option.appendChild(name);
      if(item.code){const code=document.createElement('small');code.textContent=item.code;option.appendChild(code);}
      option.addEventListener('click',()=>chooseCatalogProduct(item));
      productResults.append(option);
    }
    productResults.hidden=false;
    productSearch.setAttribute('aria-expanded','true');
  }
  function highlightProductResult(index){
    const opts=Array.from(productResults.querySelectorAll('.warranty-result-option'));
    if(!opts.length)return;
    activeResultIndex=(index+opts.length)%opts.length;
    opts.forEach((opt,i)=>opt.classList.toggle('is-active',i===activeResultIndex));
    const focused=opts[activeResultIndex];focused.scrollIntoView({block:'nearest'});
    productSearch.setAttribute('aria-activedescendant',focused.id);
  }
  if(productSearch&&productResults){
    form.classList.add('warranty-products-enhanced');
    productSelect.required=false;productSelect.tabIndex=-1;productSelect.setAttribute('aria-hidden','true');
    productSearch.addEventListener('focus',displayProductResults);
    productSearch.addEventListener('input',()=>{
      productSelect.value='';
      form.elements.productCode.value='';
      renderProductOptions('');
      displayProductResults();
    });
    productSearch.addEventListener('keydown',e=>{
      if(e.key==='ArrowDown'||e.key==='ArrowUp'){
        e.preventDefault();
        if(productResults.hidden)displayProductResults();
        highlightProductResult(activeResultIndex+(e.key==='ArrowDown'?1:-1));
      }else if(e.key==='Enter'&&!productResults.hidden&&displayedResults.length){
        e.preventDefault();chooseCatalogProduct(displayedResults[Math.max(0,activeResultIndex)]);
      }else if(e.key==='Escape')hideProductResults();
    });
    document.addEventListener('pointerdown',e=>{if(!catalogFields.contains(e.target))hideProductResults();});
  }
  function chosenMode(){return form.querySelector('input[name="productMode"]:checked')?.value==='manual'?'manual':'catalog';}
  function updateProductMode(mode,{clear=false}={}){
    const manual=mode==='manual';
    for(const radio of form.querySelectorAll('input[name="productMode"]')){radio.checked=radio.value===mode;radio.closest('.warranty-product-mode')?.classList.toggle('active',radio.checked);}
    catalogFields.hidden=manual;
    manualFields.hidden=!manual;
    hideProductResults();
    productSelect.disabled=manual;
    // This select is CSS-hidden after the accessible typeahead initializes.
    // A hidden required select blocks native submission without a visible error.
    productSelect.required=!manual&&!form.classList.contains('warranty-products-enhanced');
    manualProductName.disabled=!manual;
    manualProductName.required=manual;
    if(clear){
      form.elements.productCode.value='';
      if(manual)manualProductName.value='';
      else {productSelect.value='';if(productSearch)productSearch.value='';}
    }
  }
  function renderProductOptions(selectedId=''){
    const query=(productSearch?.value||'').trim().toLocaleLowerCase('tr');
    productSelect.replaceChildren();
    const placeholder=document.createElement('option');placeholder.value='';placeholder.textContent=products.length?'Katalogdan ürün seçin':'Katalogda kayıtlı ürün bulunamadı';productSelect.append(placeholder);
    const visible=products.filter(p=>!query||((p.name||'')+' '+(p.code||'')+' '+(p.id||'')).toLocaleLowerCase('tr').includes(query));
    for(const product of visible){
      const opt=document.createElement('option');opt.value=product.id;
      opt.textContent=(product.name||product.id)+(product.code?' · '+product.code:'');productSelect.append(opt);
    }
    if(selectedId&&!visible.some(p=>p.id===selectedId)){
      const archived=records.find(x=>x.productId===selectedId);
      const opt=document.createElement('option');opt.value=selectedId;
      opt.textContent=(archived?.productName||products.find(p=>p.id===selectedId)?.name||'Önceki ürün')+' · kayıttaki ürün';
      productSelect.append(opt);
    }
    productSelect.value=selectedId||'';
    const count=products.length;
    productCount.textContent=catalogWarning || (count?count+' kayıtlı ürün · '+visible.length+' sonuç'+(query?'':''):'Katalogda ürün bulunamadı. “Elle ürün gir” ile devam edebilirsiniz.');
  }
  form.querySelectorAll('input[name="productMode"]').forEach(radio=>radio.addEventListener('change',()=>{if(radio.checked)updateProductMode(radio.value,{clear:true});}));
  // Search and selection are handled by the same keyboard-accessible result picker.
  function resetForm(){form.reset();updateProductMode('catalog');if(productSearch)productSearch.value='';renderProductOptions();form.elements.id.value='';form.elements.warrantyMonths.value='24';form.elements.businessName.value='';form.elements.customerName.value='';form.elements.invoiceNumber.value='';form.elements.deliveryDocumentNumber.value='';form.elements.warrantyNote.value='';components.replaceChildren();addComponent();document.getElementById('warranty-form-title').textContent='Yeni garanti kaydı';if(legacyWarning)legacyWarning.hidden=true;const tech=document.getElementById('technical-device-link');if(tech)tech.hidden=true;showIssuedCode('');message('');pendingFiles=[];refreshPending();void refreshAttached('');updateDatePreview();history.replaceState({},'', '/admin/warranties');}
  function editRecord(r){const mode=r.productMode==='manual'||String(r.productId||'').startsWith('manual-')?'manual':'catalog';updateProductMode(mode);if(productSearch)productSearch.value='';if(mode==='catalog'){renderProductOptions(r.productId||'');if(productSearch)productSearch.value=(r.productName||'')+(r.productCode?' · '+r.productCode:'');}manualProductName.value=r.productName||'';form.elements.id.value=r.id||'';form.elements.productId.value=r.productId||'';form.elements.productCode.value=r.productCode||'';form.elements.businessName.value=r.businessName||'';form.elements.customerName.value=r.customerName||'';form.elements.invoiceNumber.value=r.invoiceNumber||'';form.elements.deliveryDocumentNumber.value=r.deliveryDocumentNumber||'';form.elements.warrantyNote.value=r.warrantyNote||'';form.elements.serialNumber.value=r.serialNumber||'';form.elements.verificationCode.value=r.verificationCode||'';form.elements.deliveryDate.value=r.deliveryDate||'';form.elements.warrantyMonths.value=String(r.warrantyMonths||24);components.replaceChildren();(r.components||[]).forEach(addComponent);if(!components.children.length)addComponent();document.getElementById('warranty-form-title').textContent='Garanti kaydını düzenle';if(legacyWarning)legacyWarning.hidden=!!r.verificationCode;const tech=document.getElementById('technical-device-link');if(tech){tech.href='/teknik/cihaz/'+encodeURIComponent(r.id);tech.hidden=false;}showIssuedCode(r.verificationCode||'');void refreshAttached(r.id);updateDatePreview();history.replaceState({},'', '/admin/warranties?edit='+encodeURIComponent(r.id));window.scrollTo({top:0,behavior:'smooth'});}
  async function load(){
    message('Veriler yükleniyor…');loadTechnicalPermissions();
    try{
      const [catalogResult,warrantyResult]=await Promise.allSettled([
        request('/api/warranty-products',{cache:'no-store'},30000),
        request('/api/warranties',{cache:'no-store'},30000)
      ]);
      if(warrantyResult.status==='rejected')throw warrantyResult.reason;
      const warrantyResponse=warrantyResult.value;
      if(warrantyResponse.status===401){location.href='/login';return;}
      const warranty=await warrantyResponse.json().catch(()=>({}));
      if(!warrantyResponse.ok)throw Error(warranty.error||'Garanti kayıtları alınamadı.');
      records=Array.isArray(warranty.records)?warranty.records:[];
      products=[];
      catalogWarning='';
      if(catalogResult.status==='fulfilled'){
        const response=catalogResult.value;
        if(response.status===401){location.href='/login';return;}
        const data=await response.json().catch(()=>({}));
        if(response.ok&&Array.isArray(data.products))products=data.products;
        else catalogWarning='Katalog ürünleri alınamadı. Elle ürün girebilirsiniz.';
      }else catalogWarning='Katalog bağlantısı kurulamadı. Elle ürün girebilirsiniz.';
      // Prefer the full administration catalog when the specialized endpoint
      // returns no products; the public catalog may intentionally hide records.
      if(!products.length){
        try{
          const response=await request('/api/catalog?admin=1',{cache:'no-store'},15000);
          const data=await response.json().catch(()=>({}));
          if(response.ok&&Array.isArray(data?.data?.products)){
            products=data.data.products.map(p=>({id:p.id,name:p.name,code:p.code}));
            if(products.length)catalogWarning='';
          }
        }catch{}
      }
      // Public catalog fallback is read-only and may hide unpublished products.
      if(!products.length){
        try{
          const response=await request('/api/catalog',{cache:'no-store'},15000);
          const data=await response.json().catch(()=>({}));
          if(response.ok&&Array.isArray(data?.data?.products)){
            products=data.data.products.map(p=>({id:p.id,name:p.name,code:p.code}));
            if(products.length)catalogWarning='';
          }
        }catch{}
      }
      products=[...new Map(products.filter(p=>p?.id&&p?.name).map(p=>[p.id,p])).values()]
        .sort((a,b)=>String(a.name).localeCompare(String(b.name),'tr'));
      renderProductOptions();
      if(productResults&&!productResults.hidden)displayProductResults();
      message('');
      const editId=new URLSearchParams(location.search).get('edit');
      if(editId){
        const record=records.find(x=>x.id===editId);
        if(record)editRecord(record);
        else message('Düzenlenecek garanti kaydı bulunamadı.','error');
      }
    }catch(e){catalogWarning='Katalog yüklenemedi. Elle ürün girerek devam edebilirsiniz.';renderProductOptions();message(e.message||'Garanti kayıtları alınamadı.','error');}
  }
  productSelect.addEventListener('change',()=>{const p=products.find(x=>x.id===productSelect.value);if(p)chooseCatalogProduct(p);});
  form.elements.warrantyMonths.addEventListener('change',()=>{const months=Math.max(1,Number(form.elements.warrantyMonths.value||24));components.querySelectorAll('.warranty-component-edit').forEach(row=>{const covered=row.querySelector('.wc-covered-select').value==='yes',m=row.querySelector('.wc-months');m.max=String(months);if(covered&&(!Number(m.value)||Number(m.value)>months||Number(m.value)===24))m.value=String(months);});updateDatePreview();});
  form.elements.warrantyMonths.addEventListener('input',updateDatePreview);
  form.elements.deliveryDate.addEventListener('change',updateDatePreview);
  document.getElementById('add-component').addEventListener('click',()=>addComponent());
  document.getElementById('new-warranty').addEventListener('click',resetForm);
  document.getElementById('cancel-warranty-edit').addEventListener('click',resetForm);
  function validateWarranty(payload){
    const issue=(message,element)=>({message,element});
    if(payload.productMode==='manual'&&!payload.productName)return issue('Garanti kapsamındaki ürünün adını girin.',manualProductName);
    if(payload.productMode==='catalog'&&!payload.productId)return issue('Katalogdan bir ürün seçin veya “Elle ürün gir” seçeneğini kullanın.',productSearch);
    if(!payload.businessName)return issue('Teslim edileceği işletmenin adını girin.',form.elements.businessName);
    if(payload.serialNumber.length<2)return issue('Gerçek cihaz seri numarası en az 2 karakter olmalıdır.',form.elements.serialNumber);
    if(!/^\d{4}-\d{2}-\d{2}$/.test(payload.deliveryDate)||Number.isNaN(Date.parse(payload.deliveryDate+'T12:00:00')))return issue('Geçerli bir teslim tarihi seçin.',form.elements.deliveryDate);
    if(!Number.isInteger(payload.warrantyMonths)||payload.warrantyMonths<1||payload.warrantyMonths>120)return issue('Garanti süresini 1 ile 120 ay arasında girin.',form.elements.warrantyMonths);
    if(payload.verificationCode&&!/^[A-Za-z0-9\s-]{8,32}$/.test(payload.verificationCode))return issue('Doğrulama kodu 8–32 karakter arasında, harf ve rakamlardan oluşmalıdır.',form.elements.verificationCode);
    if(!payload.components.length)return issue('En az bir garanti kapsamı bileşeni ekleyin.',document.getElementById('add-component'));
    const names=new Set();let covered=0;
    const rows=[...components.querySelectorAll('.warranty-component-edit')];
    for(let i=0;i<payload.components.length;i++){
      const item=payload.components[i],row=rows[i];
      if(!item.name)return issue((i+1)+'. bileşenin adını girin.',row?.querySelector('.wc-name'));
      if(names.has(item.name.toLocaleLowerCase('tr-TR')))return issue('Aynı garanti bileşeni iki kez eklenemez: '+item.name,row?.querySelector('.wc-name'));
      names.add(item.name.toLocaleLowerCase('tr-TR'));
      if(item.covered){covered++;if(item.months<1||item.months>payload.warrantyMonths)return issue(item.name+' için garanti süresi 1–'+payload.warrantyMonths+' ay arasında olmalıdır.',row?.querySelector('.wc-months'));}
    }
    if(!covered)return issue('En az bir bileşeni garanti kapsamına alın.',rows[0]?.querySelector('.wc-covered-select'));
    if(payload.id&&!payload.verificationCode){
      const previous=records.find(record=>record.id===payload.id);
      if(previous&&!previous.verificationCode)return issue('Bu eski kaydı güncellemek için yeni bir doğrulama kodu girin.',form.elements.verificationCode);
    }
    return null;
  }
  form.addEventListener('submit',async e=>{
    e.preventDefault();
    const submit=form.querySelector('button[type="submit"]');
    if(!submit||submit.disabled)return;
    message('');
    const mode=chosenMode();
    const payload={
      id:form.elements.id.value.trim(),productMode:mode,
      productId:mode==='manual'?'':form.elements.productId.value,
      productName:mode==='manual'?manualProductName.value.trim():'',
      productCode:form.elements.productCode.value.trim(),
      businessName:form.elements.businessName.value.trim(),
      customerName:form.elements.customerName.value.trim(),
      invoiceNumber:form.elements.invoiceNumber.value.trim(),
      deliveryDocumentNumber:form.elements.deliveryDocumentNumber.value.trim(),
      warrantyNote:form.elements.warrantyNote.value.trim(),
      serialNumber:form.elements.serialNumber.value.trim(),
      verificationCode:form.elements.verificationCode.value.trim(),
      deliveryDate:form.elements.deliveryDate.value,
      warrantyMonths:Number(form.elements.warrantyMonths.value||0),
      components:componentValues()
    };
    const problem=validateWarranty(payload);
    if(problem){
      problem.element?.setAttribute('aria-invalid','true');
      message('Garanti kaydı oluşturulamadı: '+problem.message,'error');
      return;
    }
    for(const element of form.querySelectorAll('[aria-invalid="true"]'))element.removeAttribute('aria-invalid');
    const creating=!payload.id;
    const originalLabel=submit.textContent;
    submit.disabled=true;
    submit.setAttribute('aria-busy','true');
    submit.textContent='Garanti kaydı kaydediliyor…';
    try{
      const response=await request('/api/warranties',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)},45000);
      const data=await response.json().catch(()=>({}));
      if(response.status===401){message('Oturumunuz sona erdi. Lütfen yeniden giriş yapın.','error');return;}
      if(response.status===403){message('Garanti kaydı oluşturmak için gerekli düzenleme yetkiniz bulunmuyor.','error');return;}
      if(!response.ok)throw new Error(data.error||('Kayıt gerçekleştirilemedi (HTTP '+response.status+'). Alanları kontrol edip tekrar deneyin.'));
      if(!data.record?.id)throw new Error('Sunucu geçerli kayıt bilgisi döndürmedi. Garanti listesinden sonucu kontrol edin.');
      const record=data.record;
      const index=records.findIndex(x=>x.id===record.id);
      if(index>=0)records[index]=record;else records.unshift(record);
      editRecord(record);
      let uploaded={success:0,errors:[]};
      try{uploaded=await uploadPending(record.id);}
      catch(error){uploaded.errors=['Teknik belge işlemi: '+(error?.message||'Yükleme tamamlanamadı.')];}
      const holder=(record.customerName||payload.customerName||'').trim();
      const business=(record.businessName||payload.businessName||'').trim();
      const subject=holder?holder+' adlı kişinin':business+' işletmesinin';
      const prefix=subject+' garanti kaydı başarıyla '+(creating?'oluşturulmuştur.':'güncellenmiştir.');
      const suffix=uploaded.errors.length
        ?' Ancak '+uploaded.errors.length+' belge yüklenemedi: '+uploaded.errors.join(' · ')
        :uploaded.success?' '+uploaded.success+' teknik belge eklenmiştir.':'';
      // A successful record must not be reported as a failed save if a later file upload fails.
      message(prefix+suffix,uploaded.errors.length?'error':'success');
      showIssuedCode(record.verificationCode||data.verificationCode||'');
    }catch(error){
      message('Garanti kaydı kaydedilemedi: '+(error?.message||'Beklenmeyen bir hata oluştu. Lütfen yeniden deneyin.'),'error');
    }finally{
      submit.disabled=false;
      submit.removeAttribute('aria-busy');
      submit.textContent=originalLabel;
    }
  });
  resetForm();load();
})();