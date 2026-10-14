import {useEffect,useRef,useState} from 'react';
import type {Catalog} from '../lib/model';
import {selectHero} from '../lib/hero-rotation';
const previousKey='inokskar.hero.previous';
const founderKey='inokskar.founder.intro.seen';
const memory=new Map<string,string>();
function read(key:string){try{return sessionStorage.getItem(key)||memory.get(key)||'';}catch{return memory.get(key)||'';}}
function write(key:string,value:string){memory.set(key,value);try{sessionStorage.setItem(key,value);}catch{}}
export default function HomeHero({settings:s}:{settings:Catalog['settings']}){
 const [heroUrl]=useState(()=>selectHero(s.heroImages,read(previousKey))||s.hero||'/hero.png');
 const [firstVisit]=useState(()=>read(founderKey)!=='1');
 const [mediaReady,setMediaReady]=useState(false);
 const image=useRef<HTMLImageElement>(null);
 const video=/\.mp4(?:[?#]|$)/i.test(heroUrl);
 useEffect(()=>{write(previousKey,heroUrl);if(image.current?.complete)setMediaReady(true);},[heroUrl]);
 useEffect(()=>{if(mediaReady)write(founderKey,'1');},[mediaReady]);
 return <section className={s.heroHeading.trim()||s.heroAccent.trim()?'hero':'hero hero-media-only'}>
 {s.founderLabelVisible&&s.founderLabelText.trim()&&<div className={'hero-founder-label'+(firstVisit?(mediaReady?' founder-intro':' founder-awaiting-media'):'')} aria-label="Kuruluş bilgisi"><span className="hero-founder-label-mark" aria-hidden="true"/><span className="hero-founder-label-text" title={s.founderLabelText}>{s.founderLabelText}</span></div>}
 {(s.heroHeading.trim()||s.heroAccent.trim())&&<div className="hero-content"><div className="hero-kicker"><span/> ENDÜSTRİYEL ÇÖZÜMLER</div><h1>{s.heroHeading.trim()&&<span style={{whiteSpace:'pre-line'}}>{s.heroHeading.trim()}</span>}{s.heroHeading.trim()&&s.heroAccent.trim()&&<br/>}{s.heroAccent.trim()&&<em style={{whiteSpace:'pre-line'}}>{s.heroAccent.trim()}</em>}</h1><p>{s.heroText}</p><div className="hero-actions"><a href="/iletisim?amac=teklif" className="button gold">Teklif Al</a></div></div>}
 <div className="hero-visual">{video?<video className="hero-image hero-media-video" src={heroUrl} autoPlay muted loop playsInline preload="metadata" onLoadedData={()=>setMediaReady(true)} onError={()=>setMediaReady(true)} aria-label="İnokskar ana sayfa videosu"/>:<img ref={image} className="hero-image" src={heroUrl} alt="İnokskar ana sayfa görseli" decoding="async" fetchPriority="high" onLoad={()=>setMediaReady(true)} onError={()=>setMediaReady(true)}/>}</div>
 </section>;
}
