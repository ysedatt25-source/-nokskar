import { chromium } from 'playwright';
import fs from 'node:fs';

const base=(process.env.QA_BASE_URL||'https://inokskar-web-preview.up.railway.app').replace(/\/$/,'');
const out='qa-output';
fs.mkdirSync(out,{recursive:true});
const assert=(ok,message)=>{if(!ok)throw new Error(message);};
const publicCatalog=await fetch(base+'/api/catalog').then(async response=>{
  assert(response.ok,'Public catalog fixture unavailable');
  return response.json();
});
const productData=publicCatalog.data;
assert(productData?.categories?.length>0,'Catalog categories missing');
assert(productData?.products?.length>0,'Catalog products missing');
let revision=publicCatalog.revision||1,saveCount=0,updatedName='',failNextSave=false;
const browser=await chromium.launch({headless:true});
try{
  const context=await browser.newContext({viewport:{width:1440,height:900},locale:'tr-TR'});
  const page=await context.newPage();
  const indexHTML=await fetch(base+'/').then(r=>r.text());
  await page.route('**/admin**',route=>route.fulfill({status:200,contentType:'text/html; charset=utf-8',body:indexHTML}));
  await page.route('**/api/admin/security',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({email:'qa-browser@example.test',role:'SuperAdmin'})}));
  await page.route('**/api/security/csrf',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({token:'qa-browser-only'})}));
  await page.route('**/api/catalog**',async route=>{
    if(route.request().method()==='POST'){
      if(failNextSave){
        failNextSave=false;
        await route.fulfill({status:400,contentType:'application/json',body:JSON.stringify({error:'Test: ürün güncellenemedi.'})});
        return;
      }
      const body=JSON.parse(route.request().postData()||'{}');
      assert(body.data?.products?.length>0,'Product save payload was empty');
      productData.products=body.data.products;
      revision++;
      saveCount++;
      updatedName=productData.products[0]?.name||'';
    }
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({data:productData,revision,history:[],events:[],inquiries:[]})});
  });
  await page.goto(base+'/admin?tab=products',{waitUntil:'networkidle',timeout:60000});
  await page.locator('.product-item-card').first().waitFor({state:'visible',timeout:12000});
  assert(new URL(page.url()).searchParams.get('tab')==='products','Product deep link not retained');
  const checkProductLayout=async (label)=>{
    const v=await page.evaluate(()=>{
      const buttons=[...document.querySelectorAll('.product-primary-actions button')];
      const rects=buttons.map(el=>{const r=el.getBoundingClientRect();return {x:r.x,y:r.y,right:r.right};});
      const card=document.querySelector('.product-item-card');
      const box=card?.getBoundingClientRect();
      return {scope:document.body.dataset.uiPage,
        bar:document.querySelector('.product-primary-actions')&&getComputedStyle(document.querySelector('.product-primary-actions')).display,
        list:document.querySelector('.product-card-list')&&getComputedStyle(document.querySelector('.product-card-list')).display,
        cardDisplay:card&&getComputedStyle(card).display,
        border:card&&getComputedStyle(card).borderLeftWidth,
        buttons:rects,cardBox:box?{x:box.x,right:box.right}:null,screen:innerWidth};
    });
    assert(v.scope==='admin-products','Product CSS scope missing '+label+': '+JSON.stringify(v));
    assert(v.bar==='grid'&&v.list==='grid'&&v.cardDisplay==='grid','Product grid CSS missing '+label+': '+JSON.stringify(v));
    assert(Number.parseFloat(v.border)>=2,'Card visual framing missing '+label+': '+JSON.stringify(v));
    assert(v.buttons.length===3 && Math.max(...v.buttons.map(x=>x.y))-Math.min(...v.buttons.map(x=>x.y))<4,'3 action buttons not side-by-side '+label+': '+JSON.stringify(v));
    assert(v.buttons.every(b=>b.x>=-2&&b.right<=v.screen+2),'Action row exceeds viewport '+label+': '+JSON.stringify(v));
    assert(v.cardBox&&v.cardBox.x>=-2&&v.cardBox.right<=v.screen+2,'Card exceeds viewport '+label+': '+JSON.stringify(v));
  };
  await page.setViewportSize({width:390,height:844});
  await checkProductLayout('mobile direct-link');
  await page.screenshot({path:out+'/product-mobile-direct-load.png',fullPage:true});
  await page.setViewportSize({width:820,height:1180});
  await checkProductLayout('tablet direct-link');
  await page.setViewportSize({width:1440,height:900});
  await checkProductLayout('desktop direct-link');
  await page.reload({waitUntil:'networkidle'});
  await page.locator('.product-item-card').first().waitFor({state:'visible',timeout:12000});
  assert(new URL(page.url()).searchParams.get('tab')==='products','Product tab lost on reload');
  await checkProductLayout('desktop refresh');

  // Verify the shared modal primitive in the real browser at phone, tablet and desktop sizes.
  // The production API is mocked; no product is deleted.
  const checkOverlayBounds=async (locator,label,width,height)=>{
    await locator.waitFor({state:'visible',timeout:9000});
    const result=await locator.evaluate(el=>{
      const b=el.getBoundingClientRect();
      const back=document.querySelector('[data-slot="alert-dialog-overlay"],[data-slot="dialog-overlay"]');
      const dock=document.querySelector('.admin-app-dock');
      return {left:b.left,right:b.right,top:b.top,bottom:b.bottom,w:b.width,h:b.height,
        translate:getComputedStyle(el).translate,overlayZ:back?Number(getComputedStyle(back).zIndex):0,
        dockZ:dock&&getComputedStyle(dock).display!=='none'?(Number.parseInt(getComputedStyle(dock).zIndex,10)||0):0};
    });
    assert(result.left>=-2&&result.right<=width+2&&result.top>=-2&&result.bottom<=height+2,
      'Offscreen modal '+label+': '+JSON.stringify(result));
    assert(result.overlayZ>result.dockZ,'Modal is behind mobile dock '+label+': '+JSON.stringify(result));
    if(width<=700){
      assert(Math.abs(result.bottom-height)<4,'Mobile sheet not bottom aligned '+label+': '+JSON.stringify(result));
      assert(result.translate==='none','Old horizontal dialog translation remains '+label+': '+JSON.stringify(result));
    }else{
      assert(Math.abs((result.left+result.right)/2-width/2)<5,'Dialog not centered '+label+': '+JSON.stringify(result));
    }
  };
  for(const {width,height} of [{width:390,height:844},{width:820,height:1180},{width:1440,height:900}]){
    await page.setViewportSize({width,height});
    const brand=page.locator('.admin-sidebar > a.brand.private-brand-link');
    const bounds=await brand.boundingBox();
    assert(bounds,'Management logo missing '+width);
    if(width<=1100){
      assert(Math.abs(bounds.x+bounds.width/2-width/2)<4,
        'Management logo is not centered at '+width+': '+JSON.stringify(bounds));
    }else{
      const sidebar=await page.locator('.admin-sidebar').boundingBox();
      assert(sidebar&&Math.abs(bounds.x+bounds.width/2-sidebar.x-sidebar.width/2)<5,
        'Desktop management logo not centered in sidebar: '+JSON.stringify(bounds));
    }
    await page.locator('.product-item-card').first().getByRole('button',{name:/sil/i}).click();
    const confirmDialog=page.locator('[data-slot="alert-dialog-content"]');
    await checkOverlayBounds(confirmDialog,'Product deletion confirmation '+width,width,height);
    if(width===390)await page.screenshot({path:out+'/confirmation-dialog-mobile.png'});
    await confirmDialog.getByRole('button',{name:'Vazgeç'}).click();
    await confirmDialog.waitFor({state:'hidden'});
  }
  await page.setViewportSize({width:390,height:844});
  await page.goto(base+'/admin?tab=categories',{waitUntil:'networkidle',timeout:45000});
  await page.locator('.category-admin-list .category-admin').first().getByRole('button',{name:'Düzenle'}).click();
  const categoryDialog=page.locator('[data-slot="dialog-content"]');
  await checkOverlayBounds(categoryDialog,'Category editor',390,844);
  await categoryDialog.locator('[data-slot="dialog-close"]').click();
  await categoryDialog.waitFor({state:'hidden'});
  await page.goto(base+'/admin?tab=prices',{waitUntil:'networkidle',timeout:45000});
  await page.getByRole('button',{name:/Excel\/CSV fiyat aktar/}).click();
  const priceDialog=page.locator('[data-slot="dialog-content"]');
  await checkOverlayBounds(priceDialog,'Price import',390,844);
  await priceDialog.locator('[data-slot="dialog-close"]').click();
  await priceDialog.waitFor({state:'hidden'});
  await page.goto(base+'/admin?tab=products',{waitUntil:'networkidle',timeout:45000});
  await page.setViewportSize({width:1440,height:900});
  await page.locator('.admin-sidebar nav button').filter({hasText:'Ürünler'}).first().click();
  await page.locator('.product-item-card').first().waitFor({state:'visible',timeout:12000});
  await page.screenshot({path:out+'/product-list-desktop.png',fullPage:true});
  const firstRow=page.locator('.product-item-card').first();
  await firstRow.locator('.product-select-hitarea input').check();
  assert(await firstRow.locator('.product-select-hitarea input').isChecked(),'Product select checkbox did not check');
  await firstRow.locator('.product-select-hitarea input').uncheck();
  assert(!await firstRow.locator('.product-select-hitarea input').isChecked(),'Product select checkbox did not clear');
  await page.locator('.product-select-all').click();
  assert(await page.locator('.product-select-all').getAttribute('aria-pressed')==='true','Select all failed');
  await page.locator('.product-select-all').click();
  assert(await page.locator('.product-select-all').getAttribute('aria-pressed')==='false','Clear all failed');
  await page.setViewportSize({width:390,height:844});
  const firstActionGroup=page.locator('.product-primary-actions');
  const rects=await firstActionGroup.locator('button').evaluateAll(xs=>xs.map(x=>{const b=x.getBoundingClientRect();return {x:b.x,y:b.y,width:b.width,right:b.right};}));
  assert(rects.length===3,'Expected 3 top action buttons');
  assert(Math.max(...rects.map(x=>x.y))-Math.min(...rects.map(x=>x.y))<3,'Top action buttons are not in one row');
  assert(rects.every(x=>x.x>=-1&&x.right<=391),'Top action button clipped on mobile');
  await page.setViewportSize({width:1440,height:900});
  // Verify bulk operations and selection against the isolated mocked catalog.
  await firstRow.locator('.product-select-hitarea input').check();
  await page.locator('.product-bulk-editor').first().locator('select').selectOption(productData.categories[0].id);
  assert(await page.locator('.product-bulk-editor').first().getByRole('button',{name:'Kategoriye taşı'}).isEnabled(),'Category move did not enable');
  failNextSave=true;
  await page.locator('.product-bulk-editor').first().getByRole('button',{name:'Kategoriye taşı'}).click();
  await page.locator('.product-action-feedback.error').waitFor({state:'visible'});
  assert((await page.locator('.product-action-feedback.error').innerText()).includes('Test: ürün güncellenemedi.'),'Error feedback missing');
  assert(saveCount===0,'Failed save should not modify catalog');
  await page.locator('.product-bulk-editor').first().getByRole('button',{name:'Kategoriye taşı'}).click();
  assert(saveCount===1,'Category move did not send save request');
  assert((await page.locator('.product-action-feedback.success').innerText()).includes('kategoriye taşındı'),'Success feedback missing');
  await page.locator('.product-bulk-editor').nth(1).locator('select').selectOption('Stokta');
  await page.locator('.product-bulk-editor').nth(1).getByRole('button',{name:'Durumu uygula'}).click();
  assert(saveCount===2,'Status update did not send save request');
  await page.locator('.product-bulk-publish').getByRole('button',{name:'Seçilileri gizle'}).click();
  assert(saveCount===3,'Visibility update did not send save request');
  await page.locator('.product-bulk-selection').getByRole('button',{name:'Seçimi temizle'}).click();
  assert(await page.locator('.product-selected-count').innerText()==='0 seçili','Bulk selection clear failed');
  assert((await page.locator('.product-action-feedback.info').innerText()).includes('Seçim temizlendi'),'Selection feedback missing');
  await page.locator('.admin-sidebar nav button').filter({hasText:'Genel bakış'}).first().click();
  assert(new URL(page.url()).searchParams.get('tab')==='overview','Admin navigation did not update URL');
  await page.reload({waitUntil:'networkidle'});
  await page.getByRole('heading',{name:'Genel bakış'}).waitFor({state:'visible',timeout:12000});
  await page.goBack({waitUntil:'networkidle'});
  await page.locator('.product-item-card').first().waitFor({state:'visible',timeout:12000});
  assert(new URL(page.url()).searchParams.get('tab')==='products','Back navigation failed');
  await page.locator('.product-primary-actions').getByRole('button',{name:'Görsel aktar'}).click();
  const importDialog=page.locator('.image-import-dialog');
  await importDialog.waitFor({state:'visible'});
  await page.setViewportSize({width:390,height:844});
  const importRect=await importDialog.boundingBox();
  assert(!!importRect&&importRect.x>=-2&&importRect.x+importRect.width<=392,'Import modal clipped horizontally');
  await page.screenshot({path:out+'/image-import-mobile.png',fullPage:false});
  await importDialog.getByRole('button',{name:'İptal'}).click();
  await importDialog.waitFor({state:'hidden'});
  await page.setViewportSize({width:1440,height:900});
  await page.waitForTimeout(400);
  const postDialog=await page.evaluate(()=>({
    url:location.pathname+location.search,
    tab:document.body.dataset.uiPage,
    cards:document.querySelectorAll('.product-item-card').length,
    buttons:document.querySelectorAll('.product-item-card .product-list-actions button').length,
    dialogs:document.querySelectorAll('[data-slot="dialog-content"]').length,
    overlays:document.querySelectorAll('[data-slot="dialog-overlay"]').length,
    html:document.querySelector('.product-item-card')?.outerHTML?.slice(-500)
  }));
  console.log('POST_IMPORT_DIAGNOSTICS',JSON.stringify(postDialog));
  assert(postDialog.cards>0,'Product cards disappeared after closing image import: '+JSON.stringify(postDialog));
  await page.locator('.product-item-card').first().getByRole('button',{name:'Düzenle'}).click();
  const editor=page.locator('.product-editor-dialog');
  await editor.waitFor({state:'visible'});
  assert(await editor.locator('.product-edit-section').count()===5,'Product form sections missing');
  const category=editor.locator('.product-edit-basic-grid select');
  const status=editor.locator('.product-edit-price-grid select');
  assert(await category.count()===1,'Category dropdown absent');
  assert(await status.count()===1,'Status dropdown absent');
  const originalCategory=await category.inputValue();
  const otherCategory=productData.categories.find(c=>c.id!==originalCategory)?.id||originalCategory;
  await category.selectOption(otherCategory);
  await status.selectOption('Stokta');
  assert(await category.inputValue()===otherCategory,'Category dropdown did not update');
  assert(await status.inputValue()==='Stokta','Product status dropdown did not update');
  await editor.locator('.seo-editor summary').click();
  assert(await editor.locator('.seo-editor').evaluate(el=>el.open),'SEO settings did not expand');
  await page.screenshot({path:out+'/product-editor-desktop.png',fullPage:false});
  const originalName=await editor.locator('.product-edit-basic-grid input').first().inputValue();
  await editor.locator('.product-edit-basic-grid input').first().fill(originalName+' QA');
  await editor.getByRole('button',{name:'Ürünü kaydet ve yayınla'}).click();
  await editor.waitFor({state:'hidden',timeout:12000});
  assert(saveCount===4,'Editing did not submit product data');
  assert(updatedName.endsWith(' QA'),'Edited product name was not saved in request payload');

  await page.locator('.product-primary-actions').getByRole('button',{name:'Ürün ekle'}).click();
  await editor.waitFor({state:'visible'});
  assert((await editor.locator('[data-slot=dialog-title]').innerText()).includes('Yeni ürün'),'Create product heading incorrect');
  await editor.locator('.product-edit-basic-grid input').nth(0).fill('QA Test Ürünü');
  await editor.locator('.product-edit-basic-grid input').nth(1).fill('QA-TEST-001');
  await editor.locator('.product-edit-basic-grid select').selectOption(productData.categories[0].id);
  await page.setViewportSize({width:390,height:844});
  await page.waitForTimeout(300);
  const editorBounds=await editor.boundingBox();
  assert(editorBounds && editorBounds.x>=-2 && editorBounds.x+editorBounds.width<=392,'Mobile product editor is clipped horizontally');
  const saveBounds=await editor.getByRole('button',{name:'Ürünü kaydet ve yayınla'}).boundingBox();
  assert(saveBounds && saveBounds.x>=-2 && saveBounds.x+saveBounds.width<=392,'Mobile save button is clipped horizontally');
  await page.screenshot({path:out+'/product-editor-mobile.png',fullPage:false});
  assert(await editor.getByRole('button',{name:'İptal'}).isVisible(),'Product cancel absent');
  const handle=editor.getByRole('button',{name:'Ürün penceresini aşağı kaydırarak iptal et'});
  assert(await handle.isVisible(),'Product drag handle absent');
  // Test Cancel without saving changes; re-open for a separate create submission.
  await editor.getByRole('button',{name:'İptal'}).click();
  await editor.waitFor({state:'hidden'});
  assert(saveCount===4,'Cancel unexpectedly saved product');
  await page.locator('.product-primary-actions').getByRole('button',{name:'Ürün ekle'}).click();
  await editor.waitFor({state:'visible'});
  const hb=await handle.boundingBox();
  assert(hb,'Drag handle has no bounds');
  await page.mouse.move(hb.x+hb.width/2,hb.y+hb.height/2);
  await page.mouse.down();
  await page.mouse.move(hb.x+hb.width/2,hb.y+hb.height/2+145,{steps:8});
  await page.mouse.up();
  await editor.waitFor({state:'hidden'});
  assert(saveCount===4,'Downward swipe unexpectedly saved product');
  await page.locator('.product-primary-actions').getByRole('button',{name:'Ürün ekle'}).click();
  await editor.waitFor({state:'visible'});
  await editor.locator('.product-edit-basic-grid input').nth(0).fill('QA Test Ürünü');
  await editor.locator('.product-edit-basic-grid input').nth(1).fill('QA-TEST-001');
  await editor.locator('.product-edit-basic-grid select').selectOption(productData.categories[0].id);
  await editor.getByRole('button',{name:'Ürünü kaydet ve yayınla'}).click();
  await editor.waitFor({state:'hidden',timeout:12000});
  assert(saveCount===5,'Creating did not submit product data');
  assert(productData.products.some(p=>p.name==='QA Test Ürünü'),'Created product missing from request payload');
  await page.screenshot({path:out+'/product-list-mobile.png',fullPage:true});
  await context.close();

  const publicContext=await browser.newContext({viewport:{width:390,height:844},isMobile:true,hasTouch:true,deviceScaleFactor:1});
  const publicPage=await publicContext.newPage();
  for(const path of ['/','/urunler','/iletisim','/servis-talebi','/garanti-sorgulama','/talep-sorgula']){
    await publicPage.goto(base+path,{waitUntil:'domcontentloaded',timeout:45000});
    const logo=publicPage.locator(path==='/talep-sorgula'?'.top > a:first-child':'.site-header > .brand.brand-image-link').first();
    await logo.waitFor({state:'visible',timeout:12000});
    const geometry=await logo.evaluate(el=>{
      const b=el.getBoundingClientRect();
      const h=el.closest('.site-header,.top')?.getBoundingClientRect();return {center:b.x+b.width/2,screen:window.innerWidth,headerLeft:h?.left,headerWidth:h?.width};
    });
    assert(Math.abs(geometry.center-geometry.screen/2)<=3,
      'Logo is not centered on '+path+': '+JSON.stringify(geometry));
    await publicPage.screenshot({path:out+'/logo-'+(path==='/'?'home':path.slice(1))+'.png',fullPage:false});
  }
  const visibleProduct=productData.products.find(p=>p.visible!==false&&p.id);
  assert(visibleProduct,'Public product fixture missing');
  await publicPage.goto(base+'/urun/'+encodeURIComponent(visibleProduct.id),{waitUntil:'domcontentloaded',timeout:45000});
  const routeBefore=new URL(publicPage.url()).pathname;
  await publicPage.reload({waitUntil:'domcontentloaded'});
  assert(new URL(publicPage.url()).pathname===routeBefore,'Product detail refresh changed page');
  const visibleCategory=productData.categories.find(c=>c.visible!==false&&c.id);
  assert(visibleCategory,'Public category fixture missing');
  await publicPage.goto(base+'/kategori/'+encodeURIComponent(visibleCategory.id),{waitUntil:'domcontentloaded',timeout:45000});
  const categoryBefore=new URL(publicPage.url()).pathname;
  await publicPage.reload({waitUntil:'domcontentloaded'});
  assert(new URL(publicPage.url()).pathname===categoryBefore,'Category refresh changed page');
  await publicContext.close();
  console.log(JSON.stringify({result:'PASS',productSaves:saveCount,reloadAndBack:true,errorFeedback:true,categoriesTested:true,statusTested:true,seoExpanded:true,mobileEditorContained:true,publicLogosCentered:6,mode:'mocked admin API; no production changes'},null,2));
}finally{
  await browser.close();
}
