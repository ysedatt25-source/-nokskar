(()=>{
  'use strict';
  if(location.pathname!=='/admin') return;

  const textNodeReplace=(element,from,to)=>{
    for(const node of element.childNodes){
      if(node.nodeType===Node.TEXT_NODE && (node.nodeValue||'').includes(from)) node.nodeValue=(node.nodeValue||'').replace(from,to);
    }
  };
  const readError=async response=>{
    try{const body=await response.json();return body.error||body.title||'İşlem tamamlanamadı.';}catch{return 'İşlem tamamlanamadı.';}
  };
  const saveBlob=(blob,name)=>{
    const url=URL.createObjectURL(blob),a=document.createElement('a');
    a.href=url;a.download=name;document.body.appendChild(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);
  };

  async function currentRevision(){
    const response=await fetch('/api/catalog?admin=1',{credentials:'same-origin',cache:'no-store'});
    if(response.status===401){location.href='/login';throw new Error('Oturum sona erdi.');}
    if(!response.ok) throw new Error(await readError(response));
    const body=await response.json();
    if(!Number.isInteger(body.revision)) throw new Error('Katalog sürümü okunamadı.');
    return body.revision;
  }

  function enhance(){
    const heading=[...document.querySelectorAll('h2')].find(x=>x.textContent?.trim()==='Otomatik kayıt geçmişi');
    if(!heading) return;
    const box=heading.closest('.admin-box');
    if(!box || box.querySelector('[data-full-backup-actions]')) return;

    const description=heading.parentElement?.querySelector('p');
    if(description) description.textContent='Her kaydetmeden önce önceki sürüm korunur. JSON düğmeleri yalnızca katalog verisini; tam ZIP yedeği ise katalogla birlikte kullanılan görsel ve PDF dosyalarını taşır.';
    for(const button of box.querySelectorAll('button')) if(button.textContent?.includes('Yedeği indir')) textNodeReplace(button,'Yedeği indir','JSON indir');
    for(const label of box.querySelectorAll('label')) if(label.textContent?.includes('Yedek yükle')) textNodeReplace(label,'Yedek yükle','JSON yükle');

    const actions=document.createElement('div');
    actions.dataset.fullBackupActions='1';
    actions.className='backup-full-actions';

    const download=document.createElement('button');
    download.type='button';download.className='button secondary';download.textContent='Tam ZIP yedeğini indir';

    const uploadLabel=document.createElement('label');
    uploadLabel.className='upload-button';uploadLabel.textContent='Tam ZIP yedeğini geri yükle';
    const input=document.createElement('input');
    input.type='file';input.accept='.zip,application/zip';uploadLabel.appendChild(input);

    const status=document.createElement('small');
    status.className='backup-full-status muted';status.setAttribute('role','status');

    const setBusy=value=>{download.disabled=value;input.disabled=value;};
    download.addEventListener('click',async()=>{
      setBusy(true);status.textContent='Tam yedek hazırlanıyor…';
      try{
        const response=await fetch('/api/backup',{credentials:'same-origin',cache:'no-store'});
        if(response.status===401){location.href='/login';return;}
        if(!response.ok) throw new Error(await readError(response));
        const blob=await response.blob();
        const disposition=response.headers.get('content-disposition')||'';
        const match=/filename\*?=(?:UTF-8''|\")?([^\";]+)/i.exec(disposition);
        const fallback='inokskar-tam-yedek-'+new Date().toISOString().slice(0,10)+'.zip';
        saveBlob(blob,match?decodeURIComponent(match[1].replace(/\"/g,'')):fallback);
        status.textContent='Tam ZIP yedeği indirildi.';
      }catch(error){status.textContent=error instanceof Error?error.message:'Yedek indirilemedi.';}
      finally{setBusy(false);}
    });

    input.addEventListener('change',async()=>{
      const file=input.files?.[0];if(!file)return;
      if(!confirm('Tam ZIP yedeği katalog içeriğini geri yükleyecek. Mevcut içerik işlem öncesi geçmişe kaydedilecek. Devam edilsin mi?')){input.value='';return;}
      setBusy(true);status.textContent='Yedek doğrulanıyor ve geri yükleniyor…';
      try{
        const revision=await currentRevision();
        const form=new FormData();form.append('file',file);form.append('revision',String(revision));
        const response=await fetch('/api/backup/restore',{method:'POST',body:form,credentials:'same-origin'});
        if(response.status===401){location.href='/login';return;}
        if(!response.ok) throw new Error(await readError(response));
        status.textContent='Tam yedek geri yüklendi. Sayfa yenileniyor…';
        setTimeout(()=>location.reload(),400);
      }catch(error){status.textContent=error instanceof Error?error.message:'Yedek geri yüklenemedi.';}
      finally{input.value='';setBusy(false);}
    });

    actions.append(download,uploadLabel,status);box.appendChild(actions);
  }

  const observer=new MutationObserver(enhance);observer.observe(document.documentElement,{childList:true,subtree:true});enhance();
})();
