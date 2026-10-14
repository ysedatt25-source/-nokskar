import {useEffect} from 'react';

declare global { interface Window { InokskarLoading?: {begin:()=>void;ready:()=>void}; } }
export default function BrandLoading(){
 useEffect(()=>{window.InokskarLoading?.begin();},[]);
 return <div className="brand-loading" role="status" aria-label="Sayfa yükleniyor"><div className="page-loading-brand"><div className="page-loading-mark"><img className="page-loading-logo" src="/inokskar-logo.svg" alt="İNOKSKAR"/></div><span className="page-loading-track" aria-hidden="true"/><span className="page-loading-caption" aria-hidden="true">Yükleniyor</span></div></div>;
}
