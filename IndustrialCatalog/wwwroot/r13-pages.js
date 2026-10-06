/* Page-specific visual scopes; does not alter forms, navigation or requests. */
(()=>{const tabs={'Genel bakış':'overview','Ürünler':'products','Kategoriler':'categories','Fiyat yönetimi':'prices','Site ayarları':'settings','Güvenlik':'security','İstatistikler':'stats','Geçmiş & yedek':'history','Müşteri talepleri':'inquiries'};
function scope(){const p=location.pathname.replace(/\/$/,'')||'/';let key=p==='/'?'home':p.replace(/^\//,'').replace(/\//g,'-');if(p.startsWith('/kategori/'))key='category';if(p.startsWith('/urun/'))key='product';if(p.startsWith('/admin/inquiries/'))key='inquiry';if(p.startsWith('/teknik/cihaz/'))key='device';if(/\/admin\/warranties\/.*\/certificate$/.test(p))key='certificate';if(p==='/admin'){const active=document.querySelector('.admin-sidebar nav button.active');key='admin-'+(tabs[active?.textContent.trim()]||'overview');}if(document.body.dataset.uiPage!==key)document.body.dataset.uiPage=key;}
const start=()=>{scope();new MutationObserver(scope).observe(document.body,{subtree:true,childList:true,attributes:true,attributeFilter:['class']});window.addEventListener('popstate',scope);};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();})();
/* R14: complete shared navigation on server-rendered public pages. */
(()=>{
function refine(){
 const header=document.querySelector('.site-header');if(!header)return;
 header.querySelectorAll('.nav-category-toggle').forEach(b=>{b.removeAttribute('aria-hidden');b.removeAttribute('tabindex');});
 const nav=header.querySelector('.main-nav'),actions=header.querySelector('.header-actions');
 if(document.body.classList.contains('warranty-public-body')&&nav&&actions&&!header.dataset.r14Header){
  header.dataset.r14Header='1';nav.id='primary-menu';nav.setAttribute('aria-label','Ana menü');
  actions.innerHTML='<form class="header-search" role="search" action="/urunler"><input name="q" aria-label="Ürün adı veya koduyla ara" placeholder="Ürün adı veya kodu"><button aria-label="Ürün ara" type="submit"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg></button></form><a class="header-cta" href="/iletisim?amac=teklif">Teklif Al</a><button class="icon-btn mobile-menu" type="button" aria-label="Menüyü aç" aria-controls="primary-menu" aria-expanded="false"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 6h16M4 12h16M4 18h16"/></svg></button>';
  const util=document.createElement('div');util.className='header-util-links';const accountLink=document.createElement('a');accountLink.className='util-link util-account';accountLink.href='/hesabim';accountLink.textContent='Hesabım';util.append(accountLink);actions.append(util);
  const toggle=actions.querySelector('.mobile-menu');toggle.addEventListener('click',()=>{const open=nav.classList.toggle('open');toggle.setAttribute('aria-expanded',String(open));toggle.setAttribute('aria-label',open?'Menüyü kapat':'Menüyü aç');});
  const account=document.createElement('a');account.href='/hesabim';account.textContent='Hesabım / Giriş';account.className='mobile-account-link';nav.append(account);
 }
 if((location.pathname==='/urunler'||location.pathname.startsWith('/kategori/'))&&!document.querySelector('.page-intro')&&document.querySelector('.catalogue')){
  const path=location.pathname;const name=path==='/urunler'?'Ürün koleksiyonu':([...nav.querySelectorAll('a')].find(a=>a.getAttribute('href')===path)?.textContent.trim()||'Ürün kategorisi');
  const intro=document.createElement('section');intro.className='wrap page-intro';const crumb=document.createElement('div');crumb.className='breadcrumb';const home=document.createElement('a');home.href='/';home.textContent='Ana sayfa';crumb.append(home,document.createTextNode(' / '+name));const title=document.createElement('h1');title.textContent=name;const desc=document.createElement('p');desc.textContent='İhtiyacınıza uygun ekipmanları ürün adı veya koduyla keşfedin.';intro.append(crumb,title,desc);document.querySelector('.catalogue').before(intro);
 }
}
function start(){refine();const o=new MutationObserver(refine);o.observe(document.body,{childList:true,subtree:true});}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
/* Legacy visual scripts change React-owned nodes. Use a clean document for
   global search navigation so those changes cannot break reconciliation. */
document.addEventListener('submit',event=>{
 const form=event.target;if(!(form instanceof HTMLFormElement)||!form.matches('.site-header .header-search')||!form.closest('#root'))return;
 const input=form.querySelector('input');if(!input)return;
 event.preventDefault();event.stopImmediatePropagation();const q=input.value.trim();location.assign('/urunler'+(q?'?q='+encodeURIComponent(q):''));
},true);


/* R14.8 — compact admin mobile drawer */
(()=>{
  if(!location.pathname.startsWith('/admin'))return;
  const mount=()=>{
    if(location.pathname==='/admin/security'){
      const bar=document.querySelector('.bar');if(bar){bar.classList.add('security-top');}
    }
    const side=document.querySelector('.admin-sidebar');
    const nav=side?.querySelector('nav');
    if(!side||!nav||side.dataset.r148Drawer)return;
    side.dataset.r148Drawer='1';
    const toggle=document.createElement('button');
    toggle.type='button';toggle.className='admin-mobile-toggle';toggle.setAttribute('aria-label','Yönetim menüsünü aç');toggle.setAttribute('aria-expanded','false');
    toggle.innerHTML='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 6h16M4 12h16M4 18h16"/></svg>';
    side.insertBefore(toggle,nav);
    const close=()=>{side.classList.remove('menu-open');toggle.setAttribute('aria-expanded','false');toggle.setAttribute('aria-label','Yönetim menüsünü aç')};
    toggle.addEventListener('click',()=>{const open=side.classList.toggle('menu-open');toggle.setAttribute('aria-expanded',String(open));toggle.setAttribute('aria-label',open?'Yönetim menüsünü kapat':'Yönetim menüsünü aç')});
    nav.addEventListener('click',e=>{if(innerWidth<=850&&e.target.closest('button,a'))close()});
    document.addEventListener('keydown',e=>{if(e.key==='Escape')close()});
  };
  const start=()=>{mount();const observer=new MutationObserver(mount);observer.observe(document.body,{childList:true,subtree:true});setTimeout(()=>observer.disconnect(),30000)};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();


/* R15.1 — public mobile/tablet application shell */
(()=>{
  const path=()=>location.pathname.replace(/\/$/,'')||'/';
  const excluded=()=>/^(\/admin(?:\/|$)|\/teknik(?:\/|$)|\/login$|\/kayit$|\/forgot-password$|\/reset-password$|\/account\/security$|\/api(?:\/|$))/.test(path())||/\/certificate$/.test(path());
  const icon=name=>({
    home:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="m3 11 9-8 9 8"/><path d="M5 10v10h14V10"/><path d="M9 20v-6h6v6"/></svg>',
    products:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="3" width="16" height="18" rx="2"/><path d="M8 7h8M8 11h8M8 15h5"/></svg>',
    categories:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>',
    service:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14.7 6.3a4 4 0 0 0-5 5L3 18l3 3 6.7-6.7a4 4 0 0 0 5-5l-2.2 2.2-3-3z"/></svg>',
    account:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>',
    search:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg>',
    close:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 6l12 12M18 6 6 18"/></svg>'
  }[name]||'');
  const activeKey=()=>{
    const p=path();
    if(p==='/')return'home';
    if(p==='/urunler'||p.startsWith('/urun/'))return'products';
    if(p.startsWith('/kategori/'))return'categories';
    if(p==='/servis-talebi'||p==='/garanti-sorgulama'||p==='/iletisim')return'service';
    if(p.startsWith('/hesabim'))return'account';
    return'';
  };
  const uniqueCategories=()=>{
    const seen=new Set(),rows=[];
    document.querySelectorAll('a[href^="/kategori/"]').forEach(a=>{
      const href=a.getAttribute('href')||'',label=(a.textContent||'').trim();
      if(!href||!label||seen.has(href))return;seen.add(href);rows.push({href,label});
    });
    return rows.slice(0,18);
  };
  const mountSheet=()=>{
    let overlay=document.querySelector('.app-category-overlay');
    if(overlay)return overlay;
    overlay=document.createElement('div');overlay.className='app-category-overlay';overlay.hidden=true;
    overlay.innerHTML='<div class="app-category-sheet" role="dialog" aria-modal="true" aria-label="Ürün kategorileri"><div class="app-sheet-handle"></div><div class="app-sheet-head"><div><small>ÜRÜN KATALOĞU</small><strong>Kategoriler</strong></div><button class="app-sheet-close" type="button" aria-label="Kapat">'+icon('close')+'</button></div><div class="app-category-list"></div><a class="app-all-products" href="/urunler">Tüm ürünleri görüntüle <span>→</span></a></div>';
    document.body.append(overlay);
    const close=()=>{overlay.hidden=true;document.body.classList.remove('app-sheet-open')};
    overlay.addEventListener('click',e=>{if(e.target===overlay)close()});
    overlay.querySelector('.app-sheet-close').addEventListener('click',close);
    document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!overlay.hidden)close()});
    overlay._open=()=>{
      const list=overlay.querySelector('.app-category-list'),cats=uniqueCategories();
      list.innerHTML=cats.length?cats.map(x=>'<a href="'+x.href+'"><span>'+x.label+'</span><b>›</b></a>').join(''):'<p class="app-sheet-empty">Kategoriler yükleniyor…</p>';
      overlay.hidden=false;document.body.classList.add('app-sheet-open');
    };
    return overlay;
  };
  const mountNav=()=>{
    if(document.querySelector('.app-bottom-nav'))return;
    const active=activeKey(),nav=document.createElement('nav');nav.className='app-bottom-nav';nav.setAttribute('aria-label','Mobil uygulama navigasyonu');
    const items=[
      ['home','/','Ana Sayfa','home'],
      ['products','/urunler','Ürünler','products'],
      ['categories','#','Kategoriler','categories'],
      ['service','/servis-talebi','Servis','service'],
      ['account','/hesabim','Hesabım','account']
    ];
    nav.innerHTML=items.map(([key,href,label,ic])=>key==='categories'
      ?'<button type="button" class="app-bottom-item '+(active===key?'active':'')+'" data-app-categories>'+icon(ic)+'<span>'+label+'</span></button>'
      :'<a class="app-bottom-item '+(active===key?'active':'')+'" href="'+href+'">'+icon(ic)+'<span>'+label+'</span></a>').join('');
    document.body.append(nav);
    nav.querySelector('[data-app-categories]').addEventListener('click',()=>mountSheet()._open());
  };
  const mountSearch=()=>{
    const p=path(),eligible=p==='/'||p==='/urunler'||p.startsWith('/kategori/');
    const old=document.querySelector('.app-mobile-search');
    if(!eligible){old?.remove();return}
    if(old)return;
    const header=document.querySelector('.site-header');if(!header)return;
    const form=document.createElement('form');form.className='app-mobile-search';form.action='/urunler';form.method='get';form.setAttribute('role','search');
    const q=new URLSearchParams(location.search).get('q')||'';
    form.innerHTML='<div class="app-search-field">'+icon('search')+'<input name="q" value="'+q.replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]))+'" placeholder="Ürün adı veya koduyla ara" aria-label="Ürün adı veya koduyla ara"><button type="submit">Ara</button></div>';
    header.insertAdjacentElement('afterend',form);
  };
  const mount=()=>{
    if(excluded()){document.body.classList.remove('app-shell-enabled');document.querySelector('.app-bottom-nav')?.remove();document.querySelector('.app-mobile-search')?.remove();return}
    document.body.classList.add('app-shell-enabled');
    mountNav();mountSearch();
  };
  const start=()=>{mount();const o=new MutationObserver(()=>{if(!excluded()){mountSearch();}});o.observe(document.body,{childList:true,subtree:true});window.addEventListener('popstate',mount)};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();


/* R15.8 — unified private/customer brand headers */
(()=>{
  const icon='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 6h16M4 12h16M4 18h16"/></svg>';
  const brandHtml='<img class="private-brand-image" src="/inokskar-header-brand.png" alt="İNOKSKAR Soğutma ve Endüstriyel Mutfak">';
  const setupHeader=header=>{
    if(!header||header.dataset.r158Brand)return;
    header.dataset.r158Brand='1';header.classList.add('private-brand-header');
    let brand=header.querySelector(':scope>.private-brand-link')||(header.matches('header.top')?header.querySelector(':scope>a[href="/"]'):header.querySelector(':scope>div'));
    if(brand&&brand.tagName!=='A'){
      const link=document.createElement('a');link.href='/';link.className='private-brand-link';link.innerHTML=brandHtml;brand.replaceWith(link);brand=link;
    }else if(brand){brand.classList.add('private-brand-link');brand.innerHTML=brandHtml;}
    if(!brand){
      const oldStrong=header.querySelector(':scope>strong');if(oldStrong)oldStrong.remove();
      brand=document.createElement('a');brand.href='/';brand.className='private-brand-link';brand.innerHTML=brandHtml;header.insertBefore(brand,header.firstChild);
    }
    let nav=header.querySelector(':scope>nav');
    if(!nav){
      nav=document.createElement('nav');
      [...header.children].filter(x=>x!==brand&&!x.classList.contains('private-header-toggle')).forEach(x=>nav.append(x));
      header.append(nav);
    }
    if(nav&&!header.querySelector('.private-header-toggle')){
      const toggle=document.createElement('button');toggle.type='button';toggle.className='private-header-toggle';toggle.setAttribute('aria-label','Menüyü aç');toggle.setAttribute('aria-expanded','false');toggle.innerHTML=icon;
      header.insertBefore(toggle,nav);
      const close=()=>{header.classList.remove('menu-open');toggle.setAttribute('aria-expanded','false');toggle.setAttribute('aria-label','Menüyü aç')};
      toggle.addEventListener('click',()=>{const open=header.classList.toggle('menu-open');toggle.setAttribute('aria-expanded',String(open));toggle.setAttribute('aria-label',open?'Menüyü kapat':'Menüyü aç')});
      nav.addEventListener('click',e=>{if(innerWidth<=850&&e.target.closest('a,button'))close()});
      document.addEventListener('keydown',e=>{if(e.key==='Escape')close()});
    }
  };
  const mount=()=>{
    const side=document.querySelector('.admin-sidebar');
    const sideBrand=side?.querySelector(':scope>.brand');
    if(sideBrand&&!sideBrand.dataset.r158Brand){sideBrand.dataset.r158Brand='1';sideBrand.classList.add('private-brand-link');sideBrand.innerHTML=brandHtml;}
    document.querySelectorAll('header.top,header.access-top,header.tech-top,header.warranty-admin-top,header.mail-top,header.inquiry-top,.security-top').forEach(setupHeader);
  };
  const start=()=>{mount();const o=new MutationObserver(mount);o.observe(document.body,{childList:true,subtree:true});setTimeout(()=>o.disconnect(),30000)};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();


/* R15.13 — admin phone/tablet application dock */
(()=>{
  if(location.pathname!=='/admin')return;
  const icon=name=>({
    overview:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>',
    products:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 5h16v14H4z"/><path d="M8 9h8M8 13h5"/></svg>',
    prices:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><path d="M15 8.5c-.7-.5-1.5-.8-2.5-.8-1.4 0-2.5.7-2.5 1.8 0 2.8 5.5 1.2 5.5 4 0 1.2-1.1 2-2.7 2-.9 0-1.9-.3-2.8-.9M12.5 6v12"/></svg>',
    inquiries:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 5h16v11H8l-4 4z"/><path d="M8 9h8M8 12h5"/></svg>',
    menu:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 6h16M4 12h16M4 18h16"/></svg>'
  }[name]||'');
  const mount=()=>{
    const side=document.querySelector('.admin-sidebar'),nav=side?.querySelector('nav');
    if(!side||!nav||document.querySelector('.admin-app-dock'))return;
    const find=label=>[...nav.querySelectorAll('button')].find(b=>(b.textContent||'').trim()===label);
    const refs={
      overview:find('Genel bakış'),
      products:find('Ürünler'),
      prices:find('Fiyat yönetimi'),
      inquiries:find('Müşteri talepleri')
    };
    if(Object.values(refs).some(x=>!x))return;
    const dock=document.createElement('nav');dock.className='admin-app-dock';dock.setAttribute('aria-label','Yönetim uygulama navigasyonu');
    const rows=[['overview','Genel'],['products','Ürünler'],['prices','Fiyatlar'],['inquiries','Talepler'],['menu','Menü']];
    dock.innerHTML=rows.map(([key,label])=>'<button type="button" class="admin-app-item" data-admin-dock="'+key+'">'+icon(key)+'<span>'+label+'</span></button>').join('');
    document.body.append(dock);
    const sync=()=>{
      for(const [key,ref] of Object.entries(refs)){
        dock.querySelector('[data-admin-dock="'+key+'"]')?.classList.toggle('active',ref.classList.contains('active'));
      }
      dock.querySelector('[data-admin-dock="menu"]')?.classList.toggle('active',side.classList.contains('menu-open'));
    };
    dock.addEventListener('click',e=>{
      const btn=e.target.closest('[data-admin-dock]');if(!btn)return;
      const key=btn.dataset.adminDock;
      if(key==='menu'){side.querySelector('.admin-mobile-toggle')?.click();sync();return;}
      refs[key]?.click();side.classList.remove('menu-open');window.scrollTo({top:0,behavior:'smooth'});sync();
    });
    new MutationObserver(sync).observe(side,{subtree:true,attributes:true,attributeFilter:['class']});
    sync();
  };
  const start=()=>{mount();const o=new MutationObserver(mount);o.observe(document.body,{subtree:true,childList:true});setTimeout(()=>o.disconnect(),30000)};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
