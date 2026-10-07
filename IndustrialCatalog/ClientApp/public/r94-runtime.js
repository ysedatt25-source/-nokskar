(function(){
  'use strict';
  let cache=null,loading=null;
  const money=n=>new Intl.NumberFormat('tr-TR',{maximumFractionDigits:0}).format(Number(n)||0);
  const vat=p=>Number(p&&p.vatRate!=null?p.vatRate:20).toLocaleString('tr-TR',{maximumFractionDigits:2});
  const esc=value=>String(value==null?'':value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  async function catalog(force){
    if(force){cache=null;loading=null;}
    if(cache)return cache;
    if(!loading)loading=fetch('/api/catalog',{cache:'no-store'}).then(async r=>{const b=await r.json();if(!r.ok)throw new Error(b.error||'Katalog alınamadı.');cache=b.data||{};return cache;}).finally(()=>{loading=null;});
    return loading;
  }
  function idFromHref(href){try{const u=new URL(href,location.origin);if(!u.pathname.startsWith('/urun/'))return '';return decodeURIComponent(u.pathname.slice(6));}catch{return '';}}
  function addSwipe(stage,prev,next){
    if(stage.dataset.r94Swipe)return;stage.dataset.r94Swipe='1';let start=null,suppressClick=false;
    stage.addEventListener('pointerdown',e=>{start=e.clientX;suppressClick=false;},{passive:true});
    stage.addEventListener('pointerup',e=>{if(start==null)return;const d=e.clientX-start;start=null;if(Math.abs(d)>35){suppressClick=true;(d>0?prev:next)();}},{passive:true});
    stage.addEventListener('pointercancel',()=>{start=null;},{passive:true});
    stage.addEventListener('click',e=>{if(!suppressClick)return;suppressClick=false;e.preventDefault();e.stopPropagation();},true);
  }
  function arrow(cls,label,text,fn){const b=document.createElement('button');b.type='button';b.className='gallery-arrow '+cls;b.setAttribute('aria-label',label);b.textContent=text;b.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();fn();});return b;}
  async function patchPrices(){
    if(location.pathname==='/admin')return;
    let data;try{data=await catalog(false);}catch{return;}
    const map=new Map((data.products||[]).map(p=>[String(p.id),p]));
    document.querySelectorAll('.product-card').forEach(card=>{
      const link=card.querySelector('a[href^="/urun/"]');const p=map.get(idFromHref(link&&link.href));if(!p)return;
      const price=card.querySelector('.price');if(!price)return;const key=String(p.tl||0)+'|'+vat(p);if(price.dataset.r94Price===key)return;price.dataset.r94Price=key;
      if(Number(p.tl)>0)price.innerHTML='<span class="price-amount">'+esc(money(p.tl))+' ₺</span><small>+ %'+esc(vat(p))+' KDV</small>';else price.innerHTML='<span class="price-enquire">Fiyat için bilgi alınız</span>';
    });
    const m=location.pathname.match(/^\/urun\/([^/]+)/);if(m){const p=map.get(decodeURIComponent(m[1]));if(p){const price=document.querySelector('.detail-price');if(price){const key=String(p.tl||0)+'|'+vat(p);if(price.dataset.r94Price!==key){price.dataset.r94Price=key;price.innerHTML=Number(p.tl)>0?'<span class="price-amount">'+esc(money(p.tl))+' ₺</span><small>+ %'+esc(vat(p))+' KDV</small>':'<span>Fiyat için bilgi alınız</span>';}}const note=document.querySelector('.tax-note');if(note)note.textContent='Fiyata %'+vat(p)+' KDV dâhil değildir.';}}
  }
  async function cardSliders(){
    if(location.pathname==='/admin')return;let data;try{data=await catalog(false);}catch{return;}const map=new Map((data.products||[]).map(p=>[String(p.id),p]));
    document.querySelectorAll('.product-card').forEach(card=>{
      if(card.dataset.r94Gallery)return;const link=card.querySelector('.product-photo[href^="/urun/"]');if(!link)return;const p=map.get(idFromHref(link.href));const images=(p&&Array.isArray(p.images)?p.images:[]).filter(Boolean);if(images.length<2)return;
      const img=link.querySelector('img');if(!img)return;card.dataset.r94Gallery='1';link.classList.add('product-photo-wrap');let index=Math.max(0,images.indexOf(img.getAttribute('src')));if(index<0)index=0;
      const count=document.createElement('span');count.className='gallery-count';const show=i=>{index=(i+images.length)%images.length;img.src=images[index];img.alt=p.name||'';count.textContent=(index+1)+'/'+images.length;};
      const prev=()=>show(index-1),next=()=>show(index+1);link.append(arrow('prev','Önceki ürün görseli','‹',prev),arrow('next','Sonraki ürün görseli','›',next),count);addSwipe(link,prev,next);show(index);
    });
  }
  async function detailSlider(){
    const m=location.pathname.match(/^\/urun\/([^/]+)/);if(!m)return;const stage=document.querySelector('.detail-photo');if(!stage||stage.dataset.r94Gallery)return;let data;try{data=await catalog(false);}catch{return;}const p=(data.products||[]).find(x=>String(x.id)===decodeURIComponent(m[1]));const images=(p&&Array.isArray(p.images)?p.images:[]).filter(Boolean);if(images.length<2)return;let img=stage.querySelector('img');if(!img)return;stage.dataset.r94Gallery='1';stage.classList.add('gallery-stage');let index=Math.max(0,images.indexOf(img.getAttribute('src')));if(index<0)index=0;
    const count=document.createElement('span');count.className='gallery-count';const thumbs=Array.from(document.querySelectorAll('.thumbnails button'));
    const show=i=>{index=(i+images.length)%images.length;img.src=images[index];img.alt=p.name||'';count.textContent=(index+1)+'/'+images.length;thumbs.forEach((b,j)=>{b.classList.toggle('selected',j===index);b.setAttribute('aria-pressed',j===index?'true':'false');});};
    const prev=()=>show(index-1),next=()=>show(index+1);stage.append(arrow('prev','Önceki ürün görseli','‹',prev),arrow('next','Sonraki ürün görseli','›',next),count);thumbs.forEach((b,j)=>b.addEventListener('click',()=>setTimeout(()=>show(j),0)));addSwipe(stage,prev,next);show(index);
  }
  function fallbackHeader(data){const s=data.settings||{},cats=(data.categories||[]).filter(c=>c.menu),menu=(s.menu||[]).filter(m=>m.url!='/blog');return '<header class="site-header"><a class="brand" href="/"><span><strong>'+esc(s.name||'İNOKSKAR')+'</strong><small>'+esc(s.tagline||'')+'</small></span></a><nav class="main-nav">'+cats.map(c=>'<a href="/kategori/'+encodeURIComponent(c.id)+'">'+esc(c.name)+'</a>').join('')+menu.map(m=>'<a href="'+esc(m.url)+'">'+esc(m.name)+'</a>').join('')+'</nav><div class="header-actions"><a class="small-btn" href="/urunler">Ürünler</a></div></header>';}
  function fallbackDetail(data,p){const s=data.settings||{},cat=(data.categories||[]).find(c=>c.id===p.category),images=(Array.isArray(p.images)?p.images:[]).filter(Boolean),specs=(Array.isArray(p.specs)?p.specs:[]).filter(x=>x&&x.name&&x.value),first=images[0]||'';return '<div class="fallback-detail-shell">'+fallbackHeader(data)+'<main><section class="wrap detail"><div class="fallback-detail-message">Ürün detay görünümü güvenli modda yüklendi.</div><div class="breadcrumb"><a href="/urunler">Ürünler</a><span>/</span><span>'+esc(p.name)+'</span></div><div class="detail-grid"><div><div class="detail-photo">'+(first?'<img src="'+esc(first)+'" alt="'+esc(p.name)+'">':'<div class="photo-empty"><span>Ürün görseli eklenmedi</span></div>')+'</div>'+(images.length>1?'<div class="thumbnails">'+images.map((x,i)=>'<button type="button" class="'+(i===0?'selected':'')+'" aria-label="Görsel '+(i+1)+'"><img src="'+esc(x)+'" alt=""></button>').join('')+'</div>':'')+'</div><div class="detail-summary"><p class="eyebrow">'+esc(cat&&cat.name||'')+'</p><h1>'+esc(p.name)+'</h1><p class="product-code">Ürün kodu: <strong>'+esc(p.code||p.id)+'</strong></p><span class="status">'+esc(p.status||'Bilgi alınız')+'</span><div class="detail-price">'+(Number(p.tl)>0?'<span class="price-amount">'+esc(money(p.tl))+' ₺</span><small>+ %'+esc(vat(p))+' KDV</small>':'<span>Fiyat için bilgi alınız</span>')+'</div><p class="tax-note">Fiyata %'+esc(vat(p))+' KDV dâhil değildir.</p><a class="button" href="/iletisim?urun='+encodeURIComponent(p.name||'')+'">Ürün hakkında bilgi al</a></div></div><div class="product-details"><p class="eyebrow">DAHA YAKINDAN TANIYIN</p><h2>Ürün detayları</h2><p class="preserve">'+esc(p.description||'')+'</p>'+(specs.length?'<dl class="specs">'+specs.map(x=>'<div><dt>'+esc(x.name)+'</dt><dd>'+esc(x.value)+'</dd></div>').join('')+'</dl>':'')+'<p class="preserve">'+esc(p.after||'')+'</p></div></section></main><footer><div class="wrap footer-bottom"><span>© '+new Date().getFullYear()+' '+esc(s.name||'İNOKSKAR')+'</span><span>Soğutma · Mutfak · Mobilya</span></div></footer></div>';}
  async function ensureDetail(){
    const m=location.pathname.match(/^\/urun\/([^/]+)/);if(!m||document.querySelector('.detail'))return;let data;try{data=await catalog(false);}catch{return;}const id=decodeURIComponent(m[1]),p=(data.products||[]).find(x=>String(x.id)===id);if(!p)return;const root=document.getElementById('root');if(!root)return;root.innerHTML=fallbackDetail(data,p);await detailSlider();await patchPrices();
  }
  let scheduled=false;function apply(){if(scheduled)return;scheduled=true;requestAnimationFrame(async()=>{scheduled=false;await patchPrices();await cardSliders();await detailSlider();});}
  window.addEventListener('error',()=>setTimeout(ensureDetail,80));window.addEventListener('unhandledrejection',()=>setTimeout(ensureDetail,80));
  const start=()=>{apply();setTimeout(ensureDetail,1200);new MutationObserver(apply).observe(document.body,{childList:true,subtree:true});window.setInterval(async()=>{try{await catalog(true);apply();}catch{}},5*60*1000);};
  if(document.body)start();else document.addEventListener('DOMContentLoaded',start,{once:true});
})();
