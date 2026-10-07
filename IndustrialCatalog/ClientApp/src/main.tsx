import React,{Suspense} from 'react';
import {createRoot} from 'react-dom/client';
import Storefront from './app/storefront';
import './app/globals.css';
import './app/spa-shell.css';
const Admin=React.lazy(()=>import('./app/admin/panel'));
class AppErrorBoundary extends React.Component<React.PropsWithChildren,{failed:boolean}>{
 state={failed:false};static getDerivedStateFromError(){return {failed:true};}
 componentDidCatch(error:unknown){console.error('İnokskar arayüz hatası',error);}
 render(){if(!this.state.failed)return this.props.children;return <main className="load-state"><h1>Sayfa görüntülenemedi</h1><p>Arayüz yüklenirken beklenmeyen bir hata oluştu.</p><div className="hero-actions"><a className="button" href="/">Ana sayfaya dön</a><button className="button secondary" onClick={()=>location.reload()}>Tekrar yükle</button></div></main>;}
}
function LoadingShell(){return <main className="load-state state-loading" aria-live="polite"><div className="state-panel"><span className="state-kicker">İNOKSKAR</span><h1>Çalışma alanı hazırlanıyor</h1><p>Yetkileriniz ve yönetim araçları yükleniyor.</p><div className="state-skeleton" aria-hidden="true"><span/><span/><span/></div></div></main>;}
const app=location.pathname==='/admin'?<Suspense fallback={<LoadingShell/>}><Admin/></Suspense>:<Storefront/>;
createRoot(document.getElementById('root')!).render(<AppErrorBoundary>{app}</AppErrorBoundary>);
