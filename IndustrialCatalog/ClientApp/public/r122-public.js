(()=>{
 const $=(s,r=document)=>r.querySelector(s), $$=(s,r=document)=>Array.from(r.querySelectorAll(s));
 const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const productInfo=()=>{const title=$('.detail-summary h1')?.textContent?.trim()||'';const code=$('.detail-summary .product-code strong')?.textContent?.trim()||'';return {title,code};};
 function quoteFlow(){
  if(location.pathname.startsWith('/urun/')){
   const summary=$('.detail-summary');if(summary&&!$('.detail-action-row',summary)){
    const {title,code}=productInfo();const row=document.createElement('div');row.className='detail-action-row';
    const quote=document.createElement('a');quote.className='button quote-button';quote.textContent='Teklif iste';quote.href='/iletisim?amac=teklif&urun='+encodeURIComponent(title)+'&kod='+encodeURIComponent(code);row.appendChild(quote);
    const existing=$('a.whatsapp-button,a.button[href^="/iletisim"]',summary);if(existing)row.appendChild(existing);
    const pdf=$('.pdf-link',summary);if(pdf)summary.insertBefore(row,pdf);else summary.appendChild(row);
   }
  }
  if(location.pathname==='/iletisim'){
   const q=new URLSearchParams(location.search);if(q.get('amac')==='teklif'){
    const ta=$('textarea[name="message"]');const product=q.get('urun')||'';const code=q.get('kod')||'';
    if(ta&&product&&(!ta.value||/hakkında bilgi almak istiyorum/i.test(ta.value)))ta.value=product+(code?' ('+code+')':'')+' için fiyat ve teslimat bilgilerini içeren teklif almak istiyorum.';
   }
  }
 }
 function megaMenu(){
  $$('.main-nav .nav-dropdown').forEach(drop=>{
   drop.classList.add('mega-menu');if($('.mega-grid',drop))return;
   const links=$$('a',drop);const all=links.find(x=>x.classList.contains('nav-dropdown-all'));const rest=links.filter(x=>x!==all);if(!rest.length)return;
   const grid=document.createElement('div');grid.className='mega-grid mega-flat';rest.forEach(x=>grid.appendChild(x));drop.appendChild(grid);
  });
 }
 function pagination(){
  const catalogue=$('.catalogue'),grid=catalogue&&$('.product-grid',catalogue),bar=catalogue&&$('.filterbar',catalogue);if(!grid||!bar)return;
  if($('.page-size-select',bar)){ $('.fallback-page-size',bar)?.remove(); $('.catalog-pagination[data-fallback-pagination]',catalogue)?.remove(); return; }
  if($('.catalog-pagination',catalogue))return;
  let page=1,size=matchMedia('(max-width:600px)').matches?12:24,lastCount=-1;
  const select=document.createElement('select');select.className='fallback-page-size';select.setAttribute('aria-label','Sayfa başına ürün');[12,24,48].forEach(n=>{const o=document.createElement('option');o.value=String(n);o.textContent=n+' / sayfa';if(n===size)o.selected=true;select.appendChild(o)});bar.insertBefore(select,$('.result-count',bar)||null);
  const nav=document.createElement('nav');nav.className='catalog-pagination';nav.dataset.fallbackPagination='1';nav.setAttribute('aria-label','Ürün sayfaları');
  const prev=document.createElement('button');prev.type='button';prev.className='pagination-button';prev.textContent='‹ Önceki';
  const status=document.createElement('span');status.className='pagination-status';
  const next=document.createElement('button');next.type='button';next.className='pagination-button';next.textContent='Sonraki ›';nav.append(prev,status,next);grid.after(nav);
  const draw=(reset=false)=>{const cards=$$('.product-card',grid);if(reset||cards.length!==lastCount)page=1;lastCount=cards.length;const pages=Math.max(1,Math.ceil(cards.length/size));page=Math.min(page,pages);cards.forEach((c,i)=>c.hidden=!(i>=(page-1)*size&&i<page*size));prev.disabled=page<=1;next.disabled=page>=pages;status.innerHTML='<strong>'+page+'</strong> / '+pages+'<small>'+cards.length+' ürün</small>';nav.hidden=cards.length<=size;};
  prev.addEventListener('click',()=>{page--;draw();catalogue.scrollIntoView({behavior:'smooth',block:'start'})});next.addEventListener('click',()=>{page++;draw();catalogue.scrollIntoView({behavior:'smooth',block:'start'})});select.addEventListener('change',()=>{size=Number(select.value)||24;draw(true)});
  bar.addEventListener('input',()=>setTimeout(()=>draw(true),0));bar.addEventListener('change',()=>setTimeout(()=>draw(true),0));
  new MutationObserver(()=>draw()).observe(grid,{childList:true});draw();
 }
 async function footer(){
  if($('.site-footer'))return;
  try{
   const r=await fetch('/api/catalog',{cache:'no-store'});if(!r.ok)return;const body=await r.json();const d=body.data||{},s=d.settings||{},cats=(d.categories||[]).filter(c=>!c.parent).slice(0,5);
   const footer=document.createElement('footer');footer.className='site-footer';
   footer.innerHTML='<div class="wrap footer-service-row"><div><span class="footer-kicker">İNOKSKAR DESTEK</span><strong>Satış öncesi danışmanlık ve satış sonrası servis tek noktada.</strong></div><div class="footer-service-actions"><a href="/iletisim">Teklif / bilgi alın</a><a href="/iletisim?amac=servis">Teknik Servis</a><a href="/garanti-sorgulama">Garanti sorgula</a></div></div><div class="wrap footer-grid"><div><a class="brand footer-brand" href="/"><img class="footer-brand-image" src="'+esc(s.headerImage||'/inokskar-header-brand.png')+'" alt="'+esc(s.name||'İNOKSKAR')+'"></a><p>'+esc(s.tagline||'Soğutma · Mutfak · Mobilya')+'</p><p>Profesyonel soğutma, endüstriyel mutfak ve ticari çalışma alanları için ürün ve servis çözümleri.</p></div><div><h3>Katalog</h3><a href="/urunler">Tüm ürünler</a>'+cats.map(c=>'<a href="/kategori/'+encodeURIComponent(c.id)+'">'+esc(c.name)+'</a>').join('')+'</div><div><h3>Destek</h3><a href="/iletisim">İletişim</a><a href="/iletisim?amac=servis">Teknik Servis</a><a href="/garanti-sorgulama">Garanti sorgula</a><a href="/hakkimizda">Hakkımızda</a></div><div><h3>İletişim</h3>'+(s.phone?'<a href="tel:'+esc(s.phone)+'">'+esc(s.phone)+'</a>':'')+(s.email?'<a href="mailto:'+esc(s.email)+'">'+esc(s.email)+'</a>':'')+(s.address?'<p>'+esc(s.address)+'</p>':'')+(s.hours?'<p>'+esc(s.hours)+'</p>':'')+'</div></div><div class="wrap footer-bottom"><span>© '+new Date().getFullYear()+' '+esc(s.name||'İNOKSKAR')+'. Tüm hakları saklıdır.</span><span class="footer-legal"><a href="/gizlilik">Gizlilik</a><a href="/kvkk">KVKK</a><a href="/cerez">Çerezler</a></span></div>';
   document.body.appendChild(footer);
  }catch{}
 }
 function run(){quoteFlow();megaMenu();pagination();footer();}
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',()=>setTimeout(run,0),{once:true});else setTimeout(run,0);
 let attempts=0;const timer=setInterval(()=>{quoteFlow();megaMenu();if(!$('.catalog-pagination'))pagination();if(++attempts>20)clearInterval(timer)},250);
})();
