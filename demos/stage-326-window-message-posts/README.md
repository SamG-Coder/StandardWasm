# Stage 326 Window and thread message posts

Run `npm run demo -- stage-326-window-message-posts`. The same managed fixture posts and peeks messages on the Windows x86 CLR and through the StandardWasm launch host in Node and Edge.

The comparison covers one queue for window and thread posts, null destinations, HWND and child filtering, removal in queue order, rejected pointer-bearing system messages, and dropping messages after a window retires. The browser also checks isolation and a wrong HWND declaration. The launch models its own window tree and has no desktop broadcast.
