/* R23 generated application runtime. Legacy behaviors are retained but share one DOM observer. */
globalThis.__inokskarR23Schedules=[];

/* source: r95-runtime.js */
(function(){
  'use strict';
  const text=el=>(el&&el.textContent||'').replace(/\s+/g,' ').trim();
  function publicWarrantyLink(){if(location.pathname.startsWith('/admin'))return;const nav=document.querySelector('.main-nav');if(nav&&!nav.querySelector('a[href="/garanti-sorgulama"]')){const a=document.createElement('a');a.href='/garanti-sorgulama';a.textContent='Garanti Sorgula';nav.append(a);}document.querySelectorAll('footer div').forEach(div=>{const h=div.querySelector('h3');if(text(h)!=='Keşfedin'||div.querySelector('a[href="/garanti-sorgulama"]'))return;const a=document.createElement('a');a.href='/garanti-sorgulama';a.textContent='Garanti Sorgula';div.append(a);});}
  function adminWarrantyLink(){if(location.pathname!=='/admin')return;const nav=document.querySelector('.admin-sidebar nav');if(!nav||nav.querySelector('a[href="/admin/warranties"]')||nav.querySelector('[data-r95-warranty-link]'))return;const a=document.createElement('a');a.href='/admin/warranties';a.className='r95-warranty-admin-link';a.dataset.r95WarrantyLink='1';a.textContent='Garanti & Servis';nav.append(a);}
  function customerDeleteButtons(){if(location.pathname!=='/admin')return;const title=document.querySelector('.admin-header h1');if(text(title)!=='Müşteri talepleri')return;document.querySelectorAll('.admin-content>.admin-box').forEach(card=>{if(card.querySelector('[data-r95-inquiry-delete]')||Array.from(card.querySelectorAll('button')).some(x=>text(x)==='Sil'))return;const heading=card.querySelector('.list-line');if(!heading)return;const name=text(heading.querySelector('h2')),date=text(heading.querySelector('small'));const candidates=window.__r95Inquiries||[];const match=candidates.find(x=>{try{const b=JSON.parse(x.data);return b.name===name&&new Date(x.created).toLocaleString('tr-TR')===date;}catch{return false;}});if(!match)return;const b=document.createElement('button');b.type='button';b.className='small-btn danger';b.dataset.r95InquiryDelete='1';b.textContent='Sil';b.onclick=async()=>{if(!confirm('Bu müşteri talebi kalıcı olarak silinsin mi?'))return;b.disabled=true;try{const r=await fetch('/api/inquiries/'+encodeURIComponent(match.id),{method:'DELETE'});const body=await r.json().catch(()=>({}));if(!r.ok)throw new Error(body.error||'Talep silinemedi.');card.remove();location.reload();}catch(e){alert(e.message||'Talep silinemedi.');b.disabled=false;}};heading.append(b);});}
  let inquiriesPromise=null;async function loadInquiries(){if(location.pathname!=='/admin'||text(document.querySelector('.admin-header h1'))!=='Müşteri talepleri'||window.__r95Inquiries)return;if(inquiriesPromise)return inquiriesPromise;inquiriesPromise=(async()=>{try{const r=await fetch('/api/catalog?admin=1',{cache:'no-store'});if(!r.ok)return;const b=await r.json();window.__r95Inquiries=b.inquiries||[];}catch{}finally{inquiriesPromise=null;}})();return inquiriesPromise;}
  let queued=false;function schedule(){if(queued)return;queued=true;requestAnimationFrame(async()=>{queued=false;publicWarrantyLink();adminWarrantyLink();await loadInquiries();customerDeleteButtons();});}
  const start=()=>{schedule();globalThis.__inokskarR23Schedules?.push(schedule);};if(document.body)start();else document.addEventListener('DOMContentLoaded',start,{once:true});
})();


