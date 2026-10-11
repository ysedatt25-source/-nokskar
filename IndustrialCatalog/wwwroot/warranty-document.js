(()=>{'use strict';
 const root=document.querySelector('.certificate-shell');if(!root)return;
 const status=root.querySelector('.certificate-status');
 const buttons=[...root.querySelectorAll('[data-print-panel]')];
 for(const button of buttons)button.addEventListener('click',()=>{
   const open=button.getAttribute('aria-expanded')!=='true';
   for(const item of buttons){const selected=item===button&&open;item.setAttribute('aria-expanded',String(selected));document.getElementById(item.getAttribute('aria-controls')).hidden=!selected;}
   status.textContent='';
 });
 const queryUrl=new URL('/garanti-sorgulama',location.origin);
 for(const item of root.querySelectorAll('[data-query-address]'))item.textContent=queryUrl.host+queryUrl.pathname;
 function fitText(container){
   for(const item of container.querySelectorAll('[data-fit-text]')){
     let size=Number(item.dataset.fitText),min=Number(item.dataset.minFont||7);
     item.style.fontSize=size+'pt';
     while((item.scrollHeight>item.clientHeight+1||item.scrollWidth>item.clientWidth+1)&&size>min){size-=.25;item.style.fontSize=size+'pt';}
   }
 }
 const thermal=root.querySelector('.thermal-label');
 new ResizeObserver(()=>{if(!thermal.closest('[hidden]'))fitText(thermal);}).observe(thermal);
 let printing=false;
 for(const button of root.querySelectorAll('[data-print-kind]'))button.addEventListener('click',async()=>{
   if(printing)return;printing=true;button.disabled=true;status.textContent='';
   let frame;
   try{
     const label=button.dataset.printKind==='thermal';
     const copy=(label?thermal:root.querySelector('.certificate')).cloneNode(true);
     copy.querySelectorAll('.brand-mirror-layer').forEach(e=>e.remove());
     copy.removeAttribute('id');
     const css=new URL('/warranty-document.css?v=20261011-1',location.origin).href;
     frame=document.createElement('iframe');frame.title='Yazdırma belgesi';
     frame.style.cssText='position:fixed;left:-10000px;top:0;border:0;width:'+(label?'80mm':'210mm')+';height:'+(label?'50mm':'297mm')+';';
     const loaded=new Promise((resolve,reject)=>{frame.onload=resolve;frame.onerror=()=>reject(Error('Yazdırma belgesi hazırlanamadı.'));});
     frame.srcdoc='<!doctype html><html lang="tr"><head><meta charset="utf-8"><title>İNOKSKAR '+(label?'Termal Etiket':'Garanti Belgesi')+'</title><link rel="stylesheet" href="'+css+'">'+(label?'<style>@page{size:80mm 50mm;margin:0}html,body{margin:0;padding:0;background:white}.thermal-label{border:0}</style>':'')+'</head><body class="warranty-certificate-body">'+copy.outerHTML+'</body></html>';
     document.body.append(frame);await loaded;
     await Promise.all([...frame.contentDocument.images].map(i=>i.decode().catch(()=>{throw Error('Belge logosu yüklenemedi. Tekrar deneyin.');})));
     await frame.contentDocument.fonts.ready;
     if(label){
       const sheet=frame.contentDocument.querySelector('.thermal-label');fitText(sheet);
       if(sheet.scrollHeight>sheet.clientHeight+1||sheet.scrollWidth>sheet.clientWidth+1||[...sheet.querySelectorAll('[data-fit-text]')].some(e=>e.scrollHeight>e.clientHeight+1||e.scrollWidth>e.clientWidth+1))throw Error('Bu kayıttaki uzun bilgiler 80 × 50 mm etikete sığmıyor. Ürün adı ve seri numarasını kontrol edin.');
     }
     frame.contentWindow.addEventListener('afterprint',()=>frame.remove(),{once:true});
     setTimeout(()=>frame.remove(),300000);
     frame.contentWindow.focus();frame.contentWindow.print();
   }catch(error){frame?.remove();status.textContent=error.message||'Yazdırma başlatılamadı.';}
   finally{printing=false;button.disabled=false;}
 });
})();
