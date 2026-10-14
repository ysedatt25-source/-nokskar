export function selectHero(images:string[],previous:string,random=Math.random):string{
 const unique=[...new Set(images.filter(Boolean))].slice(0,10);
 if(unique.length===0)return '';
 if(unique.length===1)return unique[0];
 const choices=unique.filter(url=>url!==previous);
 return choices[Math.min(choices.length-1,Math.max(0,Math.floor(random()*choices.length)))];
}
