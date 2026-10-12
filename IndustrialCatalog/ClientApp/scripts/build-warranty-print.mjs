import {readFileSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const localModules=process.env.INOKSKAR_PRINT_NODE_MODULES;
const {build}=await import(localModules?localModules+'/esbuild/lib/main.js':'esbuild');
await build({entryPoints:[fileURLToPath(new URL('./warranty-print-vendor.mjs',import.meta.url))],outfile:fileURLToPath(new URL('../public/warranty-print-vendor.js',import.meta.url)),bundle:true,platform:'browser',format:'iife',target:'es2020',minify:true,legalComments:'inline',nodePaths:localModules?[localModules]:[]});

// The print clone owns these pseudo elements; normal selector specificity is enough.
const output=fileURLToPath(new URL('../public/warranty-print-vendor.js',import.meta.url));
writeFileSync(output,readFileSync(output,'utf8').replace(/!\s*important/gi,''));
