/* Page-specific visual scopes; does not alter forms, navigation or requests. */
(()=>{const tabs={'Genel bakış':'overview','Ürünler':'products','Kategoriler':'categories','Fiyat yönetimi':'prices','Site ayarları':'settings','Güvenlik':'security','İstatistikler':'stats','Geçmiş & yedek':'history','Müşteri talepleri':'inquiries'};
function scope(){const p=location.pathname.replace(/\/$/,'')||'/';let key=p==='/'?'home':p.replace(/^\//,'').replace(/\//g,'-');if(p.startsWith('/kategori/'))key='category';if(p.startsWith('/urun/'))key='product';if(p.startsWith('/hesabim'))key='hesabim';if(p.startsWith('/admin/inquiries/'))key='inquiry';if(p.startsWith('/teknik/cihaz/'))key='device';if(/\/admin\/warranties\/.*\/certificate$/.test(p))key='certificate';if(p==='/admin'){const active=document.querySelector('.admin-sidebar nav button.active');key='admin-'+(tabs[active?.textContent.trim()]||'overview');}if(document.body.dataset.uiPage!==key)document.body.dataset.uiPage=key;}
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
  const intro=document.createElement('section');intro.className='wrap page-intro';const crumb=document.createElement('div');crumb.className='breadcrumb';const home=document.createElement('a');home.href='/';home.textContent='Ana sayfa';crumb.append(home,document.createTextNode(' / '+name));const title=document.createElement('h1');title.textContent=name;intro.append(crumb,title);document.querySelector('.catalogue').before(intro);
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
    nav.addEventListener('click',e=>{if(innerWidth<=1100&&e.target.closest('button,a'))close()});
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
    document.querySelectorAll('.private-brand-header').forEach(setupHeader);
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


/* R15.14 — admin subpage mobile/tablet dock */
(()=>{
  const p=location.pathname.replace(/\/$/,'')||'/';
  if(!p.startsWith('/admin/')||p==='/admin/security'&&document.body.classList.contains('r126-auth'))return;
  const icon=name=>({
    panel:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>',
    warranty:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 3 5 6v5c0 4.5 2.9 8 7 10 4.1-2 7-5.5 7-10V6z"/><path d="m9 12 2 2 4-5"/></svg>',
    technical:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14.7 6.3a4 4 0 0 0-5 5L3 18l3 3 6.7-6.7a4 4 0 0 0 5-5l-2.2 2.2-3-3z"/></svg>',
    users:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2.4"/><path d="M3 20a6 6 0 0 1 12 0M14 20a5 5 0 0 1 7-4.6"/></svg>',
    system:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6V21h-4v-.1a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1L4.2 17l.1-.1a1.7 1.7 0 0 0 .3-1.9A1.7 1.7 0 0 0 3 14H3v-4h.1a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.3-1.9L4.2 7 7 4.2l.1.1a1.7 1.7 0 0 0 1.9.3 1.7 1.7 0 0 0 1-1.6V3h4v.1a1.7 1.7 0 0 0 1 1.6 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.8 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.6 1h.1v4H21a1.7 1.7 0 0 0-1.6 1z"/></svg>'
  }[name]||'');
  const rows=[
    ['panel','/admin','Panel'],
    ['warranty','/admin/warranties','Garanti'],
    ['technical','/teknik','Teknik'],
    ['users','/admin/users','Kullanıcı'],
    ['system','/admin/system','Sistem']
  ];
  const active=key=>{
    if(key==='warranty')return p.startsWith('/admin/warranties');
    if(key==='users')return p==='/admin/users';
    if(key==='system')return ['/admin/system','/admin/mail','/admin/header-brand'].some(x=>p===x);
    return false;
  };
  const mount=()=>{
    if(document.querySelector('.private-app-dock'))return;
    const dock=document.createElement('nav');dock.className='private-app-dock';dock.setAttribute('aria-label','Yönetim hızlı erişim');
    dock.innerHTML=rows.map(([key,href,label])=>'<a class="private-app-item '+(active(key)?'active':'')+'" href="'+href+'">'+icon(key)+'<span>'+label+'</span></a>').join('');
    document.body.append(dock);document.body.classList.add('has-private-app-dock');
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();


/* R16 — central overlay shield: prevent click-through and keep focus inside open layers. */
(()=>{
  let shield=null,lastFocus=null,inerted=[];
  const focusables=root=>root?[...root.querySelectorAll('a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])')].filter(x=>!x.hidden&&x.getClientRects().length):[];
  const activeLayer=()=>{
    const admin=document.querySelector('.admin-sidebar.menu-open nav');if(admin)return {panel:admin,kind:'admin',close:()=>document.querySelector('.admin-sidebar .admin-mobile-toggle')?.click()};
    const priv=document.querySelector('.private-brand-header.menu-open>nav');if(priv)return {panel:priv,kind:'private',close:()=>document.querySelector('.private-brand-header>.private-header-toggle')?.click()};
    const pub=[...document.querySelectorAll('.site-header .main-nav.open')].find(x=>!x.closest('#root'));if(pub)return {panel:pub,kind:'public',close:()=>document.querySelector('.site-header .mobile-menu')?.click()};
    return null;
  };
  const setBackgroundInert=(on,layer)=>{
    if(!on){inerted.forEach(el=>el.removeAttribute('inert'));inerted=[];return;}
    const targets=layer?.kind==='admin'?[document.querySelector('.admin-main')]:[document.querySelector('main'),document.querySelector('.site-footer'),document.querySelector('.public-home-return-wrap')];
    inerted=targets.filter(Boolean);inerted.forEach(el=>el.setAttribute('inert',''));
  };
  const sync=()=>{
    const layer=activeLayer();
    if(layer&&!shield){
      lastFocus=document.activeElement;shield=document.createElement('button');shield.type='button';shield.className='ui-interaction-shield '+layer.kind+'-shield';shield.setAttribute('aria-label','Açık menüyü kapat');
      shield.addEventListener('pointerdown',e=>{e.preventDefault();e.stopPropagation();e.stopImmediatePropagation();activeLayer()?.close();},{capture:true});
      document.body.append(shield);document.body.classList.add('ui-layer-locked');setBackgroundInert(true,layer);
      requestAnimationFrame(()=>focusables(layer.panel)[0]?.focus({preventScroll:true}));
    }else if(!layer&&shield){
      shield.remove();shield=null;document.body.classList.remove('ui-layer-locked');setBackgroundInert(false,null);if(lastFocus instanceof HTMLElement)lastFocus.focus({preventScroll:true});lastFocus=null;
    }else if(layer&&shield){shield.className='ui-interaction-shield '+layer.kind+'-shield';}
  };
  document.addEventListener('keydown',e=>{
    const layer=activeLayer();if(!layer)return;
    if(e.key==='Escape'){e.preventDefault();layer.close();return;}
    if(e.key!=='Tab')return;const items=focusables(layer.panel);if(!items.length)return;const first=items[0],last=items[items.length-1];
    if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}
  },true);
  const start=()=>{sync();new MutationObserver(sync).observe(document.body,{subtree:true,attributes:true,attributeFilter:['class','hidden'],childList:true});};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();

/* R16 — customer account: profile / requests / security tabs and compact summary. */
(()=>{
  if(!location.pathname.startsWith('/hesabim/profil'))return;
  const mount=()=>{
    const layout=document.querySelector('.layout');if(!layout||layout.dataset.r16Tabs)return;const cards=[...layout.querySelectorAll(':scope>.card')];if(cards.length<2)return;layout.dataset.r16Tabs='1';
    const profile=cards[0],requests=cards[1],security=profile.querySelector('.security');
    const securityPanel=document.createElement('section');securityPanel.className='card customer-tab-panel';securityPanel.innerHTML='<h2>Hesap güvenliği</h2><p class="muted">Şifrenizi ve güvenli hesap erişiminizi buradan yönetin.</p>';
    if(security)securityPanel.append(security);layout.append(securityPanel);
    [profile,requests,securityPanel].forEach((x,i)=>{x.classList.add('customer-tab-panel');x.dataset.customerTab=['profile','requests','security'][i];});
    const tabs=document.createElement('nav');tabs.className='customer-account-tabs';tabs.setAttribute('aria-label','Hesabım bölümleri');tabs.innerHTML='<button type="button" data-tab="profile">Profil</button><button type="button" data-tab="requests">Taleplerim</button><button type="button" data-tab="security">Güvenlik</button>';
    const welcome=document.querySelector('.welcome');
    const readonly=profile.querySelector('input[readonly]'),name=profile.querySelector('input[name="name"]');
    const summary=document.createElement('section');summary.className='customer-profile-summary';const initials=(name?.value||'İ').trim().split(/\s+/).slice(0,2).map(x=>x[0]||'').join('').toLocaleUpperCase('tr-TR');
    summary.innerHTML='<span class="avatar">'+(initials||'İ')+'</span><div><strong>'+String(name?.value||'Müşteri').replace(/[&<>]/g,'')+'</strong><small>'+String(readonly?.value||'').replace(/[&<>]/g,'')+'</small></div><a href="/servis-talebi">Yeni servis talebi</a>';
    welcome?.insertAdjacentElement('afterend',summary);summary.insertAdjacentElement('afterend',tabs);
    const select=key=>{tabs.querySelectorAll('button').forEach(b=>b.classList.toggle('active',b.dataset.tab===key));layout.querySelectorAll('.customer-tab-panel').forEach(p=>p.hidden=p.dataset.customerTab!==key);};
    tabs.addEventListener('click',e=>{const b=e.target.closest('button[data-tab]');if(b)select(b.dataset.tab)});select('profile');
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — service request: three-step mobile-friendly wizard without changing POST payload. */
(()=>{
  if(location.pathname!='/servis-talebi')return;
  const mount=()=>{
    const form=document.getElementById('service-request-form');if(!form||form.dataset.r16Wizard)return;form.dataset.r16Wizard='1';
    const children=[...form.children],product=form.querySelector('.service-product-box'),message=[...form.querySelectorAll('label')].find(l=>l.querySelector('textarea[name="message"]'));
    if(!product||!message)return;
    const step1=document.createElement('section'),step2=document.createElement('section'),step3=document.createElement('section');[step1,step2,step3].forEach((x,i)=>{x.className='service-step-panel';x.dataset.step=String(i+1)});
    children.forEach(node=>{if(node===product)step2.append(node);else if(node===message||node.matches?.('.service-honeypot,.service-note,.service-save-profile,#service-request-message')||node.tagName==='BUTTON')step3.append(node);else step1.append(node)});
    form.append(step1,step2,step3);
    const hint=document.createElement('div');hint.className='service-scan-hint';hint.innerHTML='<strong>İpucu:</strong><span>Ürün kodu veya seri numarası cihaz etiketinde yer alır. Garanti sorgulamasından geldiyseniz bu alanlar otomatik doldurulur.</span>';step2.insertBefore(hint,step2.firstChild);
    const stepper=document.createElement('nav');stepper.className='service-stepper';stepper.setAttribute('aria-label','Servis talebi adımları');stepper.innerHTML='<button type="button" data-step="1"><b>1</b><span>İletişim</span></button><button type="button" data-step="2"><b>2</b><span>Cihaz</span></button><button type="button" data-step="3"><b>3</b><span>Arıza</span></button>';form.before(stepper);
    const actions=document.createElement('div');actions.className='service-step-actions';actions.innerHTML='<button type="button" class="back">← Geri</button><button type="button" class="next">Devam →</button>';form.append(actions);let current=1;
    const validate=()=>{for(const el of [...form.querySelectorAll('.service-step-panel[data-step="'+current+'"] input[required],.service-step-panel[data-step="'+current+'"] textarea[required],.service-step-panel[data-step="'+current+'"] select[required]')]){if(!el.checkValidity()){el.reportValidity();el.focus();return false;}}return true;};
    const show=n=>{current=Math.max(1,Math.min(3,n));form.querySelectorAll('.service-step-panel').forEach(p=>p.hidden=Number(p.dataset.step)!==current);stepper.querySelectorAll('button').forEach(b=>b.classList.toggle('active',Number(b.dataset.step)===current));actions.querySelector('.back').hidden=current===1;actions.querySelector('.next').hidden=current===3;window.scrollTo({top:Math.max(0,form.getBoundingClientRect().top+scrollY-150),behavior:'smooth'});};
    stepper.addEventListener('click',e=>{const b=e.target.closest('button[data-step]');if(!b)return;const n=Number(b.dataset.step);if(n<current||validate())show(n)});actions.querySelector('.back').addEventListener('click',()=>show(current-1));actions.querySelector('.next').addEventListener('click',()=>{if(validate())show(current+1)});form.addEventListener('submit',e=>{if(current!==3){e.preventDefault();e.stopImmediatePropagation();if(validate())show(current+1);}});show(1);
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — warranty query help for locating serial/verification information. */
(()=>{
  if(location.pathname!='/garanti-sorgulama')return;
  const mount=()=>{const form=document.getElementById('warranty-query-form');if(!form||form.dataset.r16Help)return;form.dataset.r16Help='1';const button=document.createElement('button');button.type='button';button.className='warranty-help-trigger';button.textContent='Bu bilgiler nerede?';const hint=document.createElement('div');hint.className='warranty-label-hint';hint.hidden=true;hint.innerHTML='<strong>Seri numarası:</strong><span>Cihazın ürün etiketinde bulunur. Garanti doğrulama kodu ise size verilen garanti belgesinde yer alır. Güvenlik nedeniyle yalnız seri numarasıyla sorgulama yapılamaz.</span>';form.querySelector('label:last-of-type')?.after(button,hint);button.addEventListener('click',()=>{hint.hidden=!hint.hidden;button.textContent=hint.hidden?'Bu bilgiler nerede?':'Yardımı kapat'});};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — registration: password feedback and compact optional fields on phones. */
(()=>{
  if(location.pathname!='/kayit')return;
  const mount=()=>{const form=document.querySelector('form[action="/kayit"]');if(!form||form.dataset.r16Register)return;form.dataset.r16Register='1';const pass=form.querySelector('input[name="password"]'),confirm=form.querySelector('input[name="confirm"]');if(pass){const meter=document.createElement('small');meter.className='r16-password-meter';pass.closest('label')?.append(meter);const update=()=>{const v=pass.value;let score=(v.length>=8?1:0)+(/[A-ZÇĞİÖŞÜ]/.test(v)?1:0)+(/[a-zçğıöşü]/.test(v)?1:0)+(/\d/.test(v)?1:0)+(/[^\w\s]/.test(v)?1:0);meter.textContent=!v?'En az 8 karakter kullanın.':score<=2?'Şifre gücü: zayıf':score<=4?'Şifre gücü: iyi':'Şifre gücü: güçlü';meter.dataset.strength=String(score);if(confirm?.value)confirm.setCustomValidity(confirm.value===v?'':'Şifre tekrarı aynı olmalıdır.');};pass.addEventListener('input',update);confirm?.addEventListener('input',update);update();}
    if(matchMedia('(max-width:600px)').matches){const optional=[form.querySelector('input[name="businessName"]')?.closest('label'),form.querySelector('input[name="phone"]')?.closest('label')].filter(Boolean);optional.forEach(x=>{x.classList.add('r16-optional-field');x.hidden=true;});const toggle=document.createElement('button');toggle.type='button';toggle.className='r16-optional-toggle';toggle.textContent='Firma ve telefon bilgisi ekle';form.querySelector('.two')?.after(toggle);toggle.addEventListener('click',()=>{const open=form.classList.toggle('show-optional');optional.forEach(x=>x.hidden=!open);toggle.textContent=open?'İsteğe bağlı bilgileri gizle':'Firma ve telefon bilgisi ekle'});}
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — password recovery helper text. */
(()=>{
  if(!['/forgot-password','/reset-password'].includes(location.pathname))return;
  const mount=()=>{const card=document.querySelector('main.card');if(!card||card.querySelector('.r16-recovery-help'))return;const p=document.createElement('p');p.className='r16-recovery-help';p.textContent=location.pathname==='/forgot-password'?'E-posta birkaç dakika içinde görünmezse spam / gereksiz klasörünü de kontrol edin. Güvenlik nedeniyle hesabın sistemde kayıtlı olup olmadığı açıklanmaz.':'Yeni şifrenizi başka hesaplarda kullanmadığınız, size özel bir şifre olarak belirleyin.';card.querySelector('form')?.after(p);};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — access management: one primary add button, role chosen after tap. */
(()=>{
  if(location.pathname!='/admin/users')return;
  const mount=()=>{const actions=document.querySelector('.access-actions'),admin=document.getElementById('add-admin'),tech=document.getElementById('add-tech');if(!actions||!admin||!tech||actions.dataset.r16Add)return;actions.dataset.r16Add='1';admin.hidden=true;tech.hidden=true;const wrap=document.createElement('div');wrap.className='access-add-menu';wrap.innerHTML='<button type="button" class="button" aria-expanded="false">+ Kullanıcı ekle</button><div class="access-add-popover" hidden><button type="button" data-role="admin">Admin hesabı</button><button type="button" data-role="tech">Teknik servis hesabı</button></div>';actions.prepend(wrap);const main=wrap.firstElementChild,pop=wrap.lastElementChild;const close=()=>{pop.hidden=true;main.setAttribute('aria-expanded','false')};main.addEventListener('click',()=>{pop.hidden=!pop.hidden;main.setAttribute('aria-expanded',String(!pop.hidden))});pop.addEventListener('click',e=>{const b=e.target.closest('button[data-role]');if(!b)return;(b.dataset.role==='admin'?admin:tech).click();close()});document.addEventListener('pointerdown',e=>{if(!pop.hidden&&!wrap.contains(e.target)){e.preventDefault();e.stopPropagation();e.stopImmediatePropagation();close()}},{capture:true});};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — mail settings: visible task progression and collapsible technical sections. */
(()=>{
  if(location.pathname!='/admin/mail')return;
  const mount=()=>{const form=document.getElementById('mail-settings-form'),card=form?.closest('.mail-card');if(!form||!card||card.dataset.r16Mail)return;card.dataset.r16Mail='1';const progress=document.createElement('div');progress.className='mail-progress';progress.innerHTML='<span class="current">1 · Sağlayıcı</span><span>2 · SMTP</span><span>3 · Gönderici</span><span>4 · Test</span>';card.prepend(progress);const sections=[...form.querySelectorAll('.mail-section')];sections.forEach((section,i)=>{const heading=section.querySelector('h2')?.textContent||'Ayar bölümü';const toggle=document.createElement('button');toggle.type='button';toggle.className='mail-section-toggle';toggle.innerHTML='<strong>'+heading+'</strong><span>−</span>';section.prepend(toggle);toggle.addEventListener('click',()=>{section.classList.toggle('collapsed');toggle.lastElementChild.textContent=section.classList.contains('collapsed')?'+':'−'});if(innerWidth<=600&&i>0){section.classList.add('collapsed');toggle.lastElementChild.textContent='+';}});const health=document.getElementById('mail-health');if(health)new MutationObserver(()=>{const ok=/hazır/i.test(health.textContent||'');progress.children[0].classList.toggle('ready',!!form.elements.provider?.value);progress.children[1].classList.toggle('ready',ok);progress.children[2].classList.toggle('ready',Boolean(form.elements.fromAddress?.value));}).observe(health,{childList:true,subtree:true,characterData:true});};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — branding: show recommended geometry and desktop/phone previews. */
(()=>{
  if(location.pathname!='/admin/header-brand')return;
  const mount=()=>{const card=document.querySelector('.brand-admin-card'),preview=document.getElementById('brand-preview');if(!card||!preview||card.dataset.r16Preview)return;card.dataset.r16Preview='1';const meta=document.createElement('div');meta.className='brand-preview-meta';meta.innerHTML='<span>PNG / WebP önerilir</span><span>Yatay logo oranı</span><span>Şeffaf zemin uygundur</span>';const devices=document.createElement('div');devices.className='brand-live-devices';devices.innerHTML='<div class="brand-device"><small>Masaüstü üst başlık</small><img alt="Masaüstü logo önizleme"></div><div class="brand-device"><small>Mobil üst başlık</small><img alt="Mobil logo önizleme"></div>';card.append(meta,devices);const sync=()=>devices.querySelectorAll('img').forEach(i=>i.src=preview.src);new MutationObserver(sync).observe(preview,{attributes:true,attributeFilter:['src']});preview.addEventListener('load',sync);sync();};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16.3 — eliminate duplicate page-size controls even when React renders them after DOMContentLoaded. */
(()=>{const clean=()=>{const controls=[...document.querySelectorAll('.filterbar .page-size-select')];if(controls.length>1)controls.slice(1).forEach(x=>x.remove());};const start=()=>{clean();requestAnimationFrame(clean);setTimeout(clean,250);setTimeout(clean,900);const root=document.getElementById('root')||document.body;new MutationObserver(clean).observe(root,{subtree:true,childList:true});};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();})();
