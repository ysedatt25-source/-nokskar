/* Page-specific visual scopes; does not alter forms, navigation or requests. */
(()=>{const tabs={'Genel bakış':'overview','Ürünler':'products','Kategoriler':'categories','Fiyat yönetimi':'prices','Site ayarları':'settings','Güvenlik':'security','İstatistikler':'stats','Geçmiş & yedek':'history','Müşteri talepleri':'inquiries','Referanslar':'references'};
function scope(){const p=location.pathname.replace(/\/$/,'')||'/';let key=p==='/'?'home':p.replace(/^\//,'').replace(/\//g,'-');if(p.startsWith('/kategori/'))key='category';if(p.startsWith('/urun/'))key='product';if(p.startsWith('/hesabim'))key='hesabim';if(p.startsWith('/admin/inquiries/'))key='inquiry';if(p.startsWith('/teknik/cihaz/'))key='device';if(/\/admin\/warranties\/.*\/certificate$/.test(p))key='certificate';if(p==='/admin'){const active=document.querySelector('.admin-sidebar nav button.active');const requested=new URLSearchParams(location.search).get('tab');const fromUrl=Object.values(tabs).includes(requested)?requested:null;key='admin-'+(fromUrl||tabs[active?.textContent.trim()]||'overview');}if(document.body.dataset.uiPage!==key)document.body.dataset.uiPage=key;}
const start=()=>{scope();window.addEventListener('popstate',scope);window.addEventListener('pageshow',scope);window.addEventListener('inokskar:navigation',scope);document.addEventListener('click',()=>requestAnimationFrame(scope),true);};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();})();
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
function start(){refine();window.addEventListener('pageshow',refine);window.addEventListener('inokskar:navigation',refine);}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
/* Legacy visual scripts change React-owned nodes. Use a clean document for
   global search navigation so those changes cannot break reconciliation. */
document.addEventListener('submit',event=>{
 const form=event.target;if(!(form instanceof HTMLFormElement)||!form.matches('.site-header .header-search')||!form.closest('#root'))return;
 const input=form.querySelector('input');if(!input)return;
 event.preventDefault();event.stopImmediatePropagation();const q=input.value.trim();location.assign('/urunler'+(q?'?q='+encodeURIComponent(q):''));
},true);


/* R14.8 / R18 — compact, interaction-safe admin mobile drawer */
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
    const sheetHead=document.createElement('div');
    sheetHead.className='admin-menu-sheet-head';
    sheetHead.innerHTML='<div><span>İNOKSKAR</span><strong>Yönetim Menüsü</strong></div><button type="button" class="admin-menu-sheet-close" aria-label="Yönetim menüsünü kapat"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg></button>';
    nav.prepend(sheetHead);
    const backdrop=document.createElement('button');
    backdrop.type='button';backdrop.className='admin-drawer-backdrop';backdrop.setAttribute('aria-label','Yönetim menüsünü kapat');backdrop.hidden=true;
    document.body.append(backdrop);
    let previousFocus=null;
    const drawerHome=nav.parentNode;
    const drawerAfter=nav.nextSibling;
    const focusables=()=>[...nav.querySelectorAll('button:not([disabled]),a[href],[tabindex]:not([tabindex="-1"])')].filter(x=>x.getClientRects().length);
    const returnDrawer=()=>{
      if(nav.parentNode!==drawerHome && drawerHome?.isConnected){
        if(drawerAfter && drawerAfter.parentNode===drawerHome)drawerHome.insertBefore(nav,drawerAfter);
        else drawerHome.appendChild(nav);
      }
      nav.classList.remove('admin-viewport-drawer');
      nav.removeAttribute('aria-modal');
      nav.removeAttribute('role');
    };
    const close=()=>{
      const wasOpen=side.classList.contains('menu-open');
      side.classList.remove('menu-open');
      document.body.classList.remove('admin-drawer-open');
      backdrop.hidden=true;
      returnDrawer();
      document.querySelector('.admin-main')?.removeAttribute('inert');
      toggle.setAttribute('aria-expanded','false');
      toggle.setAttribute('aria-label','Yönetim menüsünü aç');
      if(wasOpen)try{previousFocus?.focus?.({preventScroll:true});}catch{}
      previousFocus=null;
    };
    const open=()=>{
      if(side.classList.contains('menu-open'))return;
      previousFocus=document.activeElement;
      // Put the sheet directly under the React root. This escapes the compact
      // logo/header stacking context while retaining React's delegated clicks.
      const host=document.getElementById('root');
      if(!host)return;
      host.appendChild(nav);
      nav.classList.add('admin-viewport-drawer');
      nav.setAttribute('role','dialog');
      nav.setAttribute('aria-modal','true');
      side.classList.add('menu-open');
      document.body.classList.add('admin-drawer-open');
      backdrop.hidden=false;
      document.querySelector('.admin-main')?.setAttribute('inert','');
      toggle.setAttribute('aria-expanded','true');
      toggle.setAttribute('aria-label','Yönetim menüsünü kapat');
      nav.scrollTop=0;
      requestAnimationFrame(()=>focusables()[0]?.focus({preventScroll:true}));
    };
    toggle.addEventListener('click',()=>side.classList.contains('menu-open')?close():open());
    sheetHead.querySelector('.admin-menu-sheet-close')?.addEventListener('click',close);
    backdrop.addEventListener('pointerdown',e=>{e.preventDefault();e.stopPropagation();close()});
    nav.addEventListener('click',e=>{if(innerWidth<=1100&&e.target.closest('button,a'))close()});
    document.addEventListener('keydown',e=>{if(e.key==='Escape'&&side.classList.contains('menu-open'))close();if(e.key==='Tab'&&side.classList.contains('menu-open')){const list=focusables();if(!list.length)return;const first=list[0],last=list[list.length-1];if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}}});
    window.addEventListener('resize',()=>{if(innerWidth>1100&&side.classList.contains('menu-open'))close()},{passive:true});
    window.addEventListener('pageshow',()=>{if(!side.classList.contains('menu-open')){document.body.classList.remove('admin-drawer-open');backdrop.hidden=true;document.querySelector('.admin-main')?.removeAttribute('inert');}});
  };
  const start=()=>{mount();if(document.querySelector('.admin-sidebar[data-r148-drawer]'))return;const observer=new MutationObserver(()=>{mount();if(document.querySelector('.admin-sidebar[data-r148-drawer]'))observer.disconnect();});observer.observe(document.getElementById('root')||document.body,{childList:true,subtree:true});setTimeout(()=>observer.disconnect(),5000)};
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
    service:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 12a9 9 0 0 1 18 0"/><path d="M3 12v5a3 3 0 0 0 3 3h2v-8H6a3 3 0 0 0-3 3M21 12v5a3 3 0 0 1-3 3h-2v-8h2a3 3 0 0 1 3 3"/><path d="M16 20a4 4 0 0 1-4 2h-2"/></svg>',
    account:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>',
    search:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg>',
    close:'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 6l12 12M18 6 6 18"/></svg>'
  }[name]||'');
  const activeKey=()=>{
    const p=path();
    if(p==='/')return'home';
    if(p==='/urunler'||p.startsWith('/urun/'))return'products';
    if(p.startsWith('/kategori/'))return'categories';
    if(p==='/iletisim')return'service';
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
  const fetchCategories=async()=>{
    const fromDom=uniqueCategories();if(fromDom.length)return fromDom;
    try{
      const r=await fetch('/api/catalog',{cache:'no-store'}),b=await r.json();
      const cats=Array.isArray(b?.data?.categories)?b.data.categories:[];
      const roots=cats.filter(c=>!c.parent),ordered=[...roots,...cats.filter(c=>c.parent)];
      return ordered.slice(0,18).map(c=>({href:'/kategori/'+encodeURIComponent(c.id),label:String(c.name||'Kategori')}));
    }catch{return[]}
  };
  const mountSheet=()=>{
    let overlay=document.querySelector('.app-category-overlay');
    if(overlay)return overlay;
    overlay=document.createElement('div');overlay.className='app-category-overlay';overlay.hidden=true;
    overlay.innerHTML='<div class="app-category-sheet" role="dialog" aria-modal="true" aria-label="Ürün kategorileri"><div class="app-sheet-handle"></div><div class="app-sheet-head"><div><small>ÜRÜN KATALOĞU</small><strong>Kategoriler</strong></div><button class="app-sheet-close" type="button" aria-label="Kapat">'+icon('close')+'</button></div><div class="app-category-list"></div><a class="app-all-products" href="/urunler">Tüm ürünleri görüntüle <span>→</span></a></div>';
    document.body.append(overlay);
    const close=()=>{overlay.classList.remove('is-ready');overlay.hidden=true;document.body.classList.remove('app-sheet-open')};
    overlay.addEventListener('click',e=>{if(e.target===overlay)close()});
    overlay.querySelector('.app-sheet-close').addEventListener('click',close);
    document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!overlay.hidden)close()});
    overlay._close=close;
    overlay._open=async()=>{
      const list=overlay.querySelector('.app-category-list');
      list.innerHTML='<div class="app-sheet-loading" aria-live="polite"><span></span><span></span><span></span></div>';
      overlay.hidden=false;document.body.classList.add('app-sheet-open');
      requestAnimationFrame(()=>overlay.classList.add('is-ready'));
      const cats=await fetchCategories();
      if(overlay.hidden)return;
      list.innerHTML=cats.length?cats.map(x=>'<a href="'+x.href+'"><span>'+x.label+'</span><b>›</b></a>').join(''):'<p class="app-sheet-empty">Henüz yayınlanmış kategori bulunmuyor.</p>';
    };
    return overlay;
  };
  const syncNav=()=>{const active=activeKey();document.querySelectorAll('.app-bottom-nav [data-app-key]').forEach(el=>el.classList.toggle('active',el.getAttribute('data-app-key')===active));};
  const mountNav=()=>{
    if(document.querySelector('.app-bottom-nav')){syncNav();return;}
    const active=activeKey(),nav=document.createElement('nav');nav.className='app-bottom-nav';nav.setAttribute('aria-label','Mobil uygulama navigasyonu');
    const items=[
      ['home','/','Ana Sayfa','home'],
      ['products','/urunler','Ürünler','products'],
      ['categories','#','Kategoriler','categories'],
      ['service','/iletisim','Destek','service'],
      ['account','/hesabim','Hesabım','account']
    ];
    nav.innerHTML=items.map(([key,href,label,ic])=>key==='categories'
      ?'<button type="button" class="app-bottom-item '+(active===key?'active':'')+'" data-app-key="'+key+'" data-app-categories>'+icon(ic)+'<span>'+label+'</span></button>'
      :'<a class="app-bottom-item '+(active===key?'active':'')+'" data-app-key="'+key+'" href="'+href+'">'+icon(ic)+'<span>'+label+'</span></a>').join('');
    document.body.append(nav);
    nav.querySelector('[data-app-categories]').addEventListener('click',()=>{window.dispatchEvent(new Event('inokskar:open-external-layer'));mountSheet()._open();});
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
  const start=()=>{mount();if(!document.querySelector('.site-header')&&!excluded()){const o=new MutationObserver(()=>{mount();if(document.querySelector('.site-header'))o.disconnect();});o.observe(document.getElementById('root')||document.body,{childList:true,subtree:true});setTimeout(()=>o.disconnect(),5000);}const refresh=()=>{mount();syncNav();document.querySelector('.app-category-overlay')?._close?.();};window.addEventListener('popstate',refresh);window.addEventListener('pageshow',refresh);window.addEventListener('inokskar:navigation',refresh)};
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
      nav.addEventListener('click',e=>{if(innerWidth<=1100&&e.target.closest('a,button'))close()});
      document.addEventListener('keydown',e=>{if(e.key==='Escape')close()});
    }
  };
  const mount=()=>{
    const side=document.querySelector('.admin-sidebar');
    const sideBrand=side?.querySelector(':scope>.brand');
    if(sideBrand&&!sideBrand.dataset.r158Brand){sideBrand.dataset.r158Brand='1';sideBrand.classList.add('private-brand-link');sideBrand.innerHTML=brandHtml;}
    document.querySelectorAll('.private-brand-header').forEach(setupHeader);
  };
  const start=()=>{mount();const root=document.getElementById('root')||document.body;const o=new MutationObserver(()=>{mount();if(document.querySelector('.private-brand-header[data-r158-brand],.private-app-dock'))o.disconnect();});o.observe(root,{childList:true,subtree:true});setTimeout(()=>o.disconnect(),5000)};
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
      refs[key]?.click();if(side.classList.contains('menu-open'))side.querySelector('.admin-mobile-toggle')?.click();window.scrollTo({top:0,behavior:'smooth'});sync();
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
  const clearStale=()=>{if(activeLayer())return;if(shield){shield.remove();shield=null;}document.body.classList.remove('ui-layer-locked');setBackgroundInert(false,null);};
  const start=()=>{sync();const schedule=()=>requestAnimationFrame(()=>{sync();clearStale()});document.addEventListener('click',schedule,true);document.addEventListener('pointerup',schedule,true);window.addEventListener('popstate',schedule);window.addEventListener('inokskar:navigation',schedule);window.addEventListener('pageshow',()=>{sync();clearStale()});window.addEventListener('resize',()=>{if(innerWidth>1100){const layer=activeLayer();if(layer?.kind==='private')layer.close();setTimeout(clearStale,0);}},{passive:true});};
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
    summary.innerHTML='<span class="avatar">'+(initials||'İ')+'</span><div><strong>'+String(name?.value||'Müşteri').replace(/[&<>]/g,'')+'</strong><small>'+String(readonly?.value||'').replace(/[&<>]/g,'')+'</small></div><a href="/iletisim?amac=servis">Yeni teknik servis talebi</a>';
    welcome?.insertAdjacentElement('afterend',summary);summary.insertAdjacentElement('afterend',tabs);
    const select=key=>{tabs.querySelectorAll('button').forEach(b=>b.classList.toggle('active',b.dataset.tab===key));layout.querySelectorAll('.customer-tab-panel').forEach(p=>p.hidden=p.dataset.customerTab!==key);};
    tabs.addEventListener('click',e=>{const b=e.target.closest('button[data-tab]');if(b)select(b.dataset.tab)});select('profile');
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',mount,{once:true});else mount();
})();

