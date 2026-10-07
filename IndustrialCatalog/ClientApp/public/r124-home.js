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
