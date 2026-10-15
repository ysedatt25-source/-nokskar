import GalleryDots from './gallery-dots';
import {useEffect,useRef,useState} from 'react';
import {ChevronLeft,ChevronRight,Video} from 'lucide-react';
import {Dialog,DialogContent,DialogHeader,DialogTitle,DialogDescription} from '@/components/ui/dialog';
import type {Product} from '../lib/model';
import {productCode} from '../lib/model';
import {mediaUrl,imageSources} from '../lib/media';

export default function ProductGallery({product}:{product:Product}){
 const [index,setIndex]=useState(0),[expanded,setExpanded]=useState(false);
 const images=product.images??[],total=images.length+(product.video?1:0);
 const isVideo=!!product.video&&index===images.length;
 const gestureY=useRef(0);const gesture=useRef<number|null>(null),swiped=useRef(false);
 const move=(delta:number)=>setIndex(value=>total?(value+delta+total)%total:0);
 const arrows=(large=false)=>total>1&&<><button type="button" className={large?'gallery-viewer-prev':'detail-gallery-arrow detail-gallery-prev'} onClick={()=>move(-1)} aria-label="Önceki medya"><ChevronLeft size={24}/></button><button type="button" className={large?'gallery-viewer-next':'detail-gallery-arrow detail-gallery-next'} onClick={()=>move(1)} aria-label="Sonraki medya"><ChevronRight size={24}/></button></>;
 const video=(large=false)=><GalleryVideo key={(large?'large':'inline')+product.video} src={mediaUrl(product.video,'stream')} poster={mediaUrl(product.video,'poster')} active={large?expanded:!expanded} name={product.name}/>;
 return <div className="detail-gallery">
  <div className="detail-photo detail-gallery-frame" onPointerDown={e=>{gestureY.current=e.clientY;gesture.current=e.clientX;swiped.current=false;}} onPointerUp={e=>{const start=gesture.current;gesture.current=null;if(start!==null&&total>1&&Math.abs(e.clientX-start)>35&&Math.abs(e.clientX-start)>Math.abs(e.clientY-gestureY.current)){swiped.current=true;move(e.clientX>start?-1:1);}}} onPointerCancel={()=>{gesture.current=null;}}>
   <div className="detail-gallery-media" onDragStart={e=>e.preventDefault()}>{isVideo?video():images.length?<button className="gallery-expand-target" type="button" onClick={()=>{if(swiped.current){swiped.current=false;return;}setExpanded(true);}} aria-label={product.name+' görselini büyüt'}><img src={mediaUrl(images[index],'detail')} srcSet={imageSources(images[index])} sizes="(max-width: 768px) 100vw, 650px" alt={product.name+' — görsel '+(index+1)} decoding="async" fetchPriority="high"/></button>:<div className="photo-empty product-photo-fallback product-photo-fallback-detail"><img src="/inokskar-logo.svg" alt=""/><strong>{product.name}</strong><small>{productCode(product)}</small></div>}</div>
   {product.picturedLength&&!isVideo&&<span className="pictured-product-size"><span>Görseldeki ürün</span><strong>{product.picturedLength.replace('.',',')} metre</strong></span>}{arrows()}<GalleryDots index={index} total={total}/>{total>1&&<span className="detail-gallery-count" aria-live="polite">{isVideo?'Video':`${index+1}/${total}`}</span>}
  </div>
  {(total>1||product.video)&&<div className="thumbnails detail-thumbnail-rail" aria-label="Ürün görselleri ve videosu">{images.map((src,i)=><button key={i} className={i===index?'selected':''} onClick={()=>setIndex(i)} aria-pressed={i===index} aria-label={'Görsel '+(i+1)}><img src={mediaUrl(src,'thumb')} alt="" loading="lazy" decoding="async"/></button>)}{product.video&&<button type="button" className={'product-video-thumbnail '+(isVideo?'selected':'')} onClick={()=>setIndex(images.length)} aria-pressed={isVideo} aria-label="Ürün videosunu göster"><img src={mediaUrl(product.video,'poster')} alt="" loading="lazy" decoding="async" onError={e=>{if(images[0]&&e.currentTarget.getAttribute('src')!==mediaUrl(images[0],'thumb'))e.currentTarget.src=mediaUrl(images[0],'thumb')!;}}/><span className="product-video-label"><Video size={14}/> Video</span></button>}</div>}
  <Dialog open={expanded} onOpenChange={setExpanded}><DialogContent className="product-image-viewer translate-x-0 translate-y-0" onKeyDown={e=>{if(!isVideo&&total>1&&['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();move(e.key==='ArrowLeft'?-1:1);}}}><DialogHeader><DialogTitle>{product.name}</DialogTitle><DialogDescription>{isVideo?'Ürün videosu':`Görsel ${index+1} / ${total}. Diğer görseller için kaydırın veya okları kullanın.`}</DialogDescription></DialogHeader><div className="gallery-viewer-stage" onDragStart={e=>e.preventDefault()} onPointerDown={e=>{gestureY.current=e.clientY;gesture.current=e.clientX;}} onPointerUp={e=>{const start=gesture.current;gesture.current=null;if(start!==null&&total>1&&Math.abs(e.clientX-start)>35&&Math.abs(e.clientX-start)>Math.abs(e.clientY-gestureY.current))move(e.clientX>start?-1:1);}} onPointerCancel={()=>{gesture.current=null;}}>{isVideo?video(true):<img src={images[index]} alt={product.name+' — görsel '+(index+1)}/>}<span className="gallery-media-navigation">{arrows(true)}</span><GalleryDots index={index} total={total}/></div></DialogContent></Dialog>
 </div>;
}

function GalleryVideo({src,poster,active,name}:{src?:string;poster?:string;active:boolean;name:string}){
 const ref=useRef<HTMLVideoElement>(null);
 useEffect(()=>{const player=ref.current;if(!player)return;if(active){player.play().catch(()=>{/* Native controls remain available if the browser blocks autoplay. */});}else player.pause();return()=>player.pause();},[active,src]);
 return <video ref={ref} className="product-gallery-video" src={src} poster={poster} controls autoPlay={active} muted playsInline preload="none" aria-label={name+' ürün videosu'}>Tarayıcınız video oynatmayı desteklemiyor.</video>;
}
