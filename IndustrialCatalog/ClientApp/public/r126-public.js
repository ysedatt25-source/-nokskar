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
  function enhance(){
    classify();
    const isPublic=!location.pathname.startsWith('/admin')&&!location.pathname.startsWith('/teknik')&&!['/login','/kayit','/forgot-password','/hesabim','/hesabim/profil','/account/security'].includes(location.pathname);
    if(!isPublic||document.body.classList.contains('page-home'))return;
    const header=document.querySelector('.site-header');
    if(!header)return;
    const search=document.querySelector('.header-search');
    const input=search?.querySelector('input');
    if(input)input.placeholder='Ürün, kategori veya model ara...';
    utilities();
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
    window.setTimeout(()=>obs.disconnect(),8000);
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start,{once:true});else start();
  const rerun=()=>requestAnimationFrame(()=>{document.body.dataset.r126Public='';run();});
  window.addEventListener('inokskar:navigation',rerun);
  window.addEventListener('popstate',rerun);
})();
