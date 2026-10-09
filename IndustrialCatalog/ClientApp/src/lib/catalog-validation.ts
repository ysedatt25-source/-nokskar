import {z} from 'zod';
import {safeLink,productCode} from './model';
const text=z.string();
const name=text.trim().min(1,'Ad alanı boş bırakılamaz.');
const id=text.min(1).regex(/^[a-zA-Z0-9_-]+$/,'Kayıt kimliği geçersiz.');
const asset=text.refine(v=>!v||safeLink(v),'Görsel veya dosya bağlantısı geçersiz.');
const number=z.number().finite().nonnegative();
export const catalogSchema=z.object({
 settings:z.object({name,tagline:text,heroTitle:text,heroHeading:text.max(160),heroAccent:text.max(160),heroText:text,hero:asset,headerImage:asset,founderLabelText:text.max(64,'Kuruluş etiketi en fazla 64 karakter olabilir.').default('BİR YİĞİTKAR DEKOR KURULUŞUDUR'),founderLabelVisible:z.boolean().default(true),phone:text,whatsapp:text.refine(v=>!v||(/^\+?[\d\s()-]+$/.test(v)&&v.replace(/\D/g,'').length>=8&&v.replace(/\D/g,'').length<=15),'WhatsApp numarasını ülke koduyla girin.'),email:text.refine(v=>!v||z.string().email().safeParse(v).success,'E-posta adresi geçersiz.'),notificationEmail:text.trim().max(254,'Bildirim e-postası çok uzun.').refine(v=>!v||z.string().email().safeParse(v).success,'Talep bildirimi için geçerli bir e-posta adresi girin.'),address:text,hours:text,social:asset,waMessage:text,waEnabled:z.boolean(),rate:number,autoRate:z.boolean(),lastRate:text,rateSource:text.default(''),rateDate:text.default(''),about:text,menu:z.array(z.object({name,url:text.refine(safeLink,'Menü bağlantısı geçersiz.')}))}),
 categories:z.array(z.object({id,name,parent:text,description:text,image:asset,menu:z.boolean(),visible:z.boolean(),seoTitle:text.max(70).default(''),seoDescription:text.max(180).default('')})),
 products:z.array(z.object({id,code:text.trim().max(100,'Ürün kodu en fazla 100 karakter olabilir.').default(''),name,category:id,euro:number,tl:number.refine(v=>Number.isInteger(v),'TL fiyatı tam TL olmalı.'),vatRate:number.max(100,'KDV oranı en fazla %100 olabilir.').default(20),description:text,after:text,specs:z.array(z.object({name:text,value:text})),images:z.array(asset),pdf:asset,status:z.enum(['Stokta','Sipariş üzerine','Bilgi alınız']),visible:z.boolean(),demo:z.boolean(),seoTitle:text.max(70).default(''),seoDescription:text.max(180).default('')})),
 references:z.array(z.object({id,logo:asset.refine(v=>!!v,'Referans logosu zorunlu.'),phone:text.max(50).refine(v=>!v||/^\+?[\d\s().-]{8,50}$/.test(v),'Telefon numarası geçersiz.'),email:text.max(254).refine(v=>!v||z.string().email().safeParse(v).success,'Referans e-postası geçersiz.'),visible:z.boolean()})).max(250,'En fazla 250 referans eklenebilir.')
});
export function validateCatalog(input:unknown){
 const result=catalogSchema.safeParse(input);
 if(!result.success)throw new Error('İçerik doğrulanamadı: '+result.error.issues[0].path.join(' / ')+' — '+result.error.issues[0].message);
 const data=result.data;
 for(const list of [data.categories,data.products,data.references])if(new Set(list.map(x=>x.id)).size!==list.length)throw new Error('Tekrarlanan kayıt kimliği.');
 if(new Set(data.products.map(p=>productCode(p).toLowerCase())).size!==data.products.length)throw new Error('Ürün kodu başka bir üründe kullanılıyor.');
 const cats=new Map(data.categories.map(c=>[c.id,c]));
 for(const c of data.categories){const seen=new Set([c.id]);let parent=c.parent;while(parent){if(seen.has(parent))throw new Error('Kategori kendisinin veya alt kategorisinin içine taşınamaz.');seen.add(parent);const node=cats.get(parent);if(!node)throw new Error('Üst kategori bulunamadı.');parent=node.parent;}}
 if(data.products.some(p=>!cats.has(p.category)))throw new Error('Ürünün kategorisi bulunamadı.');
 return data;
}
