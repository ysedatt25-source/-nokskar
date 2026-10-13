/** Only local immutable uploads have server-generated variants. External URLs stay intact. */
export function mediaUrl(url:string|undefined,variant:'thumb'|'card'|'detail'|'stream'|'poster'){
 if(!url)return undefined;
 return /^\/api\/files\/[a-f0-9]{32}\.(png|jpg|webp|mp4)$/.test(url)?url+'?media='+variant:url;
}
export function imageSources(url:string){return /^\/api\/files\/[a-f0-9]{32}\.(png|jpg|webp)$/.test(url)?`${mediaUrl(url,'card')} 640w, ${mediaUrl(url,'detail')} 1280w`:undefined;}
