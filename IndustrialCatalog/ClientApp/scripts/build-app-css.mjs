import {readFileSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {join} from 'node:path';
const publicDir=fileURLToPath(new URL('../public/',import.meta.url));
const sources=[
 'r10-overrides.css','r123-professional.css','r13-interface.css',
 'styles/r13-foundation.css','styles/r13-private-admin.css','styles/r13-workflows.css','styles/r13-modern.css','r23-system.css'
];
let output='/* R23.2 generated main application stylesheet. */\n';
for(const source of sources)output+='\n/* source: '+source+' */\n'+readFileSync(join(publicDir,source),'utf8')+'\n';
writeFileSync(join(publicDir,'inokskar-app.css'),output);
console.log('Generated inokskar-app.css from '+sources.length+' ordered sources.');
