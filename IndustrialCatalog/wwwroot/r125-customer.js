(()=>{
  async function profile(){
    try{
      const r=await fetch('/api/customer/profile',{cache:'no-store',credentials:'same-origin'});
      if(r.status===204||!r.ok)return null;
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
