(()=>{'use strict';
 const root=document.querySelector('.certificate-shell');if(!root)return;
 const status=root.querySelector('.certificate-status');
 const buttons=[...root.querySelectorAll('[data-print-panel]')];
 for(const button of buttons)button.addEventListener('click',()=>{
   const open=button.getAttribute('aria-expanded')!=='true';
   for(const item of buttons){const selected=item===button&&open;item.setAttribute('aria-expanded',String(selected));document.getElementById(item.getAttribute('aria-controls')).hidden=!selected;}
   status.textContent='';
 });
 const back=root.querySelector('[data-certificate-back]');
 back.addEventListener('click',event=>{
   if(window.opener&&!window.opener.closed){event.preventDefault();window.close();return;}
   try{const previous=new URL(document.referrer);if(previous.origin===location.origin&&/^\/admin\/warranties(?:\/list)?$/.test(previous.pathname)){event.preventDefault();history.back();}}catch{}
 });
 const vendor=window.WarrantyPrint;
 const queryUrl=new URL('/garanti-sorgulama',location.origin);
 const qrUrl=new URL(queryUrl);qrUrl.hash=new URLSearchParams({s:root.dataset.serial,c:root.dataset.code}).toString();
 const qrReady=vendor&&root.dataset.code&&root.dataset.code!=='—'?vendor.makeQR(qrUrl.href).then(qr=>{
   for(const image of root.querySelectorAll('[data-warranty-qr]'))image.src=qr.dataUrl;
   return qr;
 }):Promise.resolve(null);
 qrReady.catch(()=>{status.textContent='QR kod hazırlanamadı. Sayfayı yenileyerek tekrar deneyin.';});
 for(const item of root.querySelectorAll('[data-query-address]'))item.textContent=queryUrl.host+queryUrl.pathname;
 function fitText(container){
   for(const item of container.querySelectorAll('[data-fit-text]')){
     let size=container.classList.contains('has-technical')&&item.classList.contains('thermal-product')?9:Number(item.dataset.fitText),min=Number(item.dataset.minFont||7);
     item.style.fontSize=size+'pt';
     while((item.scrollHeight>item.clientHeight+1||item.scrollWidth>item.clientWidth+1)&&size>min){size-=.25;item.style.fontSize=size+'pt';}
   }
 }
 function fitPreview(thermal){
   if(thermal.closest('[hidden]'))return;
   const available=thermal.parentElement.clientWidth-2;
   thermal.style.zoom=String(Math.min(1,Math.max(0.1,available/thermal.offsetWidth)));
   fitText(thermal);
   const panel=thermal.closest('.certificate-panel');let warning=panel.querySelector('.thermal-label-warning');
   if(!warning){warning=document.createElement('p');warning.className='thermal-label-warning';warning.setAttribute('role','status');panel.insertBefore(warning,panel.querySelector('[data-print-kind]'));}
   const overflow=[...thermal.querySelectorAll('[data-fit-text],.thermal-technical,.thermal-technical div')].some(e=>e.scrollHeight>e.clientHeight+1||e.scrollWidth>e.clientWidth+1);
   warning.textContent=overflow?(thermal.classList.contains('thermal-label-wide')?'Bilgiler 100 × 50 mm etikete sığmıyor. Uzun model veya belge numarasını kısaltın.':'Bilgiler 80 × 50 mm etikete okunaklı sığmıyor. 100 × 50 mm seçeneğini kullanın.'):'';
 }
 for(const thermal of root.querySelectorAll('.thermal-label')){
   const previewObserver=new ResizeObserver(()=>fitPreview(thermal));
   previewObserver.observe(thermal.parentElement);
   previewObserver.observe(thermal);
 }

 let printing=false;
 for(const button of root.querySelectorAll('[data-print-kind]'))button.addEventListener('click',async()=>{
   if(printing)return;printing=true;button.disabled=true;status.textContent='';
   let frame,viewer;
   viewer=window.open('about:blank','_blank');
   if(viewer)viewer.document.body.textContent='Yazdırma PDF’i hazırlanıyor…';
   try{
     if(!vendor)throw Error('Yazdırma araçları yüklenemedi. Sayfayı yenileyin.');
     status.textContent='PDF hazırlanıyor…';
     const qr=await qrReady;
     const label=button.dataset.printKind.startsWith('thermal');
     if(label&&!qr)throw Error('Etiket için geçerli bir garanti doğrulama kodu gerekiyor.');
     const wide=button.dataset.printKind==='thermal-wide';
     const width=wide?100:80;
     const thermal=button.closest('.certificate-panel').querySelector('.thermal-label');
     const copy=(label?thermal:root.querySelector('.certificate')).cloneNode(true);
     copy.querySelectorAll('.brand-mirror-layer').forEach(e=>e.remove());
     copy.removeAttribute('id');copy.style.removeProperty('zoom');
     const css=new URL('/warranty-document.css?v=20261011-qa1',location.origin).href;
     frame=document.createElement('iframe');frame.title='Yazdırma belgesi';
     frame.style.cssText='position:fixed;left:-10000px;top:0;border:0;width:'+(label?width+'mm':'210mm')+';height:'+(label?'50mm':'297mm')+';';
     const loaded=new Promise((resolve,reject)=>{frame.onload=resolve;frame.onerror=()=>reject(Error('Yazdırma belgesi hazırlanamadı.'));});
     frame.srcdoc='<!doctype html><html lang="tr"><head><meta charset="utf-8"><title>İNOKSKAR '+(label?'Termal Etiket':'Garanti Belgesi')+'</title><link rel="stylesheet" href="'+css+'">'+(label?'<style>@page{size:'+width+'mm 50mm;margin:0}html,body{margin:0;padding:0;background:white}.thermal-label{border:0}</style>':'')+'</head><body class="warranty-certificate-body warranty-print-document">'+copy.outerHTML+'</body></html>';
     document.body.append(frame);await loaded;
     await Promise.all([...frame.contentDocument.images].map(i=>i.decode().catch(()=>{throw Error('Belge logosu yüklenemedi. Tekrar deneyin.');})));
     await frame.contentDocument.fonts.ready;
     if(label){
       const sheet=frame.contentDocument.querySelector('.thermal-label');fitText(sheet);
       if(sheet.scrollHeight>sheet.clientHeight+1||sheet.scrollWidth>sheet.clientWidth+1||[...sheet.querySelectorAll('[data-fit-text],.thermal-technical,.thermal-technical div')].some(e=>e.scrollHeight>e.clientHeight+1||e.scrollWidth>e.clientWidth+1))throw Error('Bu kayıttaki uzun bilgiler seçilen etikete sığmıyor. 100 × 50 mm seçeneğini kullanın veya uzun model / belge numarasını kısaltın.');
     }
     if(!label){
       const sheet=frame.contentDocument.querySelector('.certificate');
       const maxHeight=133/25.4*96;
       let size=10;sheet.style.setProperty('--certificate-font',size+'px');
       while(sheet.getBoundingClientRect().height>maxHeight&&size>8){size-=.25;sheet.style.setProperty('--certificate-font',size+'px');}
       if(sheet.getBoundingClientRect().height>maxHeight)throw Error('Bu kaydın kapsamı veya notları yarım A4 alanına sığmıyor. Bilgileri kısaltarak tekrar deneyin.');
     }
     const sheet=frame.contentDocument.querySelector(label?'.thermal-label':'.certificate');
     const canvas=await vendor.render(sheet,{scale:4,backgroundColor:'#fff',logging:false,useCORS:true});
     const pdf=await vendor.PDFDocument.create(),mm=72/25.4;
     const page=pdf.addPage(label?[width*mm,50*mm]:[210*mm,297*mm]);
     const image=await pdf.embedPng(canvas.toDataURL('image/png'));
     const drawWidth=(label?width:196)*mm;
     const drawHeight=label?50*mm:drawWidth*canvas.height/canvas.width;
     page.drawImage(image,{x:label?0:7*mm,y:page.getHeight()-(label?0:7*mm)-drawHeight,width:drawWidth,height:drawHeight});
     if(label){
       const box=sheet.querySelector('[data-warranty-qr]').getBoundingClientRect(),bounds=sheet.getBoundingClientRect();
       const factor=width*mm/bounds.width,side=box.width*factor;
       const x=(box.left-bounds.left)*factor,y=page.getHeight()-(box.top-bounds.top)*factor-side;
       page.drawRectangle({x,y,width:side,height:side,color:vendor.rgb(1,1,1)});
       const unit=side/(qr.size+8);
       for(let row=0;row<qr.size;row++)for(let col=0;col<qr.size;col++)if(qr.modules[row*qr.size+col])page.drawRectangle({x:x+(col+4)*unit,y:y+(qr.size-1-row+4)*unit,width:unit,height:unit,color:vendor.rgb(0,0,0)});
     }
     const preferences=pdf.catalog.getOrCreateViewerPreferences();
     preferences.setPrintScaling(vendor.PrintScaling.None);preferences.setNumCopies(1);
     preferences.setPickTrayByPDFSize(true);preferences.setDuplex(vendor.Duplex.Simplex);
     preferences.setPrintPageRange([{start:0,end:0}]);
     pdf.setTitle('İNOKSKAR '+(label?'Termal Etiket '+width+' × 50 mm':'Garanti Belgesi'));pdf.setCreator('İNOKSKAR');
     const bytes=await pdf.save(),url=URL.createObjectURL(new Blob([bytes],{type:'application/pdf'}));
     const panel=button.closest('.certificate-panel');panel.querySelector('.certificate-pdf-download')?.remove();
     const download=document.createElement('a');download.className='certificate-pdf-download';download.href=url;download.download=label?'INOKSKAR-etiket-'+width+'x50.pdf':'INOKSKAR-garanti-A4.pdf';download.textContent='PDF’i indir';panel.append(download);
     if(viewer)viewer.location.replace(url);
     status.textContent=viewer?'PDF hazır. Açılan PDF ekranından gerçek boyutta (%100) yazdırın.':'PDF hazır. PDF’i indir bağlantısından açıp yazdırın.';
     frame.remove();
   }catch(error){frame?.remove();viewer?.close();status.textContent=error.message||'Yazdırma başlatılamadı.';}
   finally{printing=false;button.disabled=false;}
 });
})();
