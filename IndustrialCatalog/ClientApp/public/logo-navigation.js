/* One logo mirror pass per page/route; no timed repeats. */
(() => {
  if (window.__inokskarLogoNavigation) return;
  window.__inokskarLogoNavigation = true;
  const start = () => {
    const style = document.createElement('style');
    style.textContent = `.logo-mirror-host{isolation:isolate;overflow:hidden}.logo-mirror-host::after{content:"";position:absolute;pointer-events:none;z-index:9;top:-20%;left:-45%;width:30%;height:140%;background:linear-gradient(100deg,transparent,rgba(255,255,255,.12),rgba(255,255,255,.85),rgba(255,255,255,.12),transparent);transform:skewX(-20deg);opacity:0}.logo-mirror-host.logo-mirror-active::after{animation:logo-mirror-sweep .85s ease-out both}@keyframes logo-mirror-sweep{0%{transform:skewX(-20deg) translateX(0);opacity:0}12%,85%{opacity:1}100%{transform:skewX(-20deg) translateX(550%);opacity:0}}@media(prefers-reduced-motion:reduce){.logo-mirror-host::after{display:none}}`;
    document.head.append(style);
    const selector = 'img[src*="inokskar-header-brand"],.site-header .brand img,.private-brand-link img,.admin-brand img,.admin-sidebar>a img,.r126-auth .brand img,.r126-auth .mobile-brand img,.shell>a img,.account-brand img';
    const hosts = new Set();
    const pendingImages = new WeakSet();
    const route = () => location.pathname + location.search;
    const sweep = host => {
      if (host.dataset.logoMirrorRoute === route()) return;
      host.dataset.logoMirrorRoute = route();
      host.classList.remove('logo-mirror-active');
      void host.offsetWidth;
      host.classList.add('logo-mirror-active');
    };
    const mount = () => {
      for (const img of document.querySelectorAll(selector)) {
        if (!img.complete || !img.naturalWidth) {
          if (!pendingImages.has(img)) {
            pendingImages.add(img);
            img.addEventListener('load', mount, {once:true});
          }
          continue;
        }
        const host = img.parentElement;
        host.classList.add('logo-mirror-host');
        if (getComputedStyle(host).position === 'static') host.style.position = 'relative';
        hosts.add(host);
        sweep(host);
      }
      for (const host of hosts) if (!host.isConnected) hosts.delete(host);
    };
    let lastRoute = route();
    const navigation = () => {
      if (route() === lastRoute) return;
      lastRoute = route();
      mount();
    };
    for (const method of ['pushState', 'replaceState']) {
      const original = history[method];
      history[method] = function (...args) {
        const result = original.apply(this, args);
        navigation();
        return result;
      };
    }
    new MutationObserver(mount).observe(document.body, {childList:true,subtree:true});
    window.addEventListener('popstate', navigation);
    window.addEventListener('inokskar:navigation', navigation);
    window.addEventListener('pageshow', event => {
      if (event.persisted) {
        for (const host of hosts) delete host.dataset.logoMirrorRoute;
        mount();
      }
    });
    mount();
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, {once:true});
  else start();
})();
