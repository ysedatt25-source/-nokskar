import {useEffect,useRef,useState} from 'react';
import {Sparkles,LoaderCircle,Check,X} from 'lucide-react';
import './product-description.css';

type Props={name:string;category:string;images:string[];disabled:boolean;autoGenerate:boolean;description:string;onSaveGuard:(message:string)=>void;onApply:(text:string)=>void};
export default function ProductDescription({name,category,images,disabled,autoGenerate,description,onSaveGuard,onApply}:Props){
 const [ready,setReady]=useState<boolean|null>(null),[busy,setBusy]=useState(false),[draft,setDraft]=useState(''),[message,setMessage]=useState(''),[failed,setFailed]=useState(false);
 const controller=useRef<AbortController|null>(null);
 const descriptionRef=useRef(description);descriptionRef.current=description;
 useEffect(()=>{onSaveGuard(busy?'Açıklama hazırlanıyor. İşlemin tamamlanmasını bekleyin veya iptal edin.':draft?'Hazırlanan taslağı açıklamaya ekleyin veya Vazgeç seçeneğiyle kapatın.':failed&&!description.trim()?'Açıklama hazırlanamadı. Yeniden deneyin veya açıklama alanını doldurun.':'');return()=>onSaveGuard('');},[busy,draft,failed,description,onSaveGuard]);
 const fingerprint=JSON.stringify([name,category,images.slice(0,3)]);
 useEffect(()=>{let active=true;fetch('/api/admin/product-description/settings',{cache:'no-store'}).then(async r=>{if(!r.ok)throw Error();const b=await r.json();if(active){setReady(b.ready===true);}}).catch(()=>{if(active)setMessage('Bağlantı durumu alınamadı. Sayfayı yenileyin.');});return()=>{active=false;controller.current?.abort();};},[]);
 useEffect(()=>{controller.current?.abort();setBusy(false);setDraft('');setFailed(false);setMessage('');},[fingerprint]);
 async function post(url:string,body:object,signal?:AbortSignal){const tokenResponse=await fetch('/api/security/csrf',{signal,cache:'no-store'});if(!tokenResponse.ok)throw Error('Oturumunuzu kontrol edip sayfayı yenileyin.');const {token}=await tokenResponse.json();const r=await fetch(url,{method:'POST',signal,headers:{'Content-Type':'application/json','X-INOKSKAR-CSRF':token},body:JSON.stringify(body)});const b=await r.json().catch(()=>({}));if(!r.ok)throw Error(b.error||'İşlem tamamlanamadı. Tekrar deneyin.');return b;}
 async function generate(){if(busy)return;const startingDescription=descriptionRef.current;const ac=new AbortController();controller.current=ac;const timer=window.setTimeout(()=>ac.abort(),75000);setBusy(true);setDraft('');setFailed(false);setMessage('');try{const b=await post('/api/admin/product-description',{name,category,images:images.slice(0,3)},ac.signal);if(!ac.signal.aborted){if(typeof b.description!=='string'||!b.description.trim())throw Error('Açıklama oluşturulamadı. Tekrar deneyin.');if(!startingDescription.trim()&&descriptionRef.current===startingDescription){onApply(b.description);setMessage('Açıklama aşağıdaki alana eklendi. Düzenleyebilir ve ürünü kaydedebilirsiniz.');}else setDraft(b.description);}}catch(e){setFailed(true);if(!ac.signal.aborted)setMessage((e as Error).message);else if(controller.current===ac)setMessage('İnceleme durduruldu. Mevcut açıklamanız korundu.');}finally{window.clearTimeout(timer);if(controller.current===ac){setBusy(false);controller.current=null;}}}
 const generateRef=useRef(generate);generateRef.current=generate;
 const autoAttempt=useRef('');
 useEffect(()=>{
  if(!autoGenerate||ready!==true||disabled||busy||description.trim()||draft||!images.length||!name.trim()||autoAttempt.current===fingerprint)return;
  const timer=window.setTimeout(()=>{autoAttempt.current=fingerprint;void generateRef.current();},1200);
  return()=>window.clearTimeout(timer);
 },[autoGenerate,ready,disabled,busy,description,draft,fingerprint,name,images.length]);
 if(ready!==true)return null;
 return <div className="product-description-assistant">
  <div className="description-assistant-heading"><span><Sparkles size={17} aria-hidden="true"/><strong>Görsele özel açıklama</strong></span></div>
  <p>API bağlantısı kayıtlı. Yeni üründe fotoğraflar yüklenince açıklama otomatik hazırlanır. İlk üç fotoğrafı inceler; taslağı görsellerle tekrar karşılaştırır. Boş açıklama alanına doğrudan ekler; mevcut metin varsa önce taslak gösterir.</p>
  <div className="description-assistant-actions"><button type="button" className="small-btn description-generate" disabled={disabled||busy||ready!==true||!images.length||!name.trim()} onClick={()=>void generate()}>{busy?<LoaderCircle size={16} className="description-spin"/>:<Sparkles size={16}/>} {busy?'Görseller inceleniyor…':'Görsele göre açıklama hazırla'}</button>{busy&&<button type="button" className="small-btn" onClick={()=>controller.current?.abort()}>İptal</button>}</div>
  {!images.length&&<small>Önce ürün fotoğrafı yükleyin.</small>}
  {message&&<p className="description-assistant-message" role="status">{message}</p>}
  {draft&&<div className="description-draft"><label>Hazırlanan açıklama<textarea rows={7} value={draft} onChange={e=>setDraft(e.target.value)} maxLength={2400}/></label><div className="description-assistant-actions"><button type="button" className="small-btn description-generate" disabled={disabled||!draft.trim()} onClick={()=>{onApply(draft.trim());setDraft('');setMessage('Taslak açıklama alanına eklendi. Ürünü kaydettiğinizde yayınlanır.');}}><Check size={16}/>Açıklamaya ekle</button><button type="button" className="small-btn" onClick={()=>setDraft('')}><X size={16}/>Vazgeç</button></div><small>Mevcut açıklama ancak “Açıklamaya ekle” seçildiğinde değiştirilir.</small></div>}
 </div>;
}