/* R16 — warranty query help for locating serial/verification information. */
(()=>{
  if(location.pathname!='/garanti-sorgulama')return;
  const mount=()=>{const form=document.getElementById('warranty-query-form');if(!form||form.dataset.r16Help)return;form.dataset.r16Help='1';const button=document.createElement('button');button.type='button';button.className='warranty-help-trigger';button.textContent='Bu bilgiler nerede?';const hint=document.createElement('div');hint.className='warranty-label-hint';hint.hidden=true;hint.innerHTML='<strong>Seri numarası:</strong><span>Cihazın ürün etiketinde bulunur. Garanti doğrulama kodu ise size verilen garanti belgesinde yer alır. Güvenlik nedeniyle yalnız seri numarasıyla sorgulama yapılamaz.</span>';form.querySelector('label:last-of-type')?.after(button,hint);button.addEventListener('click',()=>{hint.hidden=!hint.hidden;button.textContent=hint.hidden?'Bu bilgiler nerede?':'Yardımı kapat'});};
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

/* R16.5 — eliminate duplicate page-size controls regardless of legacy wrapper/class. */
(()=>{const clean=()=>{const bar=document.querySelector('.filterbar');if(!bar)return;const hits=[...bar.querySelectorAll('button,[role="combobox"],select')].filter(el=>/^\s*(12|24|48)\s*\/\s*sayfa\s*$/i.test((el.textContent||'').trim()));if(hits.length<=1)return;hits.slice(1).forEach(el=>{let node=el;while(node.parentElement&&node.parentElement!==bar)node=node.parentElement;if(node.parentElement===bar)node.remove();else el.remove();});};const start=()=>{clean();requestAnimationFrame(clean);setTimeout(clean,250);setTimeout(clean,900);const root=document.getElementById('root')||document.body;const o=new MutationObserver(clean);o.observe(root,{subtree:true,childList:true,characterData:true});setTimeout(()=>o.disconnect(),2200);};if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();})();

/* R20.2 — unread inquiry counter for the admin mobile dock. */
(()=>{
  if((location.pathname.replace(/\/$/,'')||'/')!=='/admin')return;
  const render=(count,attempt=0)=>{
    const btn=document.querySelector('.admin-app-dock [data-admin-dock="inquiries"]');
    if(!btn){if(attempt<24)setTimeout(()=>render(count,attempt+1),125);return;}
    let badge=btn.querySelector('.admin-inquiry-badge');
    if(count<=0){badge?.remove();btn.removeAttribute('aria-label');return;}
    if(!badge){badge=document.createElement('b');badge.className='admin-inquiry-badge';badge.setAttribute('aria-hidden','true');btn.append(badge);}
    badge.textContent=count>99?'99+':String(count);
    btn.setAttribute('aria-label','Talepler, '+count+' okunmamış talep');
  };
  const refresh=async()=>{
    try{
      const response=await fetch('/api/inquiries/unread-count',{cache:'no-store',headers:{Accept:'application/json'}});
      if(!response.ok)return;
      const body=await response.json();
      const count=Math.max(0,Number(body.count)||0);
      render(count);
    }catch{}
  };
  const start=()=>{
    refresh();
    const timer=setInterval(refresh,20000);
    window.addEventListener('pageshow',refresh);
    window.addEventListener('focus',refresh);
    window.addEventListener('inokskar:inquiry-change',refresh);
    document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible')refresh();});
    window.addEventListener('beforeunload',()=>clearInterval(timer),{once:true});
  };
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();

/* R20.5 — canonical public menu behavior for SPA and server-rendered pages. */
(()=>{
 const closeIcon='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>';
 const syncGeometry=nav=>{const header=nav?.closest?.('.site-header');if(!nav||!header)return;nav.style.setProperty('--public-menu-top',Math.max(64,Math.round(header.getBoundingClientRect().bottom+10))+'px');};
 const closeCategories=nav=>{nav.querySelectorAll('.nav-category.open').forEach(x=>x.classList.remove('open'));nav.querySelectorAll('.nav-category-toggle[aria-expanded="true"]').forEach(x=>x.setAttribute('aria-expanded','false'));nav.classList.remove('category-layer-open');};
 const closeServerMenu=nav=>{closeCategories(nav);nav.classList.remove('open');const toggle=nav.closest('.site-header')?.querySelector('.mobile-menu');toggle?.setAttribute('aria-expanded','false');toggle?.setAttribute('aria-label','Menüyü aç');};
 const switchCategory=(nav,category,toggle)=>{
   const wasOpen=category.classList.contains('open');
   closeCategories(nav);
   if(!wasOpen){category.classList.add('open');toggle?.setAttribute('aria-expanded','true');nav.classList.add('category-layer-open');requestAnimationFrame(()=>category.scrollIntoView({block:'nearest',behavior:'smooth'}));}
 };
 const ensure=nav=>{
   if(!(nav instanceof HTMLElement)||nav.closest('#root'))return;
   nav.classList.add('public-mobile-menu-sheet');syncGeometry(nav);
   if(!nav.querySelector(':scope>.mobile-menu-sheet-head')){
     const head=document.createElement('div');head.className='mobile-menu-sheet-head';
     head.innerHTML='<div><span>İNOKSKAR</span><strong>Menü</strong></div><button type="button" class="mobile-menu-sheet-close" aria-label="Menüyü kapat">'+closeIcon+'</button>';
     nav.prepend(head);head.querySelector('.mobile-menu-sheet-close')?.addEventListener('click',()=>closeServerMenu(nav));
   }
   if(nav.dataset.r205Menu)return;
   nav.dataset.r205Menu='1';
   nav.addEventListener('click',e=>{
     if(innerWidth>1100)return;
     const toggle=e.target.closest('.nav-category-toggle');
     if(toggle){e.preventDefault();e.stopPropagation();const category=toggle.closest('.nav-category');if(category)switchCategory(nav,category,toggle);return;}
     const categoryLink=e.target.closest('.nav-category-link');
     const category=categoryLink?.closest('.nav-category');
     if(category&&category.querySelector('.nav-dropdown')){e.preventDefault();switchCategory(nav,category,category.querySelector('.nav-category-toggle'));return;}
     if(e.target.closest('a[href]'))closeServerMenu(nav);
   });
 };
 const mount=()=>document.querySelectorAll('.site-header .main-nav').forEach(ensure);
 const start=()=>{
   mount();
   window.addEventListener('pageshow',mount);
   window.addEventListener('inokskar:navigation',mount);
   window.addEventListener('resize',()=>{document.querySelectorAll('.site-header .public-mobile-menu-sheet').forEach(nav=>{syncGeometry(nav);if(innerWidth>1100&&nav.classList.contains('open'))closeServerMenu(nav);});},{passive:true});
   window.addEventListener('orientationchange',mount);
   document.addEventListener('click',e=>{const toggle=e.target.closest('.site-header .mobile-menu');if(toggle)requestAnimationFrame(()=>{const nav=toggle.closest('.site-header')?.querySelector('.public-mobile-menu-sheet');if(nav)syncGeometry(nav);});},true);
 };
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
})();
