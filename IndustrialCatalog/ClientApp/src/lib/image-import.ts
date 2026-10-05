import {Product,productCode} from './model';
export type ImageMatch={file:File,productId:string|null,productName:string,code:string,error:string};
export class UploadSessionExpired extends Error {}
export async function transferImages(plan:ImageMatch[],upload:(file:File)=>Promise<string>,progress:(done:number)=>void){
 const additions=new Map<string,string[]>(),pending:ImageMatch[]=plan.filter(x=>!x.productId||x.error);
 const matches=plan.filter(x=>x.productId&&!x.error);let total=0;
 for(let i=0;i<matches.length;i++){
  const row=matches[i];
  try{
   const url=await upload(row.file);
   additions.set(row.productId!,[...(additions.get(row.productId!)||[]),url]);total++;
  }catch(e){
   const message=e instanceof Error?e.message:'Dosya yüklenemedi.';
   pending.push({...row,error:message});
   if(e instanceof UploadSessionExpired){
    pending.push(...matches.slice(i+1).map(x=>({...x,error:'Oturum açtıktan sonra yeniden deneyin.'})));
    progress(i+1);break;
   }
  }
  progress(i+1);
 }
 return {additions,pending,total,failed:pending.filter(x=>x.productId&&x.error).length};
}
const normalize=(s:string)=>s.trim().normalize('NFC').toLowerCase();
export function planImages(files:File[],products:Product[]):ImageMatch[]{
 return [...files].sort((a,b)=>a.name.localeCompare(b.name,'tr',{numeric:true})).map(file=>{
  const base=file.name.replace(/\.[^.]+$/,'');
  if(!/\.(png|jpe?g|webp)$/i.test(file.name))return {file,productId:null,productName:'',code:'',error:'PNG, JPEG veya WebP seçin.'};
  if(file.size===0||file.size>20*1024*1024)return {file,productId:null,productName:'',code:'',error:'Dosya boş veya 20 MB sınırını aşıyor.'};
  const candidates=products.filter(p=>normalize(base)===normalize(productCode(p))||normalize(base.replace(/__\d+$/,''))===normalize(productCode(p)));
  if(candidates.length!==1)return {file,productId:null,productName:'',code:'',error:candidates.length?'Birden fazla kodla eşleşiyor; dosya adını değiştirin.':'Ürün koduyla eşleşmedi.'};
  const p=candidates[0];return {file,productId:p.id,productName:p.name,code:productCode(p),error:''};
 });
}
