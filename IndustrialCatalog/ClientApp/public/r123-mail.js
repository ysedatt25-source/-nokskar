(()=>{
  const form=document.getElementById('mail-settings-form');
  if(!form)return;
  const provider=form.elements.provider;
  const notice=document.getElementById('mail-notice');
  const health=document.getElementById('mail-health');
  const passwordState=document.getElementById('password-state');
  const usernameHint=document.getElementById('username-hint');
  const providerHelp=document.getElementById('provider-help');
  const testRecipient=document.getElementById('test-recipient');
  const testResult=document.getElementById('mail-test-result');
  const testButton=document.getElementById('mail-connection-test');
  let providers=[];
  let settings=null;

  const message=(host,text,type='ok')=>{host.innerHTML=`<div class="notice ${type}">${String(text).replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]))}</div>`;};
  const applyProvider=id=>{
    const p=providers.find(x=>x.id===id);if(!p)return;
    if(p.host)form.elements.host.value=p.host;
    form.elements.port.value=p.port||587;
    form.elements.enableSsl.value=String(p.enableSsl!==false);
    usernameHint.textContent=p.usernameHint?`Örnek: ${p.usernameHint}`:'';
    if(providerHelp)providerHelp.textContent=p.helpText||'Seçim yaptığınızda bilinen SMTP bilgileri aşağıya uygulanır.';
  };
  const fill=s=>{
    settings=s;
    form.elements.provider.value=s.provider||'smtp';
    form.elements.enabled.checked=Boolean(s.enabled);
    form.elements.host.value=s.host||'';
    form.elements.port.value=s.port||587;
    form.elements.enableSsl.value=String(s.enableSsl!==false);
    form.elements.username.value=s.username||'';
    form.elements.password.value='';
    form.elements.fromAddress.value=s.fromAddress||'';
    form.elements.fromName.value=s.fromName||'İNOKSKAR Soğutma ve Endüstriyel Mutfak';
    form.elements.adminAddress.value=s.adminAddress||'';
    testRecipient.value=s.adminAddress||'';
    passwordState.textContent=s.passwordConfigured?'Kayıtlı parola/API anahtarı var. Değiştirmeyecekseniz alanı boş bırakın.':'Henüz parola/API anahtarı kaydedilmemiş.';
    const p=providers.find(x=>x.id===form.elements.provider.value);usernameHint.textContent=p?.usernameHint?`Örnek: ${p.usernameHint}`:'';if(providerHelp)providerHelp.textContent=p?.helpText||'Seçim yaptığınızda bilinen SMTP bilgileri aşağıya uygulanır.';
    health.className='mail-health '+(s.configured?'ok':'warn');
    health.textContent=s.configured?'● Sağlayıcı hazır':s.enabled?'● Yapılandırma eksik':'○ E-posta sistemi pasif';
  };
  const load=async()=>{
    try{
      const r=await fetch('/api/admin/mail/settings',{cache:'no-store'});const b=await r.json();if(!r.ok)throw Error(b.error||'Ayarlar alınamadı.');
      providers=b.providers||[];provider.innerHTML=providers.map(p=>`<option value="${p.id}">${p.name}</option>`).join('');fill(b.settings||{});
    }catch(e){message(notice,e.message,'error');}
  };
  provider.addEventListener('change',()=>applyProvider(provider.value));
  form.addEventListener('submit',async e=>{
    e.preventDefault();notice.innerHTML='';const button=form.querySelector('button[type="submit"]');button.disabled=true;
    const body={provider:form.elements.provider.value,enabled:form.elements.enabled.checked,host:form.elements.host.value.trim(),port:Number(form.elements.port.value),enableSsl:form.elements.enableSsl.value==='true',username:form.elements.username.value.trim(),password:form.elements.password.value,fromAddress:form.elements.fromAddress.value.trim(),fromName:form.elements.fromName.value.trim(),adminAddress:form.elements.adminAddress.value.trim()};
    try{const r=await fetch('/api/admin/mail/settings',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});const b=await r.json().catch(()=>({}));if(!r.ok)throw Error(b.error||'Ayarlar kaydedilemedi.');fill(b.settings||body);message(notice,'E-posta sağlayıcı ayarları güvenli şekilde kaydedildi.','ok');}
    catch(err){message(notice,err.message,'error');}finally{button.disabled=false;}
  });
  testButton.addEventListener('click',async()=>{
    testResult.innerHTML='<div class="test-progress"><span class="test-dot"></span>SMTP sunucusuna bağlanılıyor ve test e-postası gönderiliyor…</div>';testButton.disabled=true;
    try{const r=await fetch('/api/admin/mail/test',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({to:testRecipient.value.trim()})});const b=await r.json().catch(()=>({}));if(!r.ok)throw Error(b.error||'Bağlantı testi başarısız.');message(testResult,'Bağlantı başarılı. Test e-postası SMTP sunucusu tarafından gönderim için kabul edildi.','ok');await load();}
    catch(err){message(testResult,err.message,'error');}finally{testButton.disabled=false;}
  });
  load();
})();
