import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const axePath = require.resolve('axe-core/axe.min.js');
const BASE = process.env.QA_BASE_URL || 'https://inokskar-web-preview.up.railway.app';
const OUT = process.env.QA_OUTPUT || 'qa-output';
fs.mkdirSync(OUT,{recursive:true});
fs.mkdirSync(path.join(OUT,'screenshots'),{recursive:true});

const devices = [
  {name:'mobile', width:390, height:844, isMobile:true, hasTouch:true},
  {name:'tablet', width:820, height:1180, isMobile:true, hasTouch:true},
  {name:'desktop', width:1440, height:1000, isMobile:false, hasTouch:false}
];
const seedPaths = ['/', '/urunler', '/iletisim', '/servis-talebi', '/talep-sorgula', '/garanti-sorgulama', '/login', '/kayit', '/forgot-password', '/hesabim', '/gizlilik', '/kvkk', '/cerez', '/admin'];
const skipPrefixes = ['/api/','/logout','/admin/inquiries/','/admin/warranties/','/teknik/cihaz/'];
const skipExact = new Set(['/logout']);
const normalizeUrl=(href)=>{
  try{
    const u=new URL(href,BASE);
    if(u.origin!==new URL(BASE).origin)return null;
    if(!['http:','https:'].includes(u.protocol))return null;
    if(skipExact.has(u.pathname)||skipPrefixes.some(p=>u.pathname.startsWith(p)))return null;
    u.hash='';
    const allowedQuery = new URLSearchParams();
    if(u.pathname==='/urunler' && u.searchParams.get('q')) allowedQuery.set('q',u.searchParams.get('q'));
    u.search=allowedQuery.toString()?('?'+allowedQuery.toString()):'';
    return u.pathname+u.search;
  }catch{return null;}
};

