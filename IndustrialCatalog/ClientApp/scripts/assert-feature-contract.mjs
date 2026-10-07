import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../../',import.meta.url));
const checks=[
 ['IndustrialCatalog/ClientApp/src/app/storefront.tsx',['normalizeTurkeyPhone','kişisel/iş e-posta','Ülke kodu isteğe bağlıdır']],
 ['IndustrialCatalog/ClientApp/public/r98-service-request.js',['normalizePhone','kişisel/iş e-posta','Ülke kodu isteğe bağlıdır']],
 ['IndustrialCatalog/InquiryPages.cs',['normalizePhone','+90 542','TALEP TAKİBİ']],
 ['IndustrialCatalog/Program.cs',['/api/inquiries/unread-count','/api/inquiries/mark-read','/api/service-request']],
 ['IndustrialCatalog/ClientApp/public/r13-pages.js',['admin-inquiry-badge','/api/inquiries/unread-count']]
];
const failures=[];
for(const [path,needles] of checks){const source=readFileSync(root+path,'utf8');for(const needle of needles)if(!source.includes(needle))failures.push(path+' :: '+needle);}
if(failures.length){console.error('Feature contract failed:');for(const failure of failures)console.error(' - '+failure);process.exit(1);}
console.log('Feature contract passed.');
