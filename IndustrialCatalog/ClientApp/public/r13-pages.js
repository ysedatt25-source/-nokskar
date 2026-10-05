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
