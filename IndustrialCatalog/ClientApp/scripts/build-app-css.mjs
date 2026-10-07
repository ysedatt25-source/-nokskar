import {readFileSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {join} from 'node:path';
const publicDir=fileURLToPath(new URL('../public/',import.meta.url));
const sources=[
 'r95-warranty.css','r99-brand.css','r99-warranty.css','r10-overrides.css','r103-public.css',
 'r122-public.css','r123-professional.css','r124-home.css','r126-public.css','r13-interface.css',
 'styles/r13-foundation.css','styles/r13-public-app.css','styles/r13-private-admin.css','styles/r13-workflows.css','styles/r13-modern.css','r23-system.css'
];
let output='/* R23.2 generated main application stylesheet. */\n';
for(const source of sources)output+='\n/* source: '+source+' */\n'+readFileSync(join(publicDir,source),'utf8')+'\n';
writeFileSync(join(publicDir,'inokskar-app.css'),output);
console.log('Generated inokskar-app.css from '+sources.length+' ordered sources.');
