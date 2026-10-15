export default function GalleryDots({index,total}:{index:number;total:number}){
 if(total<2)return null;
 return <span className="gallery-swipe-dots" role="status" aria-label={`Görsel ${index+1} / ${total}. Diğer görseller için kaydırın.`}>{[0,1,2].map(dot=><span key={dot} className={dot===index%3?'active':''} aria-hidden="true"/>)}</span>;
}
