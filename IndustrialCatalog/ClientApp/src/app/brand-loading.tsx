import {useEffect} from 'react';

declare global { interface Window { InokskarLoading?: {begin:()=>void;ready:()=>void}; } }
export default function BrandLoading(){
 useEffect(()=>{window.InokskarLoading?.begin();},[]);
 return <div className="brand-loading" role="status" aria-label="Sayfa yükleniyor"><img className="page-loading-logo" src="/inokskar-logo.svg" alt="İNOKSKAR"/></div>;
}
