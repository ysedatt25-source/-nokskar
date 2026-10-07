import {useState} from 'react';
import {Upload,Download} from 'lucide-react';
import {Dialog,DialogContent,DialogHeader,DialogTitle,DialogDescription} from '@/components/ui/dialog';
import {Table,TableHeader,TableBody,TableRow,TableHead,TableCell} from '@/components/ui/table';
import {Catalog} from '../../lib/model';

type PriceRow={code:string,name:string,currentEuro:number,newEuro:number,currentVat:number,newVat:number,newTl:number,error:string};
const money=(n:number)=>new Intl.NumberFormat('tr-TR',{maximumFractionDigits:0}).format(n);
export default function PriceImport({data,revision,onImported,onBusy,onError,onNotice}:{data:Catalog,revision:number,onImported:()=>Promise<void>|void,onBusy:(v:boolean)=>void,onError:(s:string)=>void,onNotice:(s:string)=>void}){
 const [open,setOpen]=useState(false),[file,setFile]=useState<File|null>(null),[rows,setRows]=useState<PriceRow[]>([]),[loading,setLoading]=useState(false);
 const errors=rows.filter(x=>x.error).length;
 async function preview(next:File){setFile(next);setRows([]);setLoading(true);onBusy(true);onError('');try{const form=new FormData();form.append('file',next);const r=await fetch('/api/prices/preview',{method:'POST',body:form});const b:any=await r.json().catch(()=>({}));if(!r.ok)throw Error(b.error||'Fiyat dosyası önizlenemedi.');setRows(b.rows||[]);}catch(e){onError((e as Error).message);}finally{setLoading(false);onBusy(false);}}
 async function apply(){if(!file||!rows.length||errors)return;setLoading(true);onBusy(true);onError('');try{const form=new FormData();form.append('file',file);form.append('revision',String(revision));const r=await fetch('/api/prices/import',{method:'POST',body:form});const b:any=await r.json().catch(()=>({}));if(!r.ok)throw Error(b.error||'Fiyat dosyası içe aktarılamadı.');await onImported();onNotice(`${b.updated||0} ürünün Euro fiyatı/KDV oranı dosyadan güncellendi.`);setOpen(false);setRows([]);setFile(null);}catch(e){onError((e as Error).message);}finally{setLoading(false);onBusy(false);}}
 return <>
  <a className="small-btn" href="/api/prices/template" download><Download size={16}/>Excel şablonu</a>
  <button className="small-btn" type="button" onClick={()=>setOpen(true)}><Upload size={16}/>Excel/CSV fiyat aktar</button>
  <Dialog open={open} onOpenChange={v=>!loading&&setOpen(v)}><DialogContent className="editor-dialog" inert={loading}>
   <DialogHeader><DialogTitle>Excel / CSV ile toplu fiyat güncelleme</DialogTitle><DialogDescription>Ürün koduna göre Euro baz fiyatı ve ürün KDV oranını önizleyerek güncelleyin.</DialogDescription></DialogHeader>
   <p>En güvenli akış için önce <strong>Excel şablonu</strong>nu indirin. <strong>UrunKodu</strong> ve <strong>EuroFiyat</strong> zorunludur; <strong>KdvOrani</strong> boş bırakılırsa mevcut oran korunur.</p>
   <label className="upload-button"><Upload size={17}/>Excel veya CSV seç<input type="file" accept=".xlsx,.csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv" disabled={loading} onChange={e=>{const f=e.target.files?.[0];e.currentTarget.value='';if(f)preview(f);}}/></label>
   {file&&<p className="muted">Dosya: {file.name} · {rows.length} satır · {errors?errors+' hata':'hata yok'}</p>}
   {rows.length>0&&<div className="table-box price-import-preview"><Table><TableHeader><TableRow><TableHead>Kod / Ürün</TableHead><TableHead>Mevcut €</TableHead><TableHead>Yeni €</TableHead><TableHead>KDV</TableHead><TableHead>Yeni TL</TableHead><TableHead>Durum</TableHead></TableRow></TableHeader><TableBody>{rows.map((r,i)=><TableRow key={i}><TableCell><strong>{r.code}</strong><small>{r.name||'Eşleşmedi'}</small></TableCell><TableCell>{r.currentEuro.toLocaleString('tr-TR')} €</TableCell><TableCell>{r.newEuro.toLocaleString('tr-TR')} €</TableCell><TableCell>%{r.newVat.toLocaleString('tr-TR',{maximumFractionDigits:2})}</TableCell><TableCell>{money(r.newTl)} TL</TableCell><TableCell>{r.error?<span className="import-error">{r.error}</span>:<span>Hazır</span>}</TableCell></TableRow>)}</TableBody></Table></div>}
   {errors>0&&<p className="error-banner" role="alert">Dosyada {errors} hatalı satır var. Hataları düzeltmeden içe aktarma yapılmaz.</p>}
   <button className="button" type="button" disabled={loading||!rows.length||errors>0} onClick={apply}>{loading?'İşleniyor…':'Önizlenen fiyatları kaydet ve yayınla'}</button>
  </DialogContent></Dialog>
 </>;
}
