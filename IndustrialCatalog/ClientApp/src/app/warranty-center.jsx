import {useEffect,useRef} from 'react';
import {initializeWarranty} from './warranty-query';
import './warranty-center.css';

const warrantyMarkup = `<div class="warranty-page"><section class="warranty-intro wrap"><p class="eyebrow">İNOKSKAR SATIŞ SONRASI HİZMETLER</p><h1>Garanti &amp; Servis Merkezi</h1><p>Ürününüzün garanti kaydını iki aşamalı doğrulama ile kontrol edin. Seri numarası ve garanti doğrulama kodu birlikte gereklidir.</p></section>
<section class="wrap warranty-layout"><div class="warranty-query-card"><div class="warranty-query-head"><span class="warranty-shield">✓</span><div><h2>Garanti kaydınızı doğrulayın</h2><p>Her iki bilgiyi ürününüzün garanti belgesinde yer aldığı şekilde girin.</p></div></div><form id="warranty-query-form" class="warranty-form"><label>Seri Numarası<input name="serialNumber" maxlength="100" autocomplete="off" required placeholder="Seri numaranızı girin"></label><label>Garanti Doğrulama Kodu<input name="verificationCode" maxlength="32" autocomplete="off" required placeholder="Örn. K7P4M-9X2QD"></label><details class="warranty-help"><summary>Bu bilgiler nerede?</summary><p>Seri numarası ve garanti doğrulama kodu ürününüzün garanti belgesinde yer alır.</p></details><button class="button" type="submit">Garantiyi sorgula</button><p class="warranty-privacy">Güvenlik için yalnız seri numarası yeterli değildir. Yanlış girişlerde hangi bilginin hatalı olduğu açıklanmaz.</p></form></div><div id="warranty-result" class="warranty-result" aria-live="polite"><div class="warranty-placeholder"><strong>Garanti bilgileri burada görüntülenecek.</strong><span>Teslim tarihi, bitiş tarihi, kalan gün ve bileşen bazlı kapsam tek ekranda gösterilir.</span></div></div></section></div>`;
export default function WarrantyCenter(){
 const root=useRef(null);
 useEffect(()=>initializeWarranty(root.current),[]);
 return <div className="warranty-center" ref={root} dangerouslySetInnerHTML={{__html:warrantyMarkup}}/>;
}
