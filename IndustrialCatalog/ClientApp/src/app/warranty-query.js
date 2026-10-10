export function initializeWarranty(root){
  'use strict';
  const form=root.querySelector('#warranty-query-form'),result=root.querySelector('#warranty-result');
  if(!form||!result)return;
  const trDate=value=>{if(!value)return '—';const d=new Date(value+'T12:00:00');return Number.isNaN(d.getTime())?value:d.toLocaleDateString('tr-TR');};
  const el=(tag,cls,text)=>{const n=document.createElement(tag);if(cls)n.className=cls;if(text!=null)n.textContent=text;return n;};
  function clear(){result.replaceChildren();}
  function showError(message){clear();const box=el('div','warranty-message warranty-message-error');box.append(el('strong','', 'Garanti kaydı doğrulanamadı'),el('p','',message||'Girilen bilgilerle eşleşen garanti kaydı bulunamadı.'));result.append(box);}
  function statusText(w){const d=Number(w.daysRemaining||0);if(w.status==='active'){if(d===0)return 'GARANTİNİZİN SON GÜNÜ';return 'GARANTİNİZİN BİTMESİNE '+d+' GÜN KALDI';}return 'GARANTİ SÜRENİZ '+Math.abs(d)+' GÜN ÖNCE SONA ERDİ';}
  function componentText(c){if(c.status==='excluded')return 'Garanti kapsamı dışında';const d=Number(c.daysRemaining||0);if(c.status==='active')return d===0?'Son gün':d+' gün kaldı';return Math.abs(d)+' gün önce sona erdi';}
  function render(w){
    clear();const card=el('section','warranty-record');
    const head=el('div','warranty-record-head');const state=el('span','warranty-record-state '+(w.status==='active'?'is-active':'is-expired'),w.status==='active'?'✓ Garanti Kaydı Doğrulandı':'Garanti Kaydı Doğrulandı');head.append(state,el('h2','',w.productName||'Ürün'));card.append(head);
    const meta=el('div','warranty-meta');[['Ürün Kodu',w.productCode],['Seri Numarası',w.serialNumber],['Teslim Tarihi',trDate(w.deliveryDate)],['Garanti Süresi',(w.warrantyMonths||0)+' Ay'],['Garanti Bitiş Tarihi',trDate(w.warrantyEndDate)]].forEach(([k,v])=>{const item=el('div','warranty-meta-item');item.append(el('span','',k),el('strong','',v||'—'));meta.append(item);});card.append(meta);
    const remaining=el('div','warranty-remaining '+(w.status==='active'?'is-active':'is-expired'));remaining.append(el('small','',w.status==='active'?'GARANTİ DURUMU':'GARANTİ DURUMU'),el('strong','',statusText(w)),el('span','',w.status==='active'?'Bitiş: '+trDate(w.warrantyEndDate):'Garanti bitiş tarihi: '+trDate(w.warrantyEndDate)));card.append(remaining);
    const scope=el('div','warranty-scope');scope.append(el('div','warranty-scope-title','Garanti Kapsamı'));
    (Array.isArray(w.components)?w.components:[]).forEach(c=>{const row=el('div','warranty-component '+(c.status==='excluded'?'is-excluded':c.status==='active'?'is-active':'is-expired'));const main=el('div','');const title=el('strong','',c.name||'Bileşen');const note=el('span','',c.note||'');main.append(title);if(c.note)main.append(note);const side=el('div','warranty-component-status');side.append(el('strong','',c.status==='excluded'?'Kapsam dışı':(c.months||0)+' Ay'),el('span','',componentText(c)));row.append(main,side);scope.append(row);});
    card.append(scope);const foot=el('div','warranty-record-foot');foot.append(el('p','', 'Bu ekran yalnız ürün ve garanti durumunu gösterir; müşteri kişisel bilgileri yayımlanmaz.'));const a=el('a','button','Servis talebi oluştur');const q=new URLSearchParams({productName:String(w.productName||''),productCode:String(w.productCode||''),serial:String(w.serialNumber||'')});a.href='/servis-talebi?'+q.toString();foot.append(a);card.append(foot);result.append(card);
  }
  const controller=new AbortController();
  const submit=async e=>{e.preventDefault();const button=form.querySelector('button[type="submit"]');const data=new FormData(form);button.disabled=true;button.textContent='Sorgulanıyor…';clear();const loading=el('div','warranty-message');loading.textContent='Garanti kaydı güvenli şekilde kontrol ediliyor…';result.append(loading);try{const r=await fetch('/api/warranty/query',{method:'POST',signal:controller.signal,headers:{'Content-Type':'application/json'},body:JSON.stringify({serialNumber:String(data.get('serialNumber')||''),verificationCode:String(data.get('verificationCode')||'')})});const b=await r.json().catch(()=>({}));if(!r.ok)throw new Error('Sorgulama şu anda tamamlanamadı.');if(!b.found){showError(b.message);return;}render(b.warranty||{});}catch(err){if(controller.signal.aborted)return;showError(err&&err.message?err.message:'Sorgulama şu anda tamamlanamadı.');}finally{button.disabled=false;button.textContent='Garantiyi sorgula';}};
  form.addEventListener('submit',submit);
  return ()=>{controller.abort();form.removeEventListener('submit',submit);};
}
