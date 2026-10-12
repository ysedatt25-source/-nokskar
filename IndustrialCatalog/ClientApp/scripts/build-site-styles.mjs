import {readFileSync,writeFileSync} from 'node:fs';
import {normalizeCss} from './cascade-css.mjs';

const root=new URL('../',import.meta.url);
const files=JSON.parse(readFileSync(new URL('site-styles.json',root),'utf8'));
if(!Array.isArray(files)||!files.length||new Set(files).size!==files.length)
 throw new Error('Site stylesheet manifest must contain unique filenames.');
const css=files.map(name=>{
 if(!/^[a-z0-9_-]+\.css$/.test(name))throw new Error('Invalid site stylesheet filename.');
 return '/* Source: '+name+' */\n'+readFileSync(new URL('public/'+name,root),'utf8');
}).join('\n');
writeFileSync(new URL('public/site-bundle.css',root),normalizeCss(css));
console.log('Site styles: '+files.length+' ordered modules bundled.');