/* source: r99-brand-runtime.js */
(function(){
  'use strict';
  let settings=null,loading=null,queued=false;
  async function getSettings(){if(settings)return settings;if(loading)return loading;loading=(async()=>{try{const r=await fetch('/api/catalog',{cache:'no-store'});if(!r.ok)return null;const b=await r.json();settings=b.data&&b.data.settings||null;return settings;}catch{return null;}finally{loading=null;}})();return loading;}
  async function publicBrand(){if(location.pathname.startsWith('/admin')||location.pathname==='/login')return;const brand=document.querySelector('.site-header .brand');if(!brand||brand.dataset.r99Brand==='1')return;const s=await getSettings();if(!s)return;const src=s.headerImage||'/inokskar-header-brand.png';brand.replaceChildren();brand.classList.add('brand-image-link');brand.dataset.r99Brand='1';brand.setAttribute('aria-label',(s.name||'İNOKSKAR')+' '+(s.tagline||''));const img=document.createElement('img');img.className='header-brand-image';img.src=src;img.alt=(s.name||'İNOKSKAR')+' '+(s.tagline||'');brand.append(img);}
  function adminBrandLink(){if(location.pathname!=='/admin')return;const nav=document.querySelector('.admin-sidebar nav');if(!nav||nav.querySelector('[data-r99-brand-link]'))return;const a=document.createElement('a');a.href='/admin/header-brand';a.className='r99-brand-admin-link';a.dataset.r99BrandLink='1';a.textContent='Üst Başlık Görseli';nav.append(a);}
  function adminBrandSettingsCard(){if(location.pathname!=='/admin')return;const title=(document.querySelector('.admin-header h1')?.textContent||'').trim();if(title!=='Site ayarları')return;const content=document.querySelector('.admin-content');if(!content||content.querySelector('[data-r99-brand-settings]'))return;const card=document.createElement('section');card.className='admin-box';card.dataset.r99BrandSettings='1';const h=document.createElement('h2');h.textContent='Üst başlık marka görseli';const p=document.createElement('p');p.className='muted';p.textContent='Site başlığının sol tarafındaki İnokskar görselini değiştirebilirsiniz. Görsel oranı bozulmadan alana sığdırılır.';const a=document.createElement('a');a.className='button secondary';a.href='/admin/header-brand';a.textContent='Üst başlık görselini değiştir';card.append(h,p,a);content.append(card);}
  function schedule(){if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;adminBrandLink();adminBrandSettingsCard();publicBrand();});}
  globalThis.__inokskarR23Schedules?.push(schedule);schedule();
})();

