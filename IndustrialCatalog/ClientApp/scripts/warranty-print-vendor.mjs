import {composeBooklet} from './booklet-pdf.mjs';
import QRCode from 'qrcode';
import html2canvas from 'html2canvas';
import {PDFDocument,PrintScaling,Duplex,rgb} from 'pdf-lib';
window.WarrantyPrint={composeBooklet,render:html2canvas,PDFDocument,PrintScaling,Duplex,rgb,async makeQR(text){const qr=QRCode.create(text,{errorCorrectionLevel:'M'});return {dataUrl:await QRCode.toDataURL(text,{errorCorrectionLevel:'M',margin:4,scale:8}),size:qr.modules.size,modules:qr.modules.data};}};
