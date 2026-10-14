import assert from 'node:assert/strict';
import {writeFileSync} from 'node:fs';
import {PNG} from 'pngjs';
import {PDFDocument,Duplex,PrintScaling} from 'pdf-lib';
import {composeBooklet,BOOKLET_ORDER} from './booklet-pdf.mjs';
assert.deepEqual(BOOKLET_ORDER,[[3,0],[1,2]]);
const images=[[30,70,110],[70,120,70],[130,70,100],[190,145,55]].map(color=>{const png=new PNG({width:20,height:20});for(let i=0;i<png.data.length;i+=4)png.data.set([...color,255],i);return PNG.sync.write(png);});
const bytes=await composeBooklet(images);const pdf=await PDFDocument.load(bytes);assert.equal(pdf.getPageCount(),2);
for(const page of pdf.getPages()){assert.ok(Math.abs(page.getWidth()-297/25.4*72)<.01);assert.ok(Math.abs(page.getHeight()-210/25.4*72)<.01);}
const prefs=pdf.catalog.getOrCreateViewerPreferences();assert.equal(prefs.getDuplex(),Duplex.DuplexFlipShortEdge);assert.equal(prefs.getPrintScaling(),PrintScaling.None);assert.equal(prefs.getPickTrayByPDFSize(),true);assert.deepEqual(prefs.getPrintPageRange(),[{start:0,end:1}]);
await assert.rejects(()=>composeBooklet(images.slice(0,3)));
if(process.env.BOOKLET_QA_PDF)writeFileSync(process.env.BOOKLET_QA_PDF,bytes);
console.log('Booklet PDF: PASS (4|1 / 2|3, two landscape A4 sides, exact size, short-edge duplex, 100% scaling, all pages).');
