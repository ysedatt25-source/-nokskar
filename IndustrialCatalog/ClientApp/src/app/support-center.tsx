import {useState,type FormEvent} from 'react';
import {ArrowRight, CheckCircle2, ClipboardList, Clock3, DraftingCompass, Headphones, Mail, MapPin, Phone, Search, Send, ShieldCheck, Wrench} from 'lucide-react';
import type {Catalog} from '../lib/model';

type Purpose = 'genel' | 'teklif' | 'servis' | 'proje';
const choices = [
  {key:'genel' as Purpose,label:'Genel Destek',detail:'Sorularınız, görüşleriniz ve önerileriniz',Icon:Headphones},
  {key:'teklif' as Purpose,label:'Teklif Al',detail:'Ürün, fiyat ve toplu sipariş talepleri',Icon:ClipboardList},
  {key:'servis' as Purpose,label:'Teknik Servis',detail:'Arıza, bakım, onarım ve yedek parça',Icon:Wrench},
  {key:'proje' as Purpose,label:'Proje & Danışmanlık',detail:'Özel üretim ve kurulum çözümleri',Icon:DraftingCompass}
];
const validPurpose=(v:string|null):Purpose=>choices.some(c=>c.key===v)?v as Purpose:'genel';

export default function SupportCenter({settings:s}:{settings:Catalog['settings']}) {
  const query=new URLSearchParams(typeof window!=='undefined'?window.location.search:'');
  const [purpose,setPurpose]=useState<Purpose>(()=>validPurpose(query.get('amac')));
  const [sentCode,setSentCode]=useState('');
  const [sent,setSent]=useState(false);
  const [sending,setSending]=useState(false);
  const [error,setError]=useState('');
  const selected=choices.find(c=>c.key===purpose)!;
  const product=query.get('urun')||'';
  const code=query.get('kod')||'';
  const switchPurpose=(next:Purpose)=>{
    setPurpose(next);setSent(false);setSentCode('');setError('');
    const url=new URL(window.location.href);url.searchParams.set('amac',next);url.searchParams.delete('urun');url.searchParams.delete('kod');
    window.history.replaceState(window.history.state,'',url.pathname+url.search);
  };
  const submit=async(e:FormEvent<HTMLFormElement>)=>{
    e.preventDefault();if(sending)return;
    const form=e.currentTarget;
    const fields=Object.fromEntries(new FormData(form).entries());
    const email=String(fields.email||'').trim(),phone=String(fields.phone||'').trim();
    if(!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email)){
      setError('Geçerli bir kişisel veya iş e-posta adresi girin.');
      (form.elements.namedItem('email') as HTMLInputElement|null)?.focus();return;
    }
    if(!/^\+?[\d\s().-]+$/.test(phone)||phone.replace(/\D/g,'').length<10||phone.replace(/\D/g,'').length>15){
      setError('Telefon numarası 10–15 rakam içermelidir. +90 veya ülke kodu olmadan yazabilirsiniz.');
      (form.elements.namedItem('phone') as HTMLInputElement|null)?.focus();return;
    }
    setError('');setSending(true);
    try{
      const response=await fetch(purpose==='servis'?'/api/service-request':'/api/inquiry',{
        method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(fields)
      });
      const result=await response.json().catch(()=>({}));
      if(!response.ok||!result.ok)throw new Error(result.error||'Talebiniz şu anda gönderilemedi. Lütfen tekrar deneyin.');
      setSentCode(String(result.requestCode||''));setSent(true);
    }catch(err){setError(err instanceof Error?err.message:'Bağlantı sorunu oluştu, tekrar deneyin.');}
    finally{setSending(false);}
  };
  return <section className="wrap content-page contact-pro support-center" aria-labelledby="support-title">
    <div className="support-intro">
      <span className="support-eyebrow"><ShieldCheck size={15}/> İNOKSKAR DESTEK MERKEZİ</span>
      <h1 id="support-title">Size nasıl yardımcı olabiliriz?</h1>
      <p>İhtiyacınızı seçin, size uygun kısa formu doldurun. Talebiniz doğru ekibe ulaşsın.</p>
    </div>
    <div className="contact-purpose support-purpose" role="group" aria-label="Talep amacı seçin">
      {choices.map(({key,label,detail,Icon})=><button type="button" key={key} className={'support-choice '+(purpose===key?'active':'')} onClick={()=>switchPurpose(key)} aria-pressed={purpose===key}>
        <span className="support-choice-icon"><Icon size={22} strokeWidth={1.9}/></span>
        <span className="support-choice-copy"><strong>{label}</strong><small>{detail}</small></span>
        <ArrowRight size={17} className="support-choice-arrow"/>
      </button>)}
    </div>
    <div className="support-layout">
      <div className="support-form-shell" id="destek-formu">
        <div className="support-form-heading"><div><span>TALEP OLUŞTUR</span><h2>{selected.label}</h2><p>{selected.detail}</p></div><selected.Icon size={26} aria-hidden="true"/></div>
        {sent?<div className="support-success" role="status">
          <CheckCircle2 size={44}/><h3>Talebiniz başarıyla alındı</h3>
          <p>Başvurunuz kayıt altına alındı. Süreci talep sorgulama sayfasından takip edebilirsiniz.</p>
          {sentCode&&<div className="support-request-code"><small>TALEP NUMARANIZ</small><strong>{sentCode}</strong></div>}
          <a className="button" href="/talep-sorgula">Talebimi sorgula <ArrowRight size={16}/></a>
          <button className="support-text-action" type="button" onClick={()=>{setSent(false);setSentCode('');}}>Yeni talep oluştur</button>
        </div>:<form key={purpose} className="inquiry-form support-form" onSubmit={submit}>
          <input type="hidden" name="purpose" value={purpose}/>
          <div className="support-fields-pair">
            <label>Adınız ve soyadınız  *<input name="name" required minLength={2} maxLength={100} autoComplete="name" placeholder="Adınız Soyadınız"/></label>
            <label>Telefon numaranız  *<input name="phone" required type="tel" inputMode="tel" autoComplete="tel" maxLength={40} placeholder="05xx xxx xx xx"/></label>
          </div>
          <label>E-posta adresiniz  *<input name="email" type="email" required maxLength={200} inputMode="email" autoComplete="email" placeholder="ornek@gmail.com"/></label>
          <label>Size nasıl dönüş yapalım?<select name="contactPreference" defaultValue="phone" required>
            <option value="phone">Telefonla arayın</option><option value="email">E-posta ile yanıtlayın</option><option value="system">Talep ekranından yazılı yanıtlayın</option>
          </select></label>
          {purpose==='genel'&&<>
            <label>Talep konusu  *<select name="topic" required defaultValue="">
              <option value="" disabled>Konu seçiniz</option><option value="bilgi">Bilgi almak istiyorum</option>
              <option value="oneri">Önerim var</option><option value="sikayet">Şikâyet bildirmek istiyorum</option>
              <option value="diger">Diğer</option>
            </select></label>
          </>}
          {purpose==='teklif'&&<>
            <div className="support-fields-pair"><label>Ürün / ekipman adı  *<input name="productName" defaultValue={product} maxLength={180} required placeholder="Teklif istediğiniz ürün"/></label>
            <label>Ürün kodu<input name="productCode" defaultValue={code} maxLength={100} placeholder="Varsa ürün kodu"/></label></div>
            <label>İstenen adet  *<input name="quantity" type="number" min={1} max={100000} required defaultValue={1}/></label>
          </>}
          {purpose==='servis'&&<>
            <label>Servis ihtiyacınız  *<select name="serviceTopic" required defaultValue="">
              <option value="" disabled>Servis türü seçiniz</option><option value="ariza">Arıza / Onarım</option>
              <option value="bakim">Bakım</option><option value="yedek-parca">Yedek Parça</option>
              <option value="kurulum">Kurulum / Montaj</option><option value="diger">Diğer</option>
            </select></label>
            <label>İşletme adı  *<input name="businessName" maxLength={180} required minLength={2} placeholder="İşletme / firma adı"/></label>
            <label>Servis adresi  *<textarea name="address" rows={2} maxLength={1200} minLength={5} required placeholder="Açık servis adresi, il ve ilçe"/></label>
            <div className="support-fields-pair"><label>Cihaz / ürün adı<input name="productName" maxLength={180} placeholder="Cihaz modeli"/></label>
            <label>Seri numarası<input name="serialNumber" maxLength={100} placeholder="Varsa seri no"/></label></div>
            <label>Ürün kodu<input name="productCode" maxLength={100} placeholder="Varsa ürün kodu"/></label>
          </>}
          {purpose==='proje'&&<>
            <label>Proje türü  *<select name="projectType" required defaultValue="">
              <option value="" disabled>Projenizi seçiniz</option>
              <option value="restoran">Restoran / Lokanta</option><option value="otel">Otel / Konaklama</option>
              <option value="kafe">Kafe / Pastane</option><option value="mutfak">Endüstriyel mutfak kurulumu</option>
              <option value="ozel">Özel üretim / Diğer</option>
            </select></label>
            <div className="support-fields-pair"><label>Proje ili / ilçesi  *<input name="projectCity" maxLength={120} required placeholder="Örn. İstanbul / Şişli"/></label>
            <label>Yaklaşık alan / ölçü<input name="projectSize" maxLength={120} placeholder="Örn. 90 m²"/></label></div>
            <label>Planlanan zaman<select name="projectTimeline" defaultValue="belirsiz">
              <option value="belirsiz">Henüz belirlenmedi</option><option value="hemen">En kısa sürede</option>
              <option value="1-3ay">1–3 ay içinde</option><option value="3-6ay">3–6 ay içinde</option>
              <option value="6ay">6 aydan sonra</option>
            </select></label>
          </>}
          <label>{purpose==='servis'?'Sorun / ihtiyaç açıklaması':purpose==='proje'?'Projenizin detayları':purpose==='teklif'?'Teklif detayları':'Mesajınız'}  *
            <textarea name="message" required minLength={2} rows={4} maxLength={5000} placeholder={purpose==='servis'?'Cihazdaki sorunu veya ihtiyaç duyduğunuz parçayı açıklayın.':purpose==='proje'?'İhtiyacınızı, mutfak ekipmanlarını ve özel taleplerinizi anlatın.':'Talebinizi ayrıntılarıyla yazın.'}/></label>
          <input name="website" tabIndex={-1} autoComplete="off" className="honeypot" aria-hidden="true"/>
          {error&&<p className="support-error" role="alert">{error}</p>}
          <button className="button support-submit" type="submit" disabled={sending}><Send size={18}/>{sending?'Talebiniz gönderiliyor…':'Talebimi gönder'}<ArrowRight size={18}/></button>
          <p className="support-privacy"><ShieldCheck size={14}/> Bilgileriniz yalnızca talebinizle ilgili iletişim için kullanılır.</p>
        </form>}
      </div>
      <aside className="support-info" aria-label="Diğer iletişim yolları">
        <div className="support-info-heading"><span>DOĞRUDAN İLETİŞİM</span><h2>Biz buradayız.</h2><p>Form doldurmak istemezseniz aşağıdaki kanallardan da ulaşabilirsiniz.</p></div>
        <div className="contact-quick-list">
          {s.phone&&<a href={'tel:'+s.phone}><Phone/><span><small>TELEFON</small><strong>{s.phone}</strong></span><ArrowRight className="support-info-arrow"/></a>}
          {s.email&&<a href={'mailto:'+s.email}><Mail/><span><small>E-POSTA</small><strong>{s.email}</strong></span><ArrowRight className="support-info-arrow"/></a>}
          {s.address&&<a href={'https://www.google.com/maps/search/?api=1&query='+encodeURIComponent(s.address)} target="_blank" rel="noreferrer"><MapPin/><span><small>ADRES</small><strong>{s.address}</strong></span><ArrowRight className="support-info-arrow"/></a>}
          {s.hours&&<div><Clock3/><span><small>ÇALIŞMA SAATLERİ</small><strong>{s.hours}</strong></span></div>}
        </div>
        <a className="support-track" href="/talep-sorgula"><Search size={19}/><span><strong>Mevcut talebinizi takip edin</strong><small>Talep numaranızla sorgulayın</small></span><ArrowRight size={17}/></a>
      </aside>
    </div>
  </section>;
}
