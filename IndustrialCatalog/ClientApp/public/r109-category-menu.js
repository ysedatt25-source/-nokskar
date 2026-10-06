(function(){
'use strict';
let catalog=null,scheduled=false;
const mobile=()=>window.matchMedia('(max-width:850px), (hover:none), (pointer:coarse)').matches;
function esc(s){return String(s??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));}
function childTree(root){
 const out=[];
 function walk(parent,depth){
  catalog.categories.filter(c=>c.parent===parent).forEach(c=>{out.push({c,depth});walk(c.id,depth+1);});
 }
 walk(root.id,0);return out;
}
function currentCategoryId(){const m=location.pathname.match(/^\/kategori\/([^/]+)/);return m?decodeURIComponent(m[1]):'';}
function isUnder(rootId,id){if(!id)return false;let cursor=catalog.categories.find(c=>c.id===id),seen=new Set();while(cursor&&!seen.has(cursor.id)){if(cursor.id===rootId)return true;seen.add(cursor.id);cursor=catalog.categories.find(c=>c.id===cursor.parent);}return false;}
function enhance(){
 if(!catalog)return;
 document.querySelectorAll('.subcategories').forEach(el=>el.remove());
 const nav=document.querySelector('#primary-menu.main-nav');if(!nav)return;
 const current=currentCategoryId();
 catalog.categories.filter(c=>c.parent&&c.menu).forEach(child=>{Array.from(nav.querySelectorAll(':scope>a')).forEach(a=>{try{if(decodeURIComponent(new URL(a.href,location.origin).pathname)==='/kategori/'+child.id)a.remove();}catch{}});});
 const roots=catalog.categories.filter(c=>!c.parent&&c.menu);
 roots.forEach(root=>{
  if(nav.querySelector('.nav-category[data-category-id="'+CSS.escape(root.id)+'"]'))return;
  const anchor=Array.from(nav.querySelectorAll(':scope>a')).find(a=>{
   try{return new URL(a.href,location.origin).pathname==='/kategori/'+encodeURIComponent(root.id)||new URL(a.href,location.origin).pathname==='/kategori/'+root.id;}catch{return false;}
  });
  if(!anchor)return;
  const descendants=childTree(root);
  const wrap=document.createElement('div');wrap.className='nav-category'+(isUnder(root.id,current)?' active':'');wrap.dataset.categoryId=root.id;
  const head=document.createElement('div');head.className='nav-category-head';
  anchor.classList.add('nav-category-link');anchor.replaceWith(wrap);head.append(anchor);wrap.append(head);
  if(descendants.length){
   const toggle=document.createElement('button');toggle.type='button';toggle.className='nav-category-toggle';toggle.setAttribute('aria-label',root.name+' alt kategorilerini aç/kapat');toggle.setAttribute('aria-expanded','false');toggle.innerHTML='<span aria-hidden="true">⌄</span>';head.append(toggle);
   const dd=document.createElement('div');dd.className='nav-dropdown';dd.setAttribute('role','menu');
   const all=document.createElement('a');all.className='nav-dropdown-all';all.href='/kategori/'+encodeURIComponent(root.id);all.textContent='Tüm '+root.name;all.setAttribute('role','menuitem');dd.append(all);
   descendants.forEach(({c,depth})=>{const a=document.createElement('a');a.href='/kategori/'+encodeURIComponent(c.id);a.setAttribute('role','menuitem');a.innerHTML='<span>'+esc(c.name)+'</span><span aria-hidden="true">›</span>';if(depth){a.classList.add('nav-dropdown-nested');a.style.paddingLeft=(18+depth*15)+'px';}dd.append(a);});
   wrap.append(dd);
   toggle.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();const open=!wrap.classList.contains('open');nav.querySelectorAll('.nav-category.open').forEach(x=>{if(x!==wrap){x.classList.remove('open');x.querySelector('.nav-category-toggle')?.setAttribute('aria-expanded','false');}});wrap.classList.toggle('open',open);toggle.setAttribute('aria-expanded',String(open));});
   anchor.addEventListener('click',e=>{if(!mobile())return;if(!wrap.classList.contains('open')){e.preventDefault();nav.querySelectorAll('.nav-category.open').forEach(x=>{if(x!==wrap){x.classList.remove('open');x.querySelector('.nav-category-toggle')?.setAttribute('aria-expanded','false');}});wrap.classList.add('open');toggle.setAttribute('aria-expanded','true');}});
  }
 });
}
function queue(){if(scheduled)return;scheduled=true;requestAnimationFrame(()=>{scheduled=false;enhance();});}
fetch('/api/catalog',{cache:'no-store'}).then(r=>r.json()).then(b=>{catalog=b.data||b;queue();}).catch(()=>{});
new MutationObserver(queue).observe(document.documentElement,{childList:true,subtree:true});
document.addEventListener('click',e=>{if(!mobile()&&!e.target.closest('.nav-category'))document.querySelectorAll('.nav-category.open').forEach(x=>x.classList.remove('open'));});
window.addEventListener('resize',()=>{if(!mobile())document.querySelectorAll('.nav-category.open').forEach(x=>{x.classList.remove('open');x.querySelector('.nav-category-toggle')?.setAttribute('aria-expanded','false');});});
})();