/* source: r103-runtime.js */
(function(){
  'use strict';
  const text=n=>(n&&n.textContent||'').trim();
  const isPublic=()=>!location.pathname.startsWith('/admin')&&!location.pathname.startsWith('/login')&&!location.pathname.startsWith('/forgot-password');
  const nativeFetch=window.fetch.bind(window);
  window.fetch=(input,init)=>{try{const url=typeof input==='string'?input:(input&&input.url)||'';if(location.pathname==='/admin'&&url.includes('/api/upload?kind=image')&&init&&init.body instanceof FormData){const file=init.body.get('file');if(file instanceof File&&(file.type==='video/mp4'||file.type==='image/gif')){const next=url.replace('kind=image','kind=hero');return nativeFetch(next,init);}}}catch{}return nativeFetch(input,init);};
  function addServiceNav(){document.querySelectorAll('.main-nav').forEach(nav=>{if(!nav.querySelector('a[href="/servis-talebi"]')){const a=document.createElement('a');a.href='/servis-talebi';a.textContent='Servis Talebi';nav.append(a);}});}
  function removeBottomAreas(){if(!isPublic())return;document.querySelectorAll('footer,.consult-strip').forEach(n=>n.remove());}
  function addHomeReturn(){if(!isPublic()||location.pathname==='/')return;if(document.querySelector('.page-home-return-wrap,.public-home-return-wrap'))return;const header=document.querySelector('.site-header');if(!header)return;const box=document.createElement('div');box.className='page-home-return-wrap';box.innerHTML='<a class="page-home-return" href="/">← Ana sayfaya dön</a>';header.after(box);}
  function upgradeHeroMedia(){
    document.querySelectorAll('img.hero-image').forEach(img=>{const src=img.getAttribute('src')||'';if(!/\.mp4(?:[?#]|$)/i.test(src))return;const v=document.createElement('video');v.className=img.className+' hero-media-video';v.src=src;v.autoplay=true;v.muted=true;v.loop=true;v.playsInline=true;v.preload='metadata';v.setAttribute('aria-label','İnokskar ana sayfa videosu');img.replaceWith(v);});
  }
  function upgradeAdminHero(){if(location.pathname!=='/admin')return;const labels=Array.from(document.querySelectorAll('label'));const title=labels.find(x=>text(x).startsWith('Ana sayfa görseli')||text(x).startsWith('Ana sayfa ana medya'));if(!title)return;let n=title.nextElementSibling;for(let i=0;n&&i<4;i++,n=n.nextElementSibling){if(n.matches('img.admin-image')&&/\.mp4(?:[?#]|$)/i.test(n.getAttribute('src')||'')){const v=document.createElement('video');v.className='admin-image admin-hero-video';v.src=n.getAttribute('src')||'';v.controls=true;v.muted=true;v.playsInline=true;n.replaceWith(v);}if(n.matches('label.upload-button')){const input=n.querySelector('input[type=file]');if(input){input.accept='image/png,image/jpeg,image/webp,image/gif,video/mp4';const tn=Array.from(n.childNodes).find(x=>x.nodeType===Node.TEXT_NODE);if(tn)tn.textContent=' Görsel / GIF / MP4 yükle';}break;}}
  }
  function upgradeContact(){if(location.pathname!=='/iletisim')return;const form=document.querySelector('.inquiry-form');if(!form)return;const success=form.querySelector('.success');if(success&&!success.dataset.r10Receipt){success.dataset.r10Receipt='1';const h=success.querySelector('h2');const p=success.querySelector('p');if(h)h.textContent='Talebiniz başarıyla alındı.';if(p)p.textContent='Talep numaranız ve alındı bilgisi e-posta adresinize gönderilir. Ekibimiz talebinizi inceleyerek sizinle iletişime geçecektir.';return;}if(form.dataset.r10Contact==='1')return;const labels=Array.from(form.querySelectorAll('label'));const contactLabel=labels.find(x=>text(x).startsWith('Telefon veya e-posta'));if(!contactLabel)return;const input=contactLabel.querySelector('input');if(!input)return;contactLabel.firstChild.textContent='E-posta';input.name='email';input.type='email';input.autocomplete='email';input.required=true;const phoneLabel=document.createElement('label');phoneLabel.textContent='Telefon ';const optional=document.createElement('span');optional.className='optional-label';optional.textContent='(isteğe bağlı)';phoneLabel.append(optional);const phone=document.createElement('input');phone.name='phone';phone.type='tel';phone.maxLength=40;phone.autocomplete='tel';phoneLabel.append(phone);contactLabel.after(phoneLabel);const muted=Array.from(form.querySelectorAll('.muted')).find(x=>text(x).includes('Bilgileriniz'));if(muted)muted.textContent='Talebiniz yönetim paneline kaydedilir. E-posta adresinize kurumsal alındı bildirimi gönderilir.';const button=form.querySelector('button[type="submit"],button:not([type])');if(button)button.textContent='Destek talebini gönder';form.dataset.r10Contact='1';}
  function upgradeAdmin(){if(location.pathname!=='/admin')return;const nav=document.querySelector('.admin-sidebar nav');if(nav&&!nav.querySelector('a[href="/admin/system"]')){const a=document.createElement('a');a.href='/admin/system';a.className='r10-system-link';a.textContent='⚙ Sistem Durumu';nav.append(a);}document.querySelectorAll('.bulk-controls p').forEach(p=>{if(text(p).includes('binlik adımlarla'))p.textContent='Euro baz fiyatını değiştirin. TL fiyatı güncel kura göre hesaplanır; yayınlanan TL tutarı ancak fark 500 TL veya üzerindeyse otomatik değişir.';});upgradeAdminHero();}
  function run(){addServiceNav();removeBottomAreas();addHomeReturn();upgradeHeroMedia();upgradeContact();upgradeAdmin();}
  let queued=false;const schedule=()=>{if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;run();});};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',run);else run();globalThis.__inokskarR23Schedules?.push(schedule);
})();


/* source: r104-admin-runtime.js */
(function(){'use strict';if(!location.pathname.startsWith('/admin'))return;let me=null;const can=(m,a='view')=>me?.superAdmin||(me?.permissions||[]).includes(m+'.'+a);function apply(){const nav=document.querySelector('.admin-sidebar nav');if(!nav||!me)return;const labels={products:'Ürünler',categories:'Kategoriler',prices:'Fiyat yönetimi',settings:'Site ayarları',stats:'İstatistikler',history:'Geçmiş & yedek',inquiries:'Müşteri talepleri'};nav.querySelectorAll('button').forEach(b=>{const t=(b.textContent||'').trim();let allowed=true;if([labels.products,labels.categories,labels.stats].includes(t))allowed=can('catalog');else if(t===labels.prices)allowed=can('prices');else if(t===labels.settings)allowed=can('settings');else if(t===labels.history)allowed=can('backup');else if(t===labels.inquiries)allowed=can('inquiries')||can('service');b.hidden=!allowed;});const warranty=nav.querySelector('a[href="/admin/warranties"]');if(warranty)warranty.hidden=!can('warranties');const system=nav.querySelector('a[href="/admin/system"]');if(system)system.hidden=!can('system');if(can('system')&&!nav.querySelector('a[href="/admin/mail"]')){const a=document.createElement('a');a.className='r123-mail-link';a.href='/admin/mail';a.textContent='E-posta Sağlayıcı';nav.append(a);}if(can('technical')&&!nav.querySelector('a[href="/teknik"]')){const a=document.createElement('a');a.className='r104-technical-link';a.href='/teknik';a.textContent='Teknik Cihaz Dosyaları';nav.append(a);}if(can('users')&&!nav.querySelector('a[href="/admin/users"]')){const a=document.createElement('a');a.className='r104-users-link';a.href='/admin/users';a.textContent='Kullanıcılar & Yetkiler';nav.append(a);}const h=document.querySelector('.admin-header h1')?.textContent?.trim()||'';const module=h==='Ürünler'||h==='Kategoriler'?'catalog':h==='Fiyat yönetimi'?'prices':h==='Site ayarları'?'settings':h==='Geçmiş & yedek'?'backup':h==='Müşteri talepleri'?'inquiries':'';if(module){const save=Array.from(document.querySelectorAll('.admin-header-actions button')).find(x=>(x.textContent||'').includes('Değişiklikleri kaydet'));if(save){save.disabled=!can(module,'edit');save.title=save.disabled?'Bu bölümde müdahale yetkiniz yok.':'';}}}
fetch('/api/me',{cache:'no-store'}).then(r=>r.ok?r.json():null).then(x=>{me=x;if(!me)return;apply();globalThis.__inokskarR23Schedules?.push(apply);}).catch(()=>{});})();


/* R23 shared mutation scheduler */
(()=>{let raf=0;const run=()=>{if(raf)return;raf=requestAnimationFrame(()=>{raf=0;for(const fn of globalThis.__inokskarR23Schedules||[]){try{fn();}catch{}}});};const start=()=>{const root=document.getElementById('root')||document.body;const observer=new MutationObserver(run);observer.observe(root,{childList:true,subtree:true});window.addEventListener('inokskar:navigation',run);window.addEventListener('popstate',run);window.addEventListener('pageshow',run);run();};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();})();

/* source: r122-public.js */
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
   footer.innerHTML='<div class="wrap footer-service-row"><div><span class="footer-kicker">İNOKSKAR DESTEK</span><strong>Satış öncesi danışmanlık ve satış sonrası servis tek noktada.</strong></div><div class="footer-service-actions"><a href="/iletisim">Teklif / bilgi alın</a><a href="/servis-talebi">Servis talebi</a><a href="/garanti-sorgulama">Garanti sorgula</a></div></div><div class="wrap footer-grid"><div><a class="brand footer-brand" href="/"><img class="footer-brand-image" src="'+esc(s.headerImage||'/inokskar-header-brand.png')+'" alt="'+esc(s.name||'İNOKSKAR')+'"></a><p>'+esc(s.tagline||'Soğutma · Mutfak · Mobilya')+'</p><p>Profesyonel soğutma, endüstriyel mutfak ve ticari çalışma alanları için ürün ve servis çözümleri.</p></div><div><h3>Katalog</h3><a href="/urunler">Tüm ürünler</a>'+cats.map(c=>'<a href="/kategori/'+encodeURIComponent(c.id)+'">'+esc(c.name)+'</a>').join('')+'</div><div><h3>Destek</h3><a href="/iletisim">İletişim</a><a href="/servis-talebi">Servis talebi</a><a href="/garanti-sorgulama">Garanti sorgula</a><a href="/hakkimizda">Hakkımızda</a></div><div><h3>İletişim</h3>'+(s.phone?'<a href="tel:'+esc(s.phone)+'">'+esc(s.phone)+'</a>':'')+(s.email?'<a href="mailto:'+esc(s.email)+'">'+esc(s.email)+'</a>':'')+(s.address?'<p>'+esc(s.address)+'</p>':'')+(s.hours?'<p>'+esc(s.hours)+'</p>':'')+'</div></div><div class="wrap footer-bottom"><span>© '+new Date().getFullYear()+' '+esc(s.name||'İNOKSKAR')+'. Tüm hakları saklıdır.</span><span class="footer-legal"><a href="/gizlilik">Gizlilik</a><a href="/kvkk">KVKK</a><a href="/cerez">Çerezler</a></span></div>';
   document.body.appendChild(footer);
  }catch{}
 }
 function run(){quoteFlow();megaMenu();pagination();footer();}
 let queued=false;const schedule=()=>{if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;run();});};
 const start=()=>{schedule();const root=document.getElementById('root')||document.body;const observer=new MutationObserver(schedule);observer.observe(root,{childList:true,subtree:true});setTimeout(()=>observer.disconnect(),4000);};
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
 window.addEventListener('inokskar:navigation',schedule);window.addEventListener('popstate',schedule);
})();


/* source: r124-home.js */
(function(){
  const ICONS = {
    contact: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.79 19.79 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.12.9.33 1.78.62 2.62a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.46-1.19a2 2 0 0 1 2.11-.45c.84.29 1.72.5 2.62.62A2 2 0 0 1 22 16.92z"></path></svg>',
    account: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21a8 8 0 0 0-16 0"></path><circle cx="12" cy="7" r="4"></circle></svg>'
  };

  function ready(fn){
    if(document.readyState==='loading') document.addEventListener('DOMContentLoaded',fn,{once:true});
    else fn();
  }

  function buildUtilityLinks(headerActions){
    if(!headerActions || headerActions.querySelector('.header-util-links')) return;
    const util = document.createElement('div');
    util.className='header-util-links';
    util.innerHTML = `
      <a href="/iletisim" class="util-link util-contact">${ICONS.contact}<span>Bize Ulaşın</span></a>
      <a href="/hesabim" class="util-link util-account">${ICONS.account}<span>Hesabım</span></a>
    `;
    headerActions.append(util);
    const cta = document.createElement('a');
    cta.className='header-cta';
    cta.href='/iletisim?amac=teklif';
    cta.textContent='Teklif Al';
    headerActions.append(cta);
  }

  function enhanceMobileMenu(nav, searchForm){
    if(!nav || nav.querySelector('.mobile-home-search')) return;
    const searchWrap = document.createElement('div');
    searchWrap.className='mobile-home-search';
    if(searchForm){
      searchWrap.append(searchForm.cloneNode(true));
      const cloneInput = searchWrap.querySelector('input');
      const sourceInput = searchForm.querySelector('input');
      const sync = ()=>{ if(sourceInput && cloneInput) sourceInput.value = cloneInput.value; };
      cloneInput && cloneInput.addEventListener('input', sync);
      searchWrap.querySelector('form')?.addEventListener('submit', ()=>{ sync(); searchForm.requestSubmit(); });
    }
    nav.prepend(searchWrap);
    const account = document.createElement('a');
    account.className='mobile-account-link';
    account.href='/hesabim';
    account.innerHTML=`${ICONS.account}<span>Hesabım / Giriş</span>`;
    nav.append(account);
  }

  function retitleHero(hero){
    const h1 = hero.querySelector('h1');
    if(h1){
      h1.innerHTML='Endüstriyel<br>Mutfaklarda<br><em>Güçlü Partneriniz</em>';
    }
    const p = hero.querySelector('p');
    if(p){
      p.textContent='Profesyonel mutfaklar, güvenilir soğutma sistemleri ve endüstriyel çözümlerle işletmenizin yanında. Kalite, performans ve kesintisiz servis anlayışıyla.';
    }
    const actions = hero.querySelector('.hero-actions');
    if(actions){
      const buttons = actions.querySelectorAll('a');
      buttons.forEach((btn, index)=>{
        if(index===0){
          btn.textContent='Teklif Al';
          btn.setAttribute('href','/iletisim?amac=teklif');
          btn.classList.add('gold');
        } else if(index>0){
          btn.remove();
        }
      });
      if(!actions.querySelector('a')){
        const cta=document.createElement('a');
        cta.className='button gold';
        cta.href='/iletisim?amac=teklif';
        cta.textContent='Teklif Al';
        actions.append(cta);
      }
    }
  }

  function adjustSectionTitles(){
    document.querySelectorAll('.categories-section .section-heading .eyebrow').forEach(el=>el.textContent='ÖNE ÇIKAN KATEGORİLER');
    document.querySelectorAll('.categories-section .section-heading h2').forEach(el=>el.textContent='İhtiyacınıza uygun ekipman grubunu seçin');
    const conf = document.querySelector('.home-confidence .confidence-copy');
    if(conf){
      const h2 = conf.querySelector('h2');
      const p = conf.querySelector('p:not(.eyebrow)');
      if(h2) h2.textContent='Neden İnokskar?';
      if(p) p.textContent='Teknik servis, garanti desteği, projelendirme ve yedek parça süreçlerini tek bir kurumsal yapı altında yönetiyoruz.';
    }
    const confidenceArticles = document.querySelectorAll('.confidence-grid article');
    const labels = [
      ['Uzman Teknik Servis','Deneyimli ekibimizle her zaman yanınızdayız.'],
      ['Garanti Desteği','Tüm ürünlerimizde resmi garanti güvencesi.'],
      ['Projelendirme','İhtiyacınıza özel profesyonel çözümler.'],
      ['Yedek Parça','Orijinal yedek parça ve sürekli tedarik imkânı.']
    ];
    confidenceArticles.forEach((article, i)=>{
      const strong = article.querySelector('strong');
      const p = article.querySelector('p');
      if(labels[i]){
        if(strong) strong.textContent=labels[i][0];
        if(p) p.textContent=labels[i][1];
      }
    });
    const featuredEyebrow = document.querySelector('.featured .section-heading .eyebrow');
    const featuredH2 = document.querySelector('.featured .section-heading h2');
    if(featuredEyebrow) featuredEyebrow.textContent='ÖNE ÇIKAN ÜRÜNLER';
    if(featuredH2) featuredH2.textContent='Öne Çıkan Ürünler';
    const allLink = document.querySelector('.featured .text-link');
    if(allLink) allLink.textContent='Tüm Ürünleri Gör';
  }

  function addHomeCta(main){
    if(!main || main.querySelector('.cta-banner') || !document.body.classList.contains('page-home')) return;
    const banner = document.createElement('section');
    banner.className='cta-banner';
    banner.innerHTML=`
      <div>
        <strong>Projeniz İçin Doğru Ekipman, Güçlü Bir Çözüm</strong>
        <p>Uzman ekibimiz, projenize en uygun endüstriyel mutfak çözümleri için yanınızda.</p>
      </div>
      <a class="button" href="/iletisim?amac=teklif">Teklif Al</a>
    `;
    const featured = main.querySelector('.featured');
    if(featured) featured.insertAdjacentElement('afterend', banner);
  }

  function applyPageMode(){
    const home = location.pathname==='/' || location.pathname==='/index.html';
    document.body.classList.toggle('page-home', home);
    document.body.classList.toggle('page-internal', !home);
  }

  function transform(){
    applyPageMode();
    const isHome = document.body.classList.contains('page-home');
    if(!isHome){document.querySelectorAll('.cta-banner').forEach(x=>x.remove());document.body.dataset.r124Home='';return;}
    const header = document.querySelector('.site-header');
    const hero = document.querySelector('.hero');
    if(!header||!hero||document.body.dataset.r124Home==='1')return;
    const headerActions = document.querySelector('.header-actions');
    const searchForm = document.querySelector('.header-search')?.closest('form') || document.querySelector('.header-search');
    const nav = document.querySelector('.main-nav');
    buildUtilityLinks(headerActions);
    enhanceMobileMenu(nav, searchForm && searchForm.tagName==='FORM' ? searchForm : null);
    retitleHero(hero);
    adjustSectionTitles();
    addHomeCta(document.querySelector('main'));
    document.body.dataset.r124Home='1';
  }

  ready(()=>{
    transform();
    if(document.body.classList.contains('page-home')){
      const observer = new MutationObserver(()=>{
        transform();
        if(document.body.dataset.r124Home==='1')observer.disconnect();
      });
      if(document.body.dataset.r124Home!=='1')observer.observe(document.body,{childList:true,subtree:true});
    }
    window.addEventListener('inokskar:navigation',()=>requestAnimationFrame(()=>{document.body.dataset.r124Home='';transform();}));
    window.addEventListener('popstate',()=>requestAnimationFrame(()=>{document.body.dataset.r124Home='';transform();}));
  });
})();


/* source: r125-customer.js */
(()=>{
  async function profile(){
    try{
      const r=await fetch('/api/customer/profile',{cache:'no-store',credentials:'same-origin'});
      if(!r.ok)return null;
      return await r.json();
    }catch{return null;}
  }
  function setValue(form,name,value){const el=form?.querySelector(`[name="${name}"]`);if(el&&value&&!el.value)el.value=value;}
  function addSaveProfile(form){
    if(!form||form.querySelector('[name="saveProfile"]'))return;
    const note=form.querySelector('.muted')||form.querySelector('button[type="submit"]');
    const label=document.createElement('label');
    label.className='customer-save-profile';
    label.innerHTML='<input type="checkbox" name="saveProfile" value="true"><span>Bu talepte değiştirdiğim ad ve telefon bilgilerimi profilime de kaydet</span>';
    if(note)note.insertAdjacentElement('beforebegin',label);else form.append(label);
  }
  async function apply(){
    if(location.pathname!='/iletisim')return;
    const p=await profile();if(!p)return;
    const attempt=()=>{
      const form=document.querySelector('.inquiry-form');
      if(!form)return false;
      setValue(form,'name',p.name);setValue(form,'email',p.email);setValue(form,'phone',p.phone);addSaveProfile(form);return true;
    };
    if(attempt())return;
    const obs=new MutationObserver(()=>{if(attempt())obs.disconnect();});obs.observe(document.body,{childList:true,subtree:true});
    setTimeout(()=>obs.disconnect(),8000);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',apply,{once:true});else apply();
})();


/* source: r126-public.js */
(function(){
  const contactIcon='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.79 19.79 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.12.9.33 1.78.62 2.62a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.46-1.19a2 2 0 0 1 2.11-.45c.84.29 1.72.5 2.62.62A2 2 0 0 1 22 16.92z"></path></svg>';
  const accountIcon='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21a8 8 0 0 0-16 0"></path><circle cx="12" cy="7" r="4"></circle></svg>';
  function classify(){
    const p=location.pathname,home=p==='/'||p==='/index.html';
    document.body.classList.toggle('page-home',home);
    document.body.classList.toggle('page-internal',!home);
    document.body.classList.toggle('page-listing',p==='/urunler'||p.startsWith('/kategori/'));
    document.body.classList.toggle('page-detail',p.startsWith('/urun/'));
    document.body.classList.toggle('page-contact',p==='/iletisim');
    document.body.classList.toggle('page-content',p==='/hakkimizda'||p==='/iletisim');
  }
  function utilities(){
    const actions=document.querySelector('.header-actions');
    if(!actions||actions.querySelector('.header-util-links'))return;
    const util=document.createElement('div');util.className='header-util-links';
    util.innerHTML=`<a href="/iletisim">${contactIcon}<span>Bize Ulaşın</span></a><a href="/hesabim">${accountIcon}<span>Hesabım</span></a>`;
    actions.append(util);
    const cta=document.createElement('a');cta.className='header-cta';cta.href='/iletisim?amac=teklif';cta.textContent='Teklif Al';actions.append(cta);
  }
  function mobile(nav,sourceForm){
    if(!nav||nav.querySelector('.mobile-home-search'))return;
    const wrap=document.createElement('div');wrap.className='mobile-home-search';
    if(sourceForm){
      const clone=sourceForm.cloneNode(true);wrap.append(clone);
      const ci=clone.querySelector('input'),si=sourceForm.querySelector('input');
      if(ci)ci.placeholder='Ürün, kategori veya model ara...';
      ci&&ci.addEventListener('input',()=>{if(si)si.value=ci.value;});
      clone.addEventListener('submit',e=>{e.preventDefault();if(si&&ci)si.value=ci.value;sourceForm.requestSubmit();});
    }
    nav.prepend(wrap);
    const a=document.createElement('a');a.className='mobile-account-link';a.href='/hesabim';a.innerHTML=`${accountIcon}<span>Hesabım / Giriş</span>`;nav.append(a);
  }
  function cleanMenu(){
    document.querySelectorAll('.nav-category-toggle').forEach(b=>{b.setAttribute('tabindex','-1');b.setAttribute('aria-hidden','true');});
    document.querySelectorAll('.nav-dropdown-all svg,.mega-title svg').forEach(x=>x.remove());
  }
  function enhance(){
    classify();
    const isPublic=!location.pathname.startsWith('/admin')&&!location.pathname.startsWith('/teknik')&&!['/login','/kayit','/forgot-password','/hesabim','/hesabim/profil','/account/security'].includes(location.pathname);
    if(!isPublic||document.body.classList.contains('page-home'))return;
    const header=document.querySelector('.site-header');
    if(!header)return;
    const search=document.querySelector('.header-search');
    const form=search?.closest('form');
    const input=search?.querySelector('input');
    if(input)input.placeholder='Ürün, kategori veya model ara...';
    utilities();mobile(document.querySelector('.main-nav'),form);cleanMenu();
    document.body.dataset.r126Public='1';
  }
  const run=()=>{classify();enhance();};
  function start(){
    run();
    if(document.body.classList.contains('page-home')||document.body.dataset.r126Public==='1')return;
    const obs=new MutationObserver(()=>{
      run();
      if(document.body.dataset.r126Public==='1')obs.disconnect();
    });
    obs.observe(document.body,{childList:true,subtree:true});
    window.setTimeout(()=>obs.disconnect(),5000);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
  const rerun=()=>requestAnimationFrame(()=>{document.body.dataset.r126Public='';run();});
  window.addEventListener('inokskar:navigation',rerun);
  window.addEventListener('popstate',rerun);
})();

