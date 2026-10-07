export default {
 plugins: [
  (await import('@tailwindcss/postcss')).default(),
  {postcssPlugin:'normal-declaration-priority',OnceExit(root){root.walkDecls(decl=>{decl.important=false;});}}
 ]
};
