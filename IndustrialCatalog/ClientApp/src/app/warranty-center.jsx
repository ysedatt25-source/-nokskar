import {useEffect,useRef} from 'react';
import {initializeWarranty} from './warranty-query';
import './warranty-center.css';

export default function WarrantyCenter(){
 const root=useRef(null);
 useEffect(()=>initializeWarranty(root.current),[]);
 return <div className="warranty-center" ref={root}><div className="warranty-page"><section className="warranty-intro wrap"><p className="eyebrow">İNOKSKAR SATIŞ SONRASI HİZMETLER</p><h1>Garanti &amp; Servis Merkezi</h1><p>Ürününüzün garanti kaydını iki aşamalı doğrulama ile kontrol edin. Seri numarası ve garanti doğrulama kodu birlikte gereklidir.</p></section>
<section className="wrap warranty-layout"><div className="warranty-query-card"><div className="warranty-query-head"><span className="warranty-shield">✓</span><div><h2>Garanti kaydınızı doğrulayın</h2><p>Her iki bilgiyi ürününüzün garanti belgesinde yer aldığı şekilde girin.</p></div></div><form id="warranty-query-form" className="warranty-form"><label>Seri Numarası<input name="serialNumber" maxLength="100" autoComplete="off" required placeholder="Seri numaranızı girin"/></label><label>Garanti Doğrulama Kodu<input name="verificationCode" maxLength="32" autoComplete="off" required placeholder="Örn. K7P4M-9X2QD"/></label><details className="warranty-help"><summary>Bu bilgiler nerede?</summary><p>Seri numarası ve garanti doğrulama kodu ürününüzün garanti belgesinde yer alır.</p></details><button className="button" type="submit">Garantiyi sorgula</button><p className="warranty-privacy">Güvenlik için yalnız seri numarası yeterli değildir. Yanlış girişlerde hangi bilginin hatalı olduğu açıklanmaz.</p></form></div><div id="warranty-result" className="warranty-result" aria-live="polite"><div className="warranty-placeholder"><strong>Garanti bilgileri burada görüntülenecek.</strong><span>Teslim tarihi, bitiş tarihi, kalan gün ve bileşen bazlı kapsam tek ekranda gösterilir.</span></div></div></section></div></div>;
}
