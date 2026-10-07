import {readdirSync,readFileSync,statSync} from 'node:fs';
import {extname,join} from 'node:path';
import {fileURLToPath} from 'node:url';

const repositoryRoot=fileURLToPath(new URL('../../../',import.meta.url));
const forbidden='!'+'important';
const excludedDirectories=new Set(['.git','node_modules','bin','obj','.vs','.idea','TestResults']);
const textExtensions=new Set(['.css','.scss','.sass','.less','.html','.htm','.js','.jsx','.mjs','.cjs','.ts','.tsx','.cs','.cshtml','.razor','.json','.md','.yml','.yaml','.xml','.props','.targets','.sln','.toml','.txt']);
const extensionlessTextFiles=new Set(['Dockerfile','.gitignore','.dockerignore']);
const violations=[];

function scan(directory){
  for(const entry of readdirSync(directory,{withFileTypes:true})){
    if(entry.isDirectory()&&excludedDirectories.has(entry.name))continue;
    const fullPath=join(directory,entry.name);
    if(entry.isDirectory()){scan(fullPath);continue;}
    if(!entry.isFile())continue;
    const extension=extname(entry.name).toLowerCase();
    if(!textExtensions.has(extension)&&!extensionlessTextFiles.has(entry.name))continue;
    if(statSync(fullPath).size>2_000_000)continue;
    const content=readFileSync(fullPath,'utf8');
    if(!content.includes(forbidden))continue;
    const lines=content.split(/\r?\n/);
    lines.forEach((line,index)=>{
      if(line.includes(forbidden))violations.push(fullPath.slice(repositoryRoot.length)+(index+1));
    });
  }
}

scan(repositoryRoot);
if(violations.length){
  console.error('CSS cascade policy violation: forbidden priority override detected.');
  for(const location of violations.slice(0,50))console.error(' - '+location);
  if(violations.length>50)console.error(' - and '+(violations.length-50)+' more');
  process.exit(1);
}
console.log('CSS cascade policy passed.');
