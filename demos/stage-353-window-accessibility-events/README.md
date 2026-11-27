# Stage 353 · WinForms accessibility notifications

The original `USER32!NotifyWinEvent` binding passes the event code, window handle, and signed object and child identifiers to a launch-local browser listener. The x86 CLR oracle installs a Windows accessibility hook and observes the same fixture calls. Valid windows deliver the events in order. Null, invalid and destroyed handles produce no delivery.

```sh
npm run demo -- stage-353-window-accessibility-events --no-rebuild
```

The runner compares Windows, Node and Edge and checks the void ABI, the sole pointer-free host import and isolation across launches. Within the host, the listener runs before the import returns. The browser launcher forwards the notification as a bubbling `standardwasm-winevent` event on its drop target. This is the event transport. Guest hook registration, `WM_GETOBJECT`, COM accessibility objects and browser accessibility-tree mapping are separate compatibility work.
