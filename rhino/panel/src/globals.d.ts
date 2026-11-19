declare module '*.css';

/** esbuild inlines imported PNGs as data URLs (build.mjs loader), so an import is its URL. */
declare module '*.png' {
  const url: string;
  export default url;
}

/** Compile-time flag, substituted by esbuild. False in a host build, which drops the mock. */
declare const __MOCK__: boolean;