const browser = await chromium.launch({headless:true});
const discovery = await browser.newPage({viewport:{width:1280,height:900}});
const queue=[...seedPaths], seen=new Set(), discovered=[];
while(queue.length && discovered.length<40){
  const rel=queue.shift();
  if(!rel||seen.has(rel))continue;
  seen.add(rel);
  try{
    const r=await discovery.goto(BASE+rel,{waitUntil:'domcontentloaded',timeout:45000});
    await discovery.waitForTimeout(700);
    if(!r || r.status()>=500) continue;
    discovered.push(rel);
    const hrefs=await discovery.locator('a[href]').evaluateAll(as=>as.map(a=>a.getAttribute('href')));
    const normalized=[...new Set(hrefs.map(normalizeUrl).filter(Boolean))];
    for(const p of normalized){
      if(seen.has(p)||queue.includes(p))continue;
      if(/^\/kategori\//.test(p) && [...seen].filter(x=>x.startsWith('/kategori/')).length>=5) continue;
      if(/^\/urun\//.test(p) && [...seen].filter(x=>x.startsWith('/urun/')).length>=4) continue;
      if(/^\/magaza\//.test(p) && [...seen].filter(x=>x.startsWith('/magaza/')).length>=3) continue;
      queue.push(p);
    }
  }catch{}
}
await discovery.close();

const routes=[...new Set([...seedPaths,...discovered])].slice(0,44);
const report={base:BASE,generatedAt:new Date().toISOString(),routes,devices:devices.map(d=>d.name),results:[],summary:{}};
const sanitize=s=>s.replace(/^\//,'').replace(/[^a-z0-9]+/gi,'-').replace(/^-|-$/g,'')||'home';

for(const device of devices){
  const context=await browser.newContext({viewport:{width:device.width,height:device.height},isMobile:device.isMobile,hasTouch:device.hasTouch,deviceScaleFactor:1,locale:'tr-TR'});
  for(const rel of routes){
    const page=await context.newPage();
    const errors=[], failed=[], badResponses=[];
    page.on('console',msg=>{ if(msg.type()==='error') errors.push(msg.text().slice(0,500)); });
    page.on('pageerror',err=>errors.push(String(err).slice(0,500)));
    page.on('requestfailed',req=>{
      const error=req.failure()?.errorText||'failed';
      // Optional profile prefill is canceled when the audit navigates/closes
      // the contact page; this is not a failed user submission.
      if(error==='net::ERR_ABORTED'&&req.url().includes('/api/customer/profile'))return;
      failed.push({url:req.url(),error});
    });
    page.on('response',res=>{ if(res.status()>=400 && !res.url().includes('/api/event')) badResponses.push({url:res.url(),status:res.status()}); });
    let status=0,title='',finalUrl='',navError='';
    try{
      const resp=await page.goto(BASE+rel,{waitUntil:'domcontentloaded',timeout:50000});
      status=resp?.status()||0;
      await page.waitForTimeout(900);
      title=await page.title();
      finalUrl=page.url();
    }catch(e){ navError=String(e); }

    let dom={};
    try{
      dom=await page.evaluate(({vw})=>{
        const visible=el=>{const s=getComputedStyle(el),r=el.getBoundingClientRect();return s.visibility!=='hidden'&&s.display!=='none'&&Number(s.opacity)>0&&r.width>1&&r.height>1;};
        const root=document.documentElement;
        const overflowX=Math.max(root.scrollWidth,document.body?.scrollWidth||0)-root.clientWidth;
        const brokenImages=[...document.images].filter(i=>i.complete&&i.naturalWidth===0).map(i=>i.currentSrc||i.src).slice(0,20);
        const dupIds=Object.entries([...document.querySelectorAll('[id]')].reduce((m,e)=>{m[e.id]=(m[e.id]||0)+1;return m;},{})).filter(([,n])=>n>1);
        const unlabeledInputs=[...document.querySelectorAll('input,select,textarea')].filter(el=>{
          if(!visible(el))return false;
          const id=el.id;
          return !(el.getAttribute('aria-label')||el.getAttribute('aria-labelledby')||(id&&document.querySelector('label[for="'+CSS.escape(id)+'"]'))||el.closest('label'));
        }).map(el=>({tag:el.tagName,type:el.getAttribute('type')||'',name:el.getAttribute('name')||''})).slice(0,20);
        const unnamedActions=[...document.querySelectorAll('button,a[href]')].filter(el=>{if(!visible(el))return false;const nestedAlt=[...el.querySelectorAll('img[alt]')].map(img=>img.getAttribute('alt')?.trim()).find(Boolean);return !(el.innerText?.trim()||el.getAttribute('aria-label')||el.getAttribute('title')||nestedAlt);}).map(el=>el.outerHTML.slice(0,180)).slice(0,20);
        const tinyTargets=[...document.querySelectorAll('button,a[href],input[type=checkbox],input[type=radio]')].filter(el=>{
          if(!visible(el))return false;
          const r=el.getBoundingClientRect(); return r.width<36||r.height<36;
        }).map(el=>{const r=el.getBoundingClientRect();return {text:(el.innerText||el.getAttribute('aria-label')||'').trim().slice(0,60),w:Math.round(r.width),h:Math.round(r.height)};}).slice(0,30);
        const clippedRight=[...document.querySelectorAll('body *')].filter(el=>{
          if(!visible(el))return false;
          const r=el.getBoundingClientRect(),s=getComputedStyle(el);
          if(['fixed','sticky'].includes(s.position))return false;
          let parent=el.parentElement,horizontalScroller=false;
          while(parent&&parent!==document.body){const ps=getComputedStyle(parent);if(['auto','scroll'].includes(ps.overflowX)){horizontalScroller=true;break;}parent=parent.parentElement;}
          if(horizontalScroller)return false;
          return r.right>vw+3 && r.left<vw && r.width>12;
        }).map(el=>({tag:el.tagName,cls:String(el.className||'').slice(0,100),right:Math.round(el.getBoundingClientRect().right)})).slice(0,30);
        return {overflowX,brokenImages,dupIds,unlabeledInputs,unnamedActions,tinyTargets,clippedRight,bodyText:(document.body?.innerText||'').slice(0,300)};
      },{vw:device.width});
    }catch(e){ dom={evalError:String(e)}; }

    let a11y=[];
    try{
      await page.addScriptTag({path:axePath});
      const axe=await page.evaluate(async()=>await window.axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa']},resultTypes:['violations']}));
      a11y=(axe.violations||[]).map(v=>({id:v.id,impact:v.impact,help:v.help,nodes:v.nodes.slice(0,5).map(n=>({target:n.target,html:n.html,failureSummary:n.failureSummary}))}));
    }catch{}

    const shot=path.join(OUT,'screenshots',device.name+'__'+sanitize(rel)+'.png');
    try{await page.screenshot({path:shot,fullPage:true,animations:'disabled'});}catch{}
    report.results.push({device:device.name,path:rel,status,title,finalUrl,navError,consoleErrors:[...new Set(errors)].slice(0,20),failedRequests:failed.slice(0,20),badResponses:badResponses.slice(0,20),dom,a11y,screenshot:path.relative(OUT,shot)});
    await page.close();
  }
  await context.close();
}
await browser.close();

const severeA11y=new Set(['critical','serious']);
const issueRows=[];
for(const r of report.results){
  const issues=[];
  if(r.navError)issues.push('navigation');
  if(r.status>=400||!r.status)issues.push('http '+r.status);
  if(r.consoleErrors.length)issues.push('console '+r.consoleErrors.length);
  if(r.failedRequests.length)issues.push('request-failed '+r.failedRequests.length);
  if((r.dom?.overflowX||0)>3)issues.push('overflow-x '+Math.round(r.dom.overflowX));
  if(r.dom?.brokenImages?.length)issues.push('broken-img '+r.dom.brokenImages.length);
  if(r.dom?.dupIds?.length)issues.push('duplicate-id '+r.dom.dupIds.length);
  if(r.dom?.unlabeledInputs?.length)issues.push('unlabeled-input '+r.dom.unlabeledInputs.length);
  if(r.dom?.unnamedActions?.length)issues.push('unnamed-action '+r.dom.unnamedActions.length);
  if(r.dom?.clippedRight?.length)issues.push('clipped-right '+r.dom.clippedRight.length);
  const severe=r.a11y.filter(v=>severeA11y.has(v.impact));
  if(severe.length)issues.push('a11y-serious '+severe.length);
  if(issues.length)issueRows.push({device:r.device,path:r.path,issues});
}
report.summary={tested:report.results.length,routeCount:routes.length,issuePages:issueRows.length,issues:issueRows};
fs.writeFileSync(path.join(OUT,'report.json'),JSON.stringify(report,null,2));
const md=['# Inokskar Live UI Audit','',`Base: ${BASE}`,`Routes: ${routes.length}`,`Checks: ${report.results.length}`,`Pages/viewports with findings: ${issueRows.length}`,'','## Routes','',...routes.map(r=>'- '+r),'','## Findings',''];
if(!issueRows.length) md.push('No automated blocking findings.');
else for(const row of issueRows) md.push(`- **${row.device} ${row.path}** — ${row.issues.join(', ')}`);
md.push('','## Notes','- Full-page screenshots are captured for mobile, tablet and desktop.','- Authentication-protected pages are audited in their logged-out/redirect state.','- Tiny touch targets are recorded in report.json as informational diagnostics.');
fs.writeFileSync(path.join(OUT,'REPORT.md'),md.join('\n'));
console.log('QA_REPORT_SUMMARY '+JSON.stringify(report.summary));
if(issueRows.length) process.exitCode=2;
