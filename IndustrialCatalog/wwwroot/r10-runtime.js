(function(){
  'use strict';
  const text=n=>(n&&n.textContent||'').trim();
  function addServiceNav(){
    document.querySelectorAll('.main-nav').forEach(nav=>{
      if(!nav.querySelector('a[href="/servis-talebi"]')){
        const a=document.createElement('a');a.href='/servis-talebi';a.textContent='Servis Talebi';nav.append(a);
      }
    });
  }
  function upgradeContact(){
    if(location.pathname!=='/iletisim')return;
    const form=document.querySelector('.inquiry-form');if(!form)return;
    const success=form.querySelector('.success');
    if(success&&!success.dataset.r10Receipt){
      success.dataset.r10Receipt='1';
      const h=success.querySelector('h2');const p=success.querySelector('p');
      if(h)h.textContent='Talebiniz başarıyla alındı.';
      if(p)p.textContent='Talep numaranız ve alındı bilgisi e-posta adresinize gönderilir. Ekibimiz talebinizi inceleyerek sizinle iletişime geçecektir.';
      return;
    }
    if(form.dataset.r10Contact==='1')return;
    const labels=Array.from(form.querySelectorAll('label'));
    const contactLabel=labels.find(x=>text(x).startsWith('Telefon veya e-posta'));
    if(!contactLabel)return;
    const input=contactLabel.querySelector('input');if(!input)return;
    contactLabel.firstChild.textContent='E-posta';
    input.name='email';input.type='email';input.autocomplete='email';input.required=true;
    const phoneLabel=document.createElement('label');phoneLabel.textContent='Telefon ';
    const optional=document.createElement('span');optional.className='optional-label';optional.textContent='(isteğe bağlı)';phoneLabel.append(optional);
    const phone=document.createElement('input');phone.name='phone';phone.type='tel';phone.maxLength=40;phone.autocomplete='tel';phoneLabel.append(phone);
    contactLabel.after(phoneLabel);
    const muted=Array.from(form.querySelectorAll('.muted')).find(x=>text(x).includes('Bilgileriniz'));
    if(muted)muted.textContent='Talebiniz yönetim paneline kaydedilir. E-posta adresinize kurumsal alındı bildirimi gönderilir.';
    const button=form.querySelector('button[type="submit"],button:not([type])');if(button)button.textContent='Destek talebini gönder';
    form.dataset.r10Contact='1';
  }
  function upgradeFooter(){document.querySelectorAll('footer,.consult-strip').forEach(n=>n.remove());}
  function upgradeAdmin(){
    if(location.pathname!=='/admin')return;
    const nav=document.querySelector('.admin-sidebar nav');
    if(nav&&!nav.querySelector('a[href="/admin/system"]')){
      const a=document.createElement('a');a.href='/admin/system';a.className='r10-system-link';a.textContent='⚙ Sistem Durumu';nav.append(a);
    }
    document.querySelectorAll('.bulk-controls p').forEach(p=>{if(text(p).includes('binlik adımlarla'))p.textContent='Euro baz fiyatını değiştirin. TL fiyatı güncel kura göre hesaplanır; yayınlanan TL tutarı ancak fark 500 TL veya üzerindeyse otomatik değişir.';});
  }
  function run(){addServiceNav();upgradeContact();upgradeFooter();upgradeAdmin();}
  let queued=false;const schedule=()=>{if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;run();});};
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',run);else run();
  new MutationObserver(schedule).observe(document.documentElement,{childList:true,subtree:true});
})();
