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
let revision=publicCatalog.revision||1,saveCount=0,updatedName='';
const browser=await chromium.launch({headless:true});
try{
  const context=await browser.newContext({viewport:{width:1440,height:900},locale:'tr-TR'});
  const page=await context.newPage();
  const indexHTML=await fetch(base+'/').then(r=>r.text());
  await page.route('**/admin',route=>route.fulfill({status:200,contentType:'text/html; charset=utf-8',body:indexHTML}));
  await page.route('**/api/admin/security',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({email:'qa-browser@example.test',role:'SuperAdmin'})}));
  await page.route('**/api/security/csrf',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({token:'qa-browser-only'})}));
  await page.route('**/api/catalog**',async route=>{
    if(route.request().method()==='POST'){
      const body=JSON.parse(route.request().postData()||'{}');
      assert(body.data?.products?.length>0,'Product save payload was empty');
      productData.products=body.data.products;
      revision++;
      saveCount++;
      updatedName=productData.products[0]?.name||'';
    }
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({data:productData,revision,history:[],events:[],inquiries:[]})});
  });
  await page.goto(base+'/admin',{waitUntil:'networkidle',timeout:60000});
  await page.locator('.admin-sidebar nav button').filter({hasText:'Ürünler'}).first().click();
  await page.locator('.product-list-table tbody tr').first().waitFor({state:'visible',timeout:12000});
  await page.screenshot({path:out+'/product-list-desktop.png',fullPage:true});
  await page.locator('.product-list-table tbody tr').first().getByRole('button',{name:'Düzenle'}).click();
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
  assert(saveCount===1,'Editing did not submit product data');
  assert(updatedName.endsWith(' QA'),'Edited product name was not saved in request payload');

  await page.locator('.product-list-toolbar').getByRole('button',{name:'Ürün ekle'}).click();
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
  await editor.getByRole('button',{name:'Ürünü kaydet ve yayınla'}).click();
  await editor.waitFor({state:'hidden',timeout:12000});
  assert(saveCount===2,'Creating did not submit product data');
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
  await publicContext.close();
  console.log(JSON.stringify({result:'PASS',productSaves:saveCount,categoriesTested:true,statusTested:true,seoExpanded:true,mobileEditorContained:true,publicLogosCentered:6,mode:'mocked admin API; no production changes'},null,2));
}finally{
  await browser.close();
}
