import {useRef,useState} from 'react';
import {ChevronLeft,ChevronRight} from 'lucide-react';
import {Dialog,DialogContent,DialogHeader,DialogTitle,DialogDescription} from '@/components/ui/dialog';
import type {Product} from '../lib/model';
import {productCode} from '../lib/model';

export default function ProductGallery({product}:{product:Product}){
 const [index,setIndex]=useState(0),[expanded,setExpanded]=useState(false);
 const images=product.images??[];
 const gesture=useRef<number|null>(null),swiped=useRef(false);
 const move=(delta:number)=>setIndex(value=>(value+delta+images.length)%images.length);
 const arrows=(large=false)=>images.length>1&&<><button type="button" className={large?'gallery-viewer-prev':'detail-gallery-arrow detail-gallery-prev'} onClick={()=>move(-1)} aria-label="Önceki görsel"><ChevronLeft size={24}/></button><button type="button" className={large?'gallery-viewer-next':'detail-gallery-arrow detail-gallery-next'} onClick={()=>move(1)} aria-label="Sonraki görsel"><ChevronRight size={24}/></button></>;
 return <div className="detail-gallery">
  <div className="detail-photo detail-gallery-frame" onPointerDown={e=>{gesture.current=e.clientX;swiped.current=false;}} onPointerUp={e=>{const start=gesture.current;gesture.current=null;if(start!==null&&images.length>1&&Math.abs(e.clientX-start)>35){swiped.current=true;move(e.clientX>start?-1:1);}}} onPointerCancel={()=>{gesture.current=null;}}>
   <div className="detail-gallery-media">{images.length?<button className="gallery-expand-target" type="button" onClick={()=>{if(swiped.current){swiped.current=false;return;}setExpanded(true);}} aria-label={product.name+' görselini büyüt'}><img src={images[index]} alt={product.name+' — görsel '+(index+1)} decoding="async"/></button>:<div className="photo-empty product-photo-fallback product-photo-fallback-detail"><img src="/inokskar-logo.svg" alt=""/><strong>{product.name}</strong><small>{productCode(product)}</small></div>}</div>
   {arrows()}{images.length>1&&<span className="detail-gallery-count" aria-live="polite">{index+1}/{images.length}</span>}
  </div>
  {images.length>1&&<div className="thumbnails detail-thumbnail-rail" aria-label="Ürün görselleri">{images.map((src,i)=><button key={i} className={i===index?'selected':''} onClick={()=>setIndex(i)} aria-pressed={i===index} aria-label={'Görsel '+(i+1)}><img src={src} alt="" loading="lazy"/></button>)}</div>}
  <Dialog open={expanded} onOpenChange={setExpanded}><DialogContent className="product-image-viewer translate-x-0 translate-y-0" onKeyDown={e=>{if(images.length>1&&['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();move(e.key==='ArrowLeft'?-1:1);}}}><DialogHeader><DialogTitle>{product.name}</DialogTitle><DialogDescription>Görsel {index+1} / {images.length}. Ok tuşlarıyla diğer görsellere geçebilirsiniz.</DialogDescription></DialogHeader><div className="gallery-viewer-stage"><img src={images[index]} alt={product.name+' — görsel '+(index+1)}/>{arrows(true)}</div></DialogContent></Dialog>
 </div>;
}
