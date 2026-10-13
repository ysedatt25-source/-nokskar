import {ArrowRight,ClipboardList,MessageSquare,Wrench} from 'lucide-react';
import './overview-workload.css';

type Inquiry={id:string;data:string;status?:string;readAt?:string;created:string;requestCode?:string};
const details=(q:Inquiry):Record<string,unknown>=>{try{const value=JSON.parse(q.data||'{}');return value&&typeof value==='object'&&!Array.isArray(value)?value:{};}catch{return {};}};
const completed=new Set(['answered','cevap verildi','resolved','çözüldü','closed','kapatıldı','completed','tamamlandı','cancelled','iptal']);
export default function OverviewWorkload({inquiries,canInquiries,canService,onOpen}:{inquiries:Inquiry[];canInquiries:boolean;canService:boolean;onOpen:()=>void}){
 if(!canInquiries&&!canService)return null;
 const records=inquiries.map(q=>({q,b:details(q)})).filter(({b})=>b.type==='service'?canService:canInquiries);
 const pending=records.filter(({q,b})=>!completed.has(String(b.serviceStatus||q.status||'new').toLocaleLowerCase('tr-TR')));
 const fresh=pending.filter(({q})=>Object.prototype.hasOwnProperty.call(q,'readAt')&&!String(q.readAt||'').trim());
 const service=pending.filter(({b})=>b.type==='service');
 const recent=[...pending].sort((a,b)=>Date.parse(b.q.created)-Date.parse(a.q.created)).slice(0,4);
 return <section className="admin-box overview-workload" aria-label="Bekleyen işler">
  <div className="workload-heading"><div><p className="eyebrow">İŞ TAKİBİ</p><h2>İlginizi bekleyen talepler</h2></div><button type="button" onClick={onOpen}>Taleplere git <ArrowRight size={16}/></button></div>
  <div className="workload-counts"><div><ClipboardList size={20}/><strong>{pending.length}</strong><span>Açık talep</span></div><div><MessageSquare size={20}/><strong>{fresh.length}</strong><span>Okunmamış talep</span></div><div><Wrench size={20}/><strong>{service.length}</strong><span>Devam eden servis</span></div></div>
  {recent.length?<div className="workload-list">{recent.map(({q,b})=><a key={q.id} href={'/admin/inquiries/'+encodeURIComponent(q.id)}><span><strong>{String(b.name||'Müşteri talebi')}</strong><small>{b.type==='service'?'Teknik servis':b.purpose==='teklif'?'Teklif':b.purpose==='proje'?'Proje':'Genel destek'} · {q.requestCode||'Talep detayı'}</small></span><time dateTime={q.created}>{Number.isFinite(Date.parse(q.created))?new Date(q.created).toLocaleDateString('tr-TR',{timeZone:'Europe/Istanbul'}):'—'}</time><ArrowRight size={16}/></a>)}</div>:<p className="workload-empty">Şu anda bekleyen talep yok. Yeni talepler geldiğinde burada görünecek.</p>}
 </section>;
}
