(function(){
  const handled=new WeakSet();
  const rateBoxesPending=new WeakSet();
  let adminDataPromise=null;
  let publicDataPromise=null;
  const textOf=el=>(el?.textContent||'').replace(/\s+/g,' ').trim();
  const hide=el=>{if(el)el.hidden=true;};
  const adminData=()=>adminDataPromise||(adminDataPromise=fetch('/api/catalog?admin=1',{cache:'no-store'}).then(async r=>{if(!r.ok)throw new Error('Yönetim verisi alınamadı.');return (await r.json()).data;}));
  const publicData=()=>publicDataPromise||(publicDataPromise=fetch('/api/catalog',{cache:'no-store'}).then(async r=>{if(!r.ok)throw new Error('Katalog verisi alınamadı.');return (await r.json()).data;}));
  const money=n=>new Intl.NumberFormat('tr-TR',{maximumFractionDigits:0}).format(n||0);
  const PRICE_CHANGE_THRESHOLD_TL=500;
  const stepped=(raw,last)=>{if(!(raw>0))return 0;if(!(last>0))return Math.round(raw);return Math.abs(raw-last)>=PRICE_CHANGE_THRESHOLD_TL?Math.round(raw):last;};
  function selectNumber(input){
    if(!input||handled.has(input))return;
    handled.add(input);
    const selectAll=()=>setTimeout(()=>{try{input.select();}catch{}},0);
    input.addEventListener('focus',selectAll);
    input.addEventListener('pointerdown',event=>{if(document.activeElement!==input){event.preventDefault();input.focus();selectAll();}});
  }
  function removeBlogUi(){
    document.querySelectorAll('a[href="/blog"],a[href^="/blog/"]').forEach(hide);
    if(location.pathname==='/admin'){
      document.querySelectorAll('nav button,[role="tab"],a').forEach(el=>{if(textOf(el)==='Blog'||el.getAttribute('title')==='Blog')hide(el);});
      document.querySelectorAll('.menu-edit').forEach(row=>{const values=Array.from(row.querySelectorAll('input')).map(i=>i.value);if(values.some(v=>v==='/blog'||v.startsWith('/blog/')))row.remove();});
    }
  }
  function removeLogoUi(){
    document.querySelectorAll('.brand-mark').forEach(hide);
    if(location.pathname!=='/admin')return;
    document.querySelectorAll('label').forEach(label=>{
      if(textOf(label)!=='Logo')return;
      hide(label);
      let next=label.nextElementSibling;
      while(next){
        if(next.matches('img[alt="Logo"]')){const current=next;next=next.nextElementSibling;hide(current);continue;}
        if(next.matches('.upload-button'))hide(next);
        break;
      }
    });
  }
  function improveInputs(){document.querySelectorAll('input[type="number"]').forEach(selectNumber);}
  async function patchRateUi(){
    if(location.pathname!=='/admin')return;
    for(const box of document.querySelectorAll('.admin-box')){
      const title=box.querySelector('h2');
      if(textOf(title)!=='Kur ve fiyat ayarları'||rateBoxesPending.has(box))continue;
      const rateHelp=Array.from(box.querySelectorAll('p')).find(par=>textOf(par).startsWith('Otomatik takip açıkken'));
      const alreadyPatched=box.querySelector('[data-r9-rate-status]')&&box.querySelector('[data-r9-rate-refresh]')&&(!rateHelp||textOf(rateHelp).includes('TCMB Euro döviz alış kuru'));
      if(alreadyPatched)continue;
      rateBoxesPending.add(box);
      box.querySelectorAll('.check-label').forEach(label=>{
        if(textOf(label).includes('TCMB euro döviz satış kurunu otomatik takip et')){
          Array.from(label.childNodes).filter(n=>n.nodeType===Node.TEXT_NODE).forEach(n=>n.textContent=' Borsa İstanbul referans hesaplamasında kullanılan TCMB Euro döviz alış kurunu otomatik takip et');
        }
      });
      box.querySelectorAll('p').forEach(par=>{
        const desired='Otomatik takip açıkken resmi TCMB Euro döviz alış kuru düzenli kontrol edilir. Borsa İstanbul referans fiyatlarında da TCMB döviz alış kuru esas alınır. Veri alınamazsa son geçerli kur ve yayınlanan TL fiyatları korunur.';
        if(textOf(par).startsWith('Otomatik takip açıkken')&&textOf(par)!==desired)par.textContent=desired;
      });
      if(!box.querySelector('[data-r9-rate-status]')){
        try{
          const data=await adminData(),s=data.settings||{};
          const status=document.createElement('div');status.className='rate-status';status.dataset.r9RateStatus='1';
          const date=s.rateDate?new Date(s.rateDate+'T00:00:00').toLocaleDateString('tr-TR'):'Resmi kur tarihi henüz alınmadı';
          const checked=s.lastRate?new Date(s.lastRate).toLocaleString('tr-TR'):'Henüz resmi kontrol yapılmadı';
          status.innerHTML='<strong></strong><span></span><span></span>';
          status.children[0].textContent=s.rateSource||'Kur kaynağı bekleniyor';
          status.children[1].textContent=s.rateDate?'Kur tarihi: '+date:date;
          status.children[2].textContent=s.lastRate?'Son kontrol: '+checked:checked;
          const checkbox=box.querySelector('.check-label');if(checkbox)checkbox.insertAdjacentElement('afterend',status);else box.append(status);
        }catch{}
      }
      if(!box.querySelector('[data-r9-rate-refresh]')){
        const before=Array.from(box.querySelectorAll('button')).find(b=>textOf(b)==='Bu kurla TL fiyatlarını hesapla');
        if(before){
          const button=document.createElement('button');button.type='button';button.className='button secondary';button.dataset.r9RateRefresh='1';button.textContent='Resmi kuru şimdi güncelle';
          button.addEventListener('click',async()=>{
            if(document.querySelector('.unsaved')){alert('Resmi kuru yenilemeden önce mevcut değişiklikleri kaydedin.');return;}
            button.disabled=true;const original=button.textContent;button.textContent='Kur alınıyor…';
            try{const response=await fetch('/api/rate/refresh',{method:'POST'});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.error||'Resmi kur alınamadı.');location.reload();}
            catch(error){alert(error.message||'Resmi kur alınamadı.');button.disabled=false;button.textContent=original;}
          });
          before.insertAdjacentElement('beforebegin',button);
        }
      }
      rateBoxesPending.delete(box);
    }
  }
  async function patchProductPricePreview(){
    if(location.pathname!=='/admin')return;
    for(const dialog of document.querySelectorAll('[role="dialog"]')){
      if(!textOf(dialog).includes('Ürün düzenle')||dialog.querySelector('[data-r9-price-preview]'))continue;
      const labels=Array.from(dialog.querySelectorAll('label'));
      const euroLabel=labels.find(x=>textOf(x).startsWith('Euro fiyatı'));
      const codeLabel=labels.find(x=>textOf(x).startsWith('Ürün kodu'));
      const euroInput=euroLabel?.querySelector('input[type="number"]');
      const codeInput=codeLabel?.querySelector('input');
      if(!euroInput)return;
      const preview=document.createElement('div');preview.className='price-preview-box';preview.dataset.r9PricePreview='1';
      const parent=euroLabel.closest('.two-col')||euroLabel;
      parent.insertAdjacentElement('afterend',preview);
      let data;try{data=await adminData();}catch{return;}
      const render=()=>{
        const rate=Number(data.settings?.rate||0),euro=Number(euroInput.value||0),code=(codeInput?.value||'').trim().toLowerCase();
        const old=data.products?.find(p=>((p.code||p.id||'').trim().toLowerCase()===code));const last=Number(old?.tl||0);
        if(!(rate>0)||!(euro>0)){preview.textContent='Euro fiyatı ve geçerli kur olduğunda TL önizlemesi burada görünür.';return;}
        const published=stepped(euro*rate,last);
        preview.innerHTML='<span></span><strong></strong><small></small>';
        preview.children[0].textContent='Kur: '+rate.toLocaleString('tr-TR',{minimumFractionDigits:4,maximumFractionDigits:4})+' TL'+(data.settings.rateSource?' · '+data.settings.rateSource:'');
        preview.children[1].textContent='Yayınlanacak yaklaşık fiyat: '+money(published)+' TL + KDV';
        preview.children[2].textContent='500 TL değişim eşiği uygulanır; hesaplanan karşılık 500 TL’den az değişirse mevcut yayın fiyatı korunur.';
      };
      euroInput.addEventListener('input',render);codeInput?.addEventListener('input',render);render();
    }
  }
  async function patchVatLabels(){
    if(location.pathname==='/admin')return;
    let data;try{data=await publicData();}catch{return;}
    const byId=new Map((data.products||[]).map(p=>[String(p.id),p]));
    const vat=p=>Number(p?.vatRate??20).toLocaleString('tr-TR',{maximumFractionDigits:2});
    document.querySelectorAll('.product-card').forEach(card=>{
      const link=card.querySelector('a[href^="/urun/"]');if(!link)return;
      const id=decodeURIComponent((link.getAttribute('href')||'').slice('/urun/'.length));const p=byId.get(id);if(!p)return;
      const small=card.querySelector('.price small');if(small)small.textContent='TL + %'+vat(p)+' KDV';
    });
    const m=location.pathname.match(/^\/urun\/([^/]+)/);if(m){const p=byId.get(decodeURIComponent(m[1]));if(p){const small=document.querySelector('.detail-price small');if(small)small.textContent='TL + %'+vat(p)+' KDV';const note=document.querySelector('.tax-note');if(note)note.textContent='Fiyata %'+vat(p)+' KDV dâhil değildir.';}}
  }
  function closePriceModal(){document.querySelector('[data-r9-price-modal]')?.remove();}
  function showPricePreview(file,rows){
    closePriceModal();const overlay=document.createElement('div');overlay.className='r9-modal-backdrop';overlay.dataset.r9PriceModal='1';
    const modal=document.createElement('div');modal.className='r9-modal';const h=document.createElement('h2');h.textContent='Excel / CSV fiyat önizlemesi';modal.append(h);
    const info=document.createElement('p');const errors=rows.filter(r=>r.error).length;info.textContent=file.name+' · '+rows.length+' satır · '+(errors?errors+' hata':'hata yok');modal.append(info);
    const scroller=document.createElement('div');scroller.className='r9-price-table-wrap';const table=document.createElement('table');
    const thead=document.createElement('thead');const hr=document.createElement('tr');['Kod / Ürün','Mevcut €','Yeni €','KDV','Yeni TL','Durum'].forEach(v=>{const th=document.createElement('th');th.textContent=v;hr.append(th);});thead.append(hr);table.append(thead);
    const tbody=document.createElement('tbody');rows.forEach(r=>{const tr=document.createElement('tr');const vals=[r.code+(r.name?' · '+r.name:''),Number(r.currentEuro||0).toLocaleString('tr-TR')+' €',Number(r.newEuro||0).toLocaleString('tr-TR')+' €','%'+Number(r.newVat??20).toLocaleString('tr-TR',{maximumFractionDigits:2}),money(r.newTl)+' TL',r.error||'Hazır'];vals.forEach((v,i)=>{const td=document.createElement('td');td.textContent=v;if(i===5&&r.error)td.className='r9-error';tr.append(td);});tbody.append(tr);});table.append(tbody);scroller.append(table);modal.append(scroller);
    if(errors){const warn=document.createElement('p');warn.className='error-banner';warn.textContent='Dosyada '+errors+' hatalı satır var. Hatalar düzeltilmeden içe aktarma yapılmaz.';modal.append(warn);}
    const actions=document.createElement('div');actions.className='r9-modal-actions';const cancel=document.createElement('button');cancel.className='small-btn';cancel.textContent='Kapat';cancel.onclick=closePriceModal;actions.append(cancel);
    const applyButton=document.createElement('button');applyButton.className='button';applyButton.textContent='Önizlenen fiyatları kaydet ve yayınla';applyButton.disabled=!!errors||!rows.length;applyButton.onclick=async()=>{applyButton.disabled=true;applyButton.textContent='Kaydediliyor…';try{const fresh=await fetch('/api/catalog?admin=1',{cache:'no-store'}).then(r=>r.json());const form=new FormData();form.append('file',file);form.append('revision',String(fresh.revision));const response=await fetch('/api/prices/import',{method:'POST',body:form});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.error||'Fiyatlar içe aktarılamadı.');location.reload();}catch(e){alert(e.message||'Fiyatlar içe aktarılamadı.');applyButton.disabled=false;applyButton.textContent='Önizlenen fiyatları kaydet ve yayınla';}};actions.append(applyButton);modal.append(actions);overlay.append(modal);overlay.addEventListener('click',e=>{if(e.target===overlay)closePriceModal();});document.body.append(overlay);
  }
  function installSecurityLink(){
    if(location.pathname!=='/admin')return;
    const nav=document.querySelector('.admin-sidebar nav');if(!nav||nav.querySelector('[data-r92-security-link]'))return;
    const link=document.createElement('a');link.href='/admin/security';link.dataset.r92SecurityLink='1';link.className='r92-security-link';link.textContent='Güvenlik';nav.append(link);
  }
  function installPriceSpreadsheetTools(){
    if(location.pathname!=='/admin'||textOf(document.querySelector('.admin-header h1'))!=='Fiyat yönetimi')return;
    const toolbar=document.querySelector('.admin-toolbar');if(!toolbar||toolbar.querySelector('[data-r9-price-tools]'))return;
    const group=document.createElement('div');group.className='r9-price-tools';group.dataset.r9PriceTools='1';
    const template=document.createElement('a');template.className='small-btn';template.href='/api/prices/template';template.textContent='Excel şablonu';group.append(template);
    const label=document.createElement('label');label.className='upload-button';label.textContent='Excel/CSV fiyat aktar';const input=document.createElement('input');input.type='file';input.accept='.xlsx,.csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv';label.append(input);group.append(label);toolbar.append(group);
    input.addEventListener('change',async()=>{const file=input.files?.[0];input.value='';if(!file)return;try{label.setAttribute('aria-busy','true');const form=new FormData();form.append('file',file);const response=await fetch('/api/prices/preview',{method:'POST',body:form});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.error||'Fiyat dosyası önizlenemedi.');showPricePreview(file,body.rows||[]);}catch(e){alert(e.message||'Fiyat dosyası önizlenemedi.');}finally{label.removeAttribute('aria-busy');}});
  }
  function apply(){removeBlogUi();removeLogoUi();improveInputs();patchRateUi();patchProductPricePreview();installPriceSpreadsheetTools();installSecurityLink();}
  let applyQueued=false;function scheduleApply(){if(applyQueued)return;applyQueued=true;requestAnimationFrame(()=>{applyQueued=false;apply();});}
  if(location.pathname==='/blog'||location.pathname.startsWith('/blog/')){location.replace('/urunler');return;}
  const start=()=>{scheduleApply();new MutationObserver(scheduleApply).observe(document.body,{childList:true,subtree:true});};
  if(document.body)start();else document.addEventListener('DOMContentLoaded',start,{once:true});
})();
