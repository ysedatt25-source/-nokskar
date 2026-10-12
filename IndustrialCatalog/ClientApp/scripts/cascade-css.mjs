import postcss from 'postcss';
export const layerOrder='@layer theme, base, components, utilities, site, layout;';
export function normalizeCss(css){
 const root=postcss.parse(css);const seen=new Set();
 const rules=[];root.walkRules(rule=>rules.push(rule));
 for(const rule of rules.reverse()){
  const contexts=[];for(let p=rule.parent;p&&p.type!=='root';p=p.parent)if(p.type==='atrule')contexts.unshift(p.name+':'+p.params);else if(p.type==='rule')contexts.unshift('selector:'+p.selector);
  const declarations=rule.nodes.filter(n=>n.type==='decl').map(n=>[n.prop,n.value,!!n.important]);
  if(rule.nodes.some(n=>n.type==='rule'||n.type==='atrule'))continue;
  const key=JSON.stringify([contexts,rule.selector,declarations]);if(seen.has(key))rule.remove();else seen.add(key);
 }
 const declarationsSeen=new Set();const ordered=[];root.walkDecls(decl=>{if(decl.parent.type==='rule')ordered.push(decl);});
 for(const decl of ordered.reverse()){
  const contexts=[];for(let p=decl.parent;p&&p.type!=='root';p=p.parent)if(p.type==='atrule')contexts.unshift(p.name+':'+p.params);else if(p.type==='rule')contexts.unshift('selector:'+p.selector);
  const prop=decl.prop.startsWith('--')?decl.prop:decl.prop.toLowerCase();const key=JSON.stringify([contexts,prop,!!decl.important]);
  if(declarationsSeen.has(key))decl.remove();else declarationsSeen.add(key);
 }
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
