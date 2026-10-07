(function(){
  'use strict';
  const txt=n=>(n&&n.textContent||'').trim();
  let rowsCache=null,loading=null;
  async function inquiryRows(){
    if(Array.isArray(rowsCache))return rowsCache;
    if(Array.isArray(window.__r95Inquiries)){rowsCache=window.__r95Inquiries;return rowsCache;}
    if(loading)return loading;
    loading=(async()=>{try{const r=await fetch('/api/catalog?admin=1',{cache:'no-store'});if(!r.ok)return[];const b=await r.json();rowsCache=b.inquiries||[];window.__r95Inquiries=rowsCache;return rowsCache;}catch{return[]}finally{loading=null;}})();
    return loading;
  }
  function openRequestedTab(){
    if(location.pathname!=='/admin'||new URLSearchParams(location.search).get('tab')!=='inquiries')return;
    const button=Array.from(document.querySelectorAll('.admin-sidebar button')).find(x=>txt(x)==='Müşteri talepleri');
    if(button&&!button.classList.contains('active'))button.click();
  }
  function matchRow(card,rows,index){
    const heading=card.querySelector('.list-line');
    const name=txt(heading&&heading.querySelector('h2')),date=txt(heading&&heading.querySelector('small'));
    const exact=rows.find(x=>{try{const b=JSON.parse(x.data);return b.name===name&&new Date(x.created).toLocaleString('tr-TR')===date;}catch{return false;}});
    return exact||rows[index]||null;
  }
  async function patch(){
    if(location.pathname!=='/admin'||txt(document.querySelector('.admin-header h1'))!=='Müşteri talepleri')return;
    const rows=await inquiryRows();
    const cards=Array.from(document.querySelectorAll('.admin-content>.admin-box')).filter(card=>card.querySelector('.list-line'));
    cards.forEach((card,index)=>{
      const row=matchRow(card,rows,index);if(!row)return;
      let payload={};try{payload=JSON.parse(row.data||'{}')}catch{}
      if(!card.querySelector('[data-r10-request-type]')){const badge=document.createElement('span');badge.dataset.r10RequestType='1';badge.className=payload.type==='service'?'request-type service':'request-type';badge.textContent=payload.type==='service'?'Servis Talebi':'Destek Talebi';const line=card.querySelector('.list-line>div')||card.querySelector('.list-line');if(line)line.prepend(badge);}
      if(payload.type==='service'&&!card.querySelector('[data-r10-service-status]')){const stat=document.createElement('span');stat.dataset.r10ServiceStatus='1';stat.className='r10-service-status';const map={new:'Yeni',review:'İnceleniyor',scheduled:'Servis planlandı',parts:'Parça bekliyor',completed:'Tamamlandı',cancelled:'İptal'};stat.textContent=map[payload.serviceStatus||row.status]||'Yeni';card.append(stat);}
      let actions=card.querySelector('.r98-inquiry-actions');
      if(!actions){actions=document.createElement('div');actions.className='r98-inquiry-actions';card.append(actions);}
      if(!card.querySelector('[data-r98-inquiry-detail]')){const a=document.createElement('a');a.className='small-btn';a.dataset.r98InquiryDetail='1';a.href='/admin/inquiries/'+encodeURIComponent(row.id);a.textContent='Detay';actions.append(a);}
      if(!card.querySelector('[data-r98-inquiry-delete]')&&!card.querySelector('[data-r95-inquiry-delete]')){const b=document.createElement('button');b.type='button';b.className='small-btn danger';b.dataset.r98InquiryDelete='1';b.textContent='Sil';b.onclick=async()=>{if(!confirm('Bu müşteri talebi kalıcı olarak silinsin mi?'))return;b.disabled=true;try{const r=await fetch('/api/inquiries/'+encodeURIComponent(row.id),{method:'DELETE'});const body=await r.json().catch(()=>({}));if(!r.ok)throw new Error(body.error||'Talep silinemedi.');rowsCache=rows.filter(x=>x.id!==row.id);window.__r95Inquiries=rowsCache;card.remove();}catch(e){alert(e.message||'Talep silinemedi.');b.disabled=false;}};actions.append(b);}
    });
  }
  let queued=false;function schedule(){if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;openRequestedTab();patch();});}
  new MutationObserver(schedule).observe(document.documentElement,{subtree:true,childList:true});
  schedule();
})();
