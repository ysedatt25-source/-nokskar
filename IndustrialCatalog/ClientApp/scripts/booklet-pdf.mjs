import {PDFDocument,PrintScaling,Duplex,rgb} from 'pdf-lib';
export const BOOKLET_ORDER=[[3,0],[1,2]];
export async function composeBooklet(images,qrBox){
 if(images.length!==4)throw new Error('Kitapçık dört sayfa olmalı.');
 const pdf=await PDFDocument.create(),mm=72/25.4,width=297*mm,height=210*mm,half=width/2;
 const embedded=await Promise.all(images.map(image=>pdf.embedPng(image)));
 for(const pair of BOOKLET_ORDER){const page=pdf.addPage([width,height]);for(let side=0;side<2;side++){
  const index=pair[side];page.drawImage(embedded[index],{x:side*half,y:0,width:half,height});
  if(index===1&&qrBox){const box=qrBox,qr=box.qr,x=side*half+box.x*half,y=height-(box.y+box.h)*height,w=box.w*half,h=box.h*height;page.drawRectangle({x,y,width:w,height:h,color:rgb(1,1,1)});const unit=w/(qr.size+8);for(let row=0;row<qr.size;row++)for(let col=0;col<qr.size;col++)if(qr.modules[row*qr.size+col])page.drawRectangle({x:x+(col+4)*unit,y:y+(qr.size-1-row+4)*unit,width:unit,height:unit,color:rgb(0,0,0)});}
 } }
 const prefs=pdf.catalog.getOrCreateViewerPreferences();prefs.setPrintScaling(PrintScaling.None);prefs.setDuplex(Duplex.DuplexFlipShortEdge);prefs.setPickTrayByPDFSize(true);prefs.setNumCopies(1);prefs.setPrintPageRange([{start:0,end:1}]);
 pdf.setTitle('İNOKSKAR · Garanti ve Kullanım Kitapçığı');pdf.setCreator('İNOKSKAR');
 return pdf.save();
}
