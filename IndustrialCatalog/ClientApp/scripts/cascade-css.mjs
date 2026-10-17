import postcss from 'postcss';
export const layerOrder='@layer theme, base, components, utilities, site, layout;';
export function normalizeCss(css){
 const root=postcss.parse(css);const seen=new Set();
 // Refuse forced priority at source instead of silently rewriting it into a stronger layer.
 root.walkDecls(decl=>{if(decl.important)throw new Error('Forced CSS priority is prohibited for property: '+decl.prop);});
 const rules=[];root.walkRules(rule=>rules.push(rule));
 for(const rule of rules.reverse()){
  const contexts=[];for(let p=rule.parent;p&&p.type!=='root';p=p.parent)if(p.type==='atrule')contexts.unshift(p.name+':'+p.params);else if(p.type==='rule')contexts.unshift('selector:'+p.selector);
  const declarations=rule.nodes.filter(n=>n.type==='decl').map(n=>[n.prop,n.value,!!n.important]);
  if(rule.nodes.some(n=>n.type==='rule'||n.type==='atrule'))continue;
  const key=JSON.stringify([contexts,rule.selector,declarations]);if(seen.has(key))rule.remove();else seen.add(key);
 }
 // Different values may be browser fallbacks. Shorthands between declarations
 // can also reset a longhand, so only adjacent identical declarations are safe.
 root.walkRules(rule=>{
  let previous=null;
  for(const node of [...rule.nodes]){
   if(node.type==='comment')continue;
   if(node.type!=='decl'){previous=null;continue;}
   const prop=node.prop.startsWith('--')?node.prop:node.prop.toLowerCase();
   const key=JSON.stringify([prop,node.value,!!node.important]);
   if(key===previous)node.remove();else previous=key;
  }
 });
 root.walkRules(rule=>{if(!rule.nodes.some(node=>node.type!=='comment'))rule.remove();});
 const outside=[],normal=[],layout=[];
 function split(node){
  if(node.type==='decl'){const copy=node.clone();copy.important=false;return node.important?[null,copy]:[copy,null];}
  if(!node.nodes)return [node.clone(),null];
  const a=node.clone({nodes:[]}),b=node.clone({nodes:[]});
  for(const child of node.nodes){const [low,high]=split(child);if(low)a.append(low);if(high)b.append(high);}
  return [a.nodes.some(n=>n.type!=='comment')?a:null,b.nodes.some(n=>n.type!=='comment')?b:null];
 }
 for(const node of root.nodes){
  if(node.type==='atrule'&&['charset','namespace','custom-variant','theme','utility','variant','source'].includes(node.name)){outside.push(node.clone());continue;}
  if(node.type==='atrule'&&node.name==='import'){const copy=node.clone();if(!/\blayer\b/.test(copy.params)&&!/(tailwindcss|tw-animate-css)/.test(copy.params))copy.params+=' layer(site)';outside.push(copy);continue;}
  if(node.type==='atrule'&&node.name==='layer'&&['site','layout'].includes(node.params)){outside.push(node.clone());continue;}
  if(node.type==='atrule'&&node.name==='layer'&&!node.nodes)continue;
  const [low,high]=split(node);if(low)normal.push(low);if(high)layout.push(high);
 }
 const result=postcss.root();result.append(postcss.atRule({name:'layer',params:'theme, base, components, utilities, site, layout'}));
 for(const node of outside)result.append(node);
 if(normal.length)result.append(postcss.atRule({name:'layer',params:'site',nodes:normal}));
 if(layout.length)result.append(postcss.atRule({name:'layer',params:'layout',nodes:layout}));
 return result.toString();
}
