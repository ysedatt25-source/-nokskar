import {rmSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
rmSync(fileURLToPath(new URL('../../wwwroot',import.meta.url)),{recursive:true,force:true});
