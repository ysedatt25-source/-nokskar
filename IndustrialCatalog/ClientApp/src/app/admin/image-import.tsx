import {useState} from 'react';
import {Catalog} from '../../lib/model';
import {ImageMatch,planImages,transferImages,UploadSessionExpired} from '../../lib/image-import';
import {Dialog,DialogContent,DialogHeader,DialogTitle,DialogDescription} from '@/components/ui/dialog';

export default function ImageImport({data,onApply,onBusy}:{data:Catalog,onApply:(next:Catalog)=>void,onBusy:(busy:boolean)=>void}){
 const [open,setOpen]=useState(false),[plan,setPlan]=useState<ImageMatch[]>([]),[running,setRunning]=useState(false),[summary,setSummary]=useState(''),[progress,setProgress]=useState(0);
 const matches=plan.filter(x=>x.productId&&!x.error);
 async function run(){
  setRunning(true);onBusy(true);setSummary('');setProgress(0);
  try{
   const result=await transferImages(plan,async file=>{
     const form=new FormData();form.append('file',file);
     const response=await fetch('/api/upload?kind=image',{method:'POST',body:form});
     if(response.status===401)throw new UploadSessionExpired('Oturum sona erdi. Oturum açtıktan sonra yeniden deneyin.');
     const body=await response.json().catch(()=>({error:'Dosya yükleme servisi geçerli yanıt vermedi.'})) as {error?:string,url?:string};
     if(!response.ok)throw Error(body.error||'Yükleme başarısız.');
     if(typeof body.url!=='string')throw Error('Sunucudan geçerli dosya bağlantısı alınamadı.');
     if(/\.pdf$/i.test(body.url))throw Error('Görsel alanına PDF eklenemez.');
     return body.url;
   },setProgress);
   const {additions,total,pending,failed}=result;
   if(total)onApply({...data,products:data.products.map(p=>({...p,images:[...p.images,...(additions.get(p.id)||[])]}))});
   // Retain failed/unmatched files, and remove successes so retry cannot duplicate them.
   setPlan(pending);
   setSummary(`${total} görsel ${additions.size} ürüne eklendi. ${failed} dosya tekrar kontrol edilmeli. Başarılı görselleri kalıcılaştırmak için Değişiklikleri kaydet düğmesini kullanın.`);
  }finally{setRunning(false);onBusy(false);}
 }
 return <>
  <button className="small-btn" onClick={()=>setOpen(true)}>Kodla toplu görsel aktar</button>
  <Dialog open={open} onOpenChange={v=>!running&&setOpen(v)}><DialogContent className="editor-dialog" showCloseButton={!running} aria-busy={running}>
   <DialogHeader><DialogTitle>Ürün koduyla toplu görsel aktarımı</DialogTitle><DialogDescription>Dosyaları seçin, eşleşmeleri kontrol edin ve görselleri ürünlere ekleyin.</DialogDescription></DialogHeader>
   <p>Dosya adlarını ürün koduyla hazırlayın: <strong>ABC-001.jpg</strong>, <strong>ABC-001__01.jpg</strong>, <strong>ABC-001__02.jpg</strong>. Mevcut galeri korunur; yeni görseller dosya sırasıyla sonuna eklenir.</p>
   <label className="upload-button">Görselleri seç<input aria-label="Kodla eşleştirilecek görseller" type="file" multiple accept="image/png,image/jpeg,image/webp" disabled={running} onChange={e=>{setPlan(planImages(Array.from(e.target.files||[]),data.products));setSummary('');e.target.value='';}}/></label>
   {running&&<p role="status">{progress} / {matches.length} dosya işlendi.</p>}{summary&&<p role="status" className="notice">{summary}</p>}
   <button className="small-btn" disabled={running||!plan.some(x=>x.productId&&x.error)} onClick={()=>setPlan(plan.map(x=>x.productId?{...x,error:''}:x))}>Başarısız yüklemeleri yeniden denemeye hazırla</button><p>{matches.length} eşleşen görsel · {plan.length-matches.length} kontrol gereken dosya</p>
   <div className="import-results">{plan.map((row,i)=><div className="import-result" key={i}><strong>{row.file.name}</strong><span>{row.productName?row.code+' · '+row.productName:row.error}</span>{row.productName&&row.error&&<span role="alert">{row.error}</span>}<button className="small-btn" disabled={running} onClick={()=>setPlan(plan.filter((_,j)=>j!==i))}>Listeden kaldır</button></div>)}</div>
   <button className="button" disabled={running||!matches.length} onClick={run}>{running?'Görseller aktarılıyor…':`${matches.length} görseli eşleşen ürünlere ekle`}</button>
  </DialogContent></Dialog>
 </>;
}
