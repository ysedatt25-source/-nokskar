import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../../',import.meta.url));
const read=p=>readFileSync(root+p,'utf8');
const checks=[
 ['IndustrialCatalog/ClientApp/index.html',text=>!text.includes('inokskar-app-runtime.js'),'SPA must not load the legacy consolidated runtime.'],
 ['IndustrialCatalog/ClientApp/src/app/globals.css',text=>!text.includes('grid-auto-columns:minmax(230px,78vw)'),'Legacy 78vw mobile category rail must not return.'],
 ['IndustrialCatalog/ClientApp/src/app/globals.css',text=>!text.includes('body.storefront-layer-open{overflow:hidden'),'Storefront scroll locking must remain React-owned.'],
 ['IndustrialCatalog/ClientApp/src/app/storefront.tsx',text=>text.includes('storefront-scroll-locked')&&text.includes('spa-bottom-nav')&&text.includes('spa-category-layer'),'Public SPA shell must stay React-owned.'],
 ['IndustrialCatalog/ClientApp/src/app/admin/panel.tsx',text=>text.includes('admin-app-dock')&&text.includes('admin-inquiry-badge')&&text.includes('admin-mobile-toggle'),'Admin navigation and unread badge must stay React-owned.'],
 ['IndustrialCatalog/ClientApp/public/r13-pages.js',text=>text.includes('__inokskarSetScrollLock'),'Server-rendered overlays must use the shared scroll-lock registry.'],
 ['IndustrialCatalog/ClientApp/public/r13-pages.js',text=>(text.match(/MutationObserver/g)||[]).length<=2,'Server runtime must not restore broad DOM observers.'],
 ['IndustrialCatalog/ClientApp/public/r13-pages.js',text=>!text.includes('setInterval('),'Server runtime must not restore background polling.']
];
const failures=[];
for(const [path,test,message] of checks){const text=read(path);if(!test(text))failures.push(path+' :: '+message);}
if(failures.length){console.error('UI architecture contract failed:');for(const f of failures)console.error(' - '+f);process.exit(1);}
console.log('UI architecture contract passed.');
