# Stage 41 · Portable narrow scalar signatures

The portable compiler retains narrow CLR slot types while using i32 for IL evaluation stack values. It truncates or sign-extends values at the argument, local store, and return boundaries.

```sh
npm run demo -- stage-41-portable-narrow-scalars
npm test
```

The demo calls the generated export with raw i32 arguments 255 and 32768. They enter the managed method as signed 8-bit and 16-bit values -1 and -32768, giving -32769.
