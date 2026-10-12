import {readFileSync,readdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const base=fileURLToPath(new URL('..',import.meta.url)),failures=[];
function check(dir){for(const item of readdirSync(dir,{withFileTypes:true})){if(item.name==='node_modules')continue;const path=dir+'/'+item.name;if(item.isDirectory())check(path);else if(/\.(css|js|tsx|ts)$/.test(item.name)&&/!\s*important/i.test(readFileSync(path,'utf8')))failures.push(path);}}
check(base+'/public');check(base+'/src');check(base+'/../wwwroot');
if(failures.length)throw new Error('Forced CSS priority is prohibited: '+failures.join(', '));
console.log('Style contract: PASS (source and built assets contain no forced declarations).');
