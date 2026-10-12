import React from 'react';
import {createRoot} from 'react-dom/client';
import './app/globals.css';
import BrandLoading from './app/brand-loading';
const Storefront=React.lazy(()=>import('./app/storefront'));
const Admin=React.lazy(()=>import('./app/admin/panel'));

class AppErrorBoundary extends React.Component<React.PropsWithChildren, {failed:boolean}> {
  state={failed:false};
  static getDerivedStateFromError(){return {failed:true};}
  componentDidCatch(error:unknown){window.InokskarLoading?.ready();console.error('İnokskar arayüz hatası',error);}
  render(){
    if(!this.state.failed)return this.props.children;
    const productPage=location.pathname.startsWith('/urun/');
    return <main className="load-state"><h1>Sayfa görüntülenemedi</h1><p>{productPage?'Ürün detayı yüklenirken beklenmeyen bir görüntüleme hatası oluştu. Ürün verileri korunur.':'Arayüz yüklenirken beklenmeyen bir hata oluştu.'}</p><div className="hero-actions"><a className="button" href={productPage?'/urunler':'/'}>{productPage?'Ürünlere dön':'Ana sayfaya dön'}</a><button className="button secondary" onClick={()=>location.reload()}>Tekrar yükle</button></div></main>;
  }
}

createRoot(document.getElementById('root')!).render(<AppErrorBoundary><React.Suspense fallback={<BrandLoading/>}>{location.pathname==='/admin'?<Admin/>:<Storefront/>}</React.Suspense></AppErrorBoundary>);
