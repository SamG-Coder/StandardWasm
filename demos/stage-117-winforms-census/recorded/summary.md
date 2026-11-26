# WinForms blocker census

Entry `WinFormsReference.Program::Main`. Reached 28357 methods (27117 with IL), 1695 instantiated types, 2867 initialized types, 2048 virtual slots, 0 unresolved references.

Whole-program verdict from `Program.Main`: status -2 at [WinFormsReference, MethodDef 3, IL 0x0022] (value-type newobj: `System.Drawing.Size::.ctor`).

| Compiler verdict | Methods |
| --- | ---: |
| judged | 27117 |
| compiles | 2099 |
| blockedInOwnBody | 13373 |
| blockedInClosure | 2684 |
| exportAbiNoVerdict | 8961 |
| notJudged | 1240 |

## Blocker categories by reached methods whose own body stops there

| Category | Methods |
| --- | ---: |
| method-level: locals without localsinit ([SkipLocalsInit]) | 3450 |
| method-level: no distinguishing signature or local type | 1545 |
| method-level: declaring reference type | 1438 |
| static constructor body/header | 577 |
| method-level: member of generic type | 567 |
| method-level: value-type instance call | 560 |
| method-level: generic method call | 502 |
| static field with type initializer | 480 |
| method-level: reference type | 443 |
| virtual dispatch (receiver not proven exact) | 425 |
| method-level: throw | 355 |
| opcode: throw | 278 |
| call (linked target or signature) | 258 |
| generic method instantiation | 233 |
| instance field ldflda | 168 |
| static field | 151 |
| method-level: interface call | 138 |
| method-level: reflection: ldtoken | 119 |
| method-level: indirect load/store | 106 |
| method-level: unsafe pointer (signature) | 106 |
| method-level: managed byref | 99 |
| opcode: ldloca.s | 94 |
| catch clause type | 87 |
| ldtoken (runtime handles) | 86 |
| method-level: calls P/Invoke | 86 |
| call to P/Invoke | 75 |
| opcode: box | 70 |
| method-level: foreign type initializer trigger | 67 |
| method-level: boxing | 66 |
| method-level: generic method definition | 64 |
| method-level: value-type newobj | 63 |
| interface dispatch | 48 |
| callvirt (linked target or signature) | 47 |
| reference newobj (constructor or signature) | 43 |
| delegate target (ldftn) | 36 |
| value-type newobj | 30 |
| method-level: generic reference-type instantiation | 29 |
| opcode: castclass | 29 |
| static call that triggers a type initializer | 28 |
| opcode: ldarga.s | 27 |

## Blocker categories by reached methods whose compile stops there first (own or closure)

| Category | Methods |
| --- | ---: |
| method-level: locals without localsinit ([SkipLocalsInit]) | 3788 |
| method-level: no distinguishing signature or local type | 1545 |
| method-level: declaring reference type | 1438 |
| catch clause type | 1402 |
| static field with type initializer | 616 |
| method-level: value-type instance call | 589 |
| static constructor body/header | 577 |
| method-level: member of generic type | 567 |
| virtual dispatch (receiver not proven exact) | 534 |
| method-level: generic method call | 502 |
| call (linked target or signature) | 467 |
| method-level: reference type | 443 |
| opcode: throw | 396 |
| method-level: throw | 356 |
| generic method instantiation | 330 |
| static field | 201 |
| instance field ldflda | 200 |
| method-level: interface call | 139 |
| opcode: ldloca.s | 136 |
| method-level: reflection: ldtoken | 119 |
| ldtoken (runtime handles) | 116 |
| method-level: indirect load/store | 106 |
| method-level: unsafe pointer (signature) | 106 |
| method-level: managed byref | 99 |
| call to P/Invoke | 95 |
| opcode: box | 87 |
| method-level: calls P/Invoke | 86 |
| method-level: foreign type initializer trigger | 67 |
| method-level: boxing | 66 |
| method-level: generic method definition | 64 |
| interface dispatch | 63 |
| method-level: value-type newobj | 63 |
| callvirt (linked target or signature) | 60 |
| reference newobj (constructor or signature) | 58 |
| value-type newobj | 44 |
| delegate target (ldftn) | 42 |
| opcode: ldarga.s | 38 |
| static call that triggers a type initializer | 34 |
| opcode: castclass | 32 |
| method-level: generic reference-type instantiation | 29 |

## Method-level suspects

Types and static features of method-level failures that occur in no reached method whose own body fully lowered (compiles or blocked only in a callee).

| Failed methods | Kind | Type |
| ---: | --- | --- |
| 5934 | locals without localsinit ([SkipLocalsInit]) | `feature: locals without localsinit ([SkipLocalsInit])` |
| 4568 | value-type instance call | `feature: value-type instance call` |
| 3213 | generic method call | `feature: generic method call` |
| 3054 | value-type instance method | `feature: value-type instance method` |
| 2320 | indirect load/store | `feature: indirect load/store` |
| 1843 | member of generic type | `feature: member of generic type` |
| 1530 | Span<T> (signature) | `feature: Span<T> (signature)` |
| 1496 | value-type newobj | `feature: value-type newobj` |
| 1482 | throw | `feature: throw` |
| 1095 | generic method definition | `feature: generic method definition` |
| 1043 | unsafe: Unsafe/MemoryMarshal | `feature: unsafe: Unsafe/MemoryMarshal` |
| 988 | interface call | `feature: interface call` |
| 955 | boxing | `feature: boxing` |
| 789 | Span<T> (local) | `feature: Span<T> (local)` |
| 778 | reflection: ldtoken | `feature: reflection: ldtoken` |
| 777 | calls P/Invoke | `feature: calls P/Invoke` |
| 760 | unsafe pointer (local) | `feature: unsafe pointer (local)` |
| 739 | unsafe pointer (signature) | `feature: unsafe pointer (signature)` |
| 733 | foreign type initializer trigger | `feature: foreign type initializer trigger` |
| 714 | reflection: typeof | `feature: reflection: typeof` |
| 609 | Span<T> / ReadOnlySpan<T> | `System.ReadOnlySpan`1<Byte>` |
| 591 | constrained. call | `feature: constrained. call` |
| 577 | static constructor | `feature: static constructor` |
| 568 | Span<T> / ReadOnlySpan<T> | `System.ReadOnlySpan`1<Char>` |
| 549 | managed byref | `Int32&` |
| 520 | pinned local (fixed) | `feature: pinned local (fixed)` |
| 511 | interface type | `System.IFormatProvider` |
| 431 | generic parameter | `!0` |
| 407 | generic parameter | `!!0` |
| 371 | value type | `System.Drawing.Color` |
| 334 | sizeof | `feature: sizeof` |
| 317 | unmanaged pointer | `Char*` |
| 306 | unmanaged pointer | `Byte*` |
| 299 | stackalloc (localloc) | `feature: stackalloc (localloc)` |
| 288 | Span<T> / ReadOnlySpan<T> | `System.Span`1<Char>` |
| 287 | value type | `System.DateTime` |
| 276 | Span<T> / ReadOnlySpan<T> | `System.Span`1<Byte>` |
| 252 | delegate or function pointer creation (ldftn) | `feature: delegate or function pointer creation (ldftn)` |
| 239 | pinned local | `Char& pinned` |
| 237 | delegate allocation | `feature: delegate allocation` |

## Top blocker sites

| Blocks | Site | IL | Category | Operand |
| ---: | --- | --- | --- | --- |
| 832 | `System.SR::GetResourceString` (System.Private.CoreLib#8021) | IL_0014 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 166 | `System.SR::GetResourceString` (System.Text.Json#13) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 144 | `System.SR::GetResourceString` (System.Windows.Forms#114) | IL_0000 | call (linked target or signature) | `System.SR::get_ResourceManager` |
| 43 | `System.SR::GetResourceString` (System.Net.Requests#4) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 32 | `System.SR::GetResourceString` (System.ComponentModel.TypeConverter#13) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 30 | `System.Private.Windows.Core.Resources.SR::GetResourceString` (System.Private.Windows.Core#2535) | IL_0000 | call (linked target or signature) | `System.Private.Windows.Core.Resources.SR::get_ResourceManager` |
| 29 | `System.SR::GetResourceString` (System.Net.Sockets#105) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 26 | `System.SR::GetResourceString` (System.Drawing.Common#37) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 24 | `System.Collections.Generic.List`1::.ctor` (System.Private.CoreLib#42045) | IL_0007 | static field with type initializer | `System.Collections.Generic.List`1::s_emptyArray` |
| 23 | `System.SR::GetResourceString` (System.ComponentModel.Primitives#4) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 22 | `System.SR::GetResourceString` (System.Formats.Asn1#3) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 21 | `System.SR::GetResourceString` (System.Private.Uri#277) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 20 | `System.SR::GetResourceString` (System.Collections#3) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 16 | `System.SR::GetResourceString` (System.Security.Principal.Windows#65) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 15 | `System.SR::GetResourceString` (System.Formats.Nrbf#3) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 13 | `System.ArgumentNullException::Throw` (System.Private.CoreLib#3229) | IL_0006 | opcode: throw | `` |
| 12 | `System.Delegate::Combine` (System.Private.CoreLib#1202) | IL_0007 | virtual dispatch (receiver not proven exact) | `System.Delegate::CombineImpl` |
| 12 | `System.Text.Encoding::.ctor` (System.Private.CoreLib#18895) | IL_0013 | generic method instantiation | `System.ArgumentOutOfRangeException::ThrowIfNegative` |
| 12 | `System.Text.Json.Serialization.JsonConverter::.ctor` (System.Text.Json#2556) | IL_0012 | ldtoken (runtime handles) | `System.Text.Json.Serialization.JsonConverter` |
| 11 | `System.SR::GetResourceString` (System.Collections.Specialized#3) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 11 | `System.SR::GetResourceString` (System.Diagnostics.DiagnosticSource#12) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 11 | `System.Collections.Generic.List`1::.ctor` (System.Private.CoreLib#42046) | IL_0017 | static field with type initializer | `System.Collections.Generic.List`1::s_emptyArray` |
| 11 | `System.SR::GetResourceString` (System.Security.Cryptography#537) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 10 | `System.Threading.Lock::.ctor` (System.Private.CoreLib#16441) | IL_0007 | static field with type initializer | `System.Threading.Lock::s_maxSpinCount` |
| 10 | `System.Convert::ToInt32` (System.Private.CoreLib#3962) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 10 | `System.OperatingSystem::IsOSVersionAtLeast` (System.Private.CoreLib#7199) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 9 | `System.SR::GetResourceString` (Microsoft.Win32.Registry#43) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 9 | `System.SR::GetResourceString` (System.Collections.Concurrent#8) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 9 | `System.Drawing.SystemPens::FromSystemColor` (System.Drawing.Common#1402) | method | method-level: value-type instance call | `feature: value-type instance call` |
| 9 | `System.SR::GetResourceString` (System.Net.Security#76) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 9 | `System.Numerics.BitOperations::Log2SoftwareFallback` (System.Private.CoreLib#11514) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 9 | `System.Delegate::Remove` (System.Private.CoreLib#1219) | IL_001d | opcode: throw | `` |
| 9 | `System.Enum::ValidateRuntimeType` (System.Private.CoreLib#1303) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 9 | `System.BitConverter::SingleToUInt32Bits` (System.Private.CoreLib#3409) | IL_0001 | generic method instantiation | `System.Runtime.CompilerServices.Unsafe::BitCast` |
| 9 | `System.Convert::ThrowByteOverflowException` (System.Private.CoreLib#3833) | IL_000a | opcode: throw | `` |
| 9 | `System.Convert::ThrowSByteOverflowException` (System.Private.CoreLib#3834) | IL_000a | opcode: throw | `` |
| 9 | `System.SR::GetResourceString` (System.Reflection.Metadata#28) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 9 | `System.Windows.Forms.ToolStripManager/ModalMenuFilter::get_Instance` (System.Windows.Forms#25047) | IL_0000 | static field | `System.Windows.Forms.ToolStripManager/ModalMenuFilter::t_instance` |
| 9 | `System.Windows.Forms.Control::get_IsHandleCreated` (System.Windows.Forms#3201) | IL_0006 | virtual dispatch (receiver not proven exact) | `System.Windows.Forms.NativeWindow::get_Handle` |
| 8 | `System.Threading.ThreadPool::get_UseWindowsThreadPool` (System.Private.CoreLib#16930) | IL_0000 | static field with type initializer | `System.Threading.ThreadPool::<UseWindowsThreadPool>k__BackingField` |
| 8 | `System.String::Equals` (System.Private.CoreLib#2439) | IL_0002 | opcode: bne.un.s | `` |
| 8 | `System.String::IsNullOrEmpty` (System.Private.CoreLib#2492) | IL_0004 | callvirt (linked target or signature) | `System.String::get_Length` |
| 8 | `System.Collections.Hashtable::.ctor` (System.Private.CoreLib#41170) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 8 | `System.Windows.Forms.Control::set_Visible` (System.Windows.Forms#3312) | IL_0002 | virtual dispatch (receiver not proven exact) | `System.Windows.Forms.Control::SetVisibleCore` |
| 7 | `System.Drawing.SystemBrushes::FromSystemColor` (System.Drawing.Common#1340) | IL_0000 | opcode: ldarga.s | `` |
| 7 | `System.SR::GetResourceString` (System.Net.Primitives#13) | IL_0019 | catch clause type | `System.Resources.MissingManifestResourceException` |
| 7 | `System.Globalization.GregorianCalendarHelper::GetYearOffset` (System.Private.CoreLib#13885) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 7 | `System.Environment::get_TickCount64` (System.Private.CoreLib#1417) | IL_0000 | call to P/Invoke | `Interop/Kernel32::GetTickCount64` |
| 7 | `System.GC::SuppressFinalize` (System.Private.CoreLib#1512) | IL_0011 | value-type instance call (managed receiver address) | `System.Runtime.CompilerServices.MethodTable::get_HasFinalizer` |
| 7 | `System.Runtime.CompilerServices.ConditionalWeakTable`2::.ctor` (System.Private.CoreLib#33325) | IL_0007 | reference newobj (constructor or signature) | `System.Object::.ctor` |
| 7 | `System.Convert::ThrowCharOverflowException` (System.Private.CoreLib#3832) | IL_000a | opcode: throw | `` |
| 7 | `System.Convert::ThrowInt16OverflowException` (System.Private.CoreLib#3835) | IL_000a | opcode: throw | `` |
| 7 | `System.Convert::ThrowUInt16OverflowException` (System.Private.CoreLib#3836) | IL_000a | opcode: throw | `` |
| 7 | `Windows.Win32.PInvokeCore::SystemParametersInfoBool` (System.Private.Windows.Core#80) | IL_0003 | opcode: ldloca.s | `` |
| 7 | `System.Windows.Forms.ToolStrip::get_OverflowButton` (System.Windows.Forms#11650) | IL_003c | opcode: ldloca.s | `` |
| 6 | `System.ComponentModel.TypeDescriptor::GetPropertiesImpl` (System.ComponentModel.TypeConverter#1021) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 6 | `System.ComponentModel.ReflectTypeDescriptionProvider::GetTypeData` (System.ComponentModel.TypeConverter#862) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 6 | `System.Environment::get_CurrentManagedThreadId` (System.Private.CoreLib#1352) | method | runtime internal call | `` |
| 6 | `System.Threading.ExecutionContext::Capture` (System.Private.CoreLib#16365) | method | method-level: locals without localsinit ([SkipLocalsInit]) | `` |
| 6 | `System.Runtime.InteropServices.Marshal::GetLastPInvokeError` (System.Private.CoreLib#31440) | method | runtime internal call | `` |

## Static features of reached methods

| Feature | Reached methods | Own-body compiler blocks |
| --- | ---: | ---: |
| managed address (ldloca/ldflda) | 6349 | 6343 |
| locals without localsinit ([SkipLocalsInit]) | 6011 | 6011 |
| value-type instance call | 4851 | 4851 |
| virtual call | 4725 | 4703 |
| static field | 3751 | 3742 |
| string literal | 3723 | 2258 |
| generic method call | 3565 | 3565 |
| value-type field | 3111 | 3105 |
| value-type instance method | 3057 | 3054 |
| indirect load/store | 2336 | 2336 |
| member of generic type | 1984 | 1843 |
| throw | 1949 | 1949 |
| type test or cast | 1697 | 1623 |
| value-type newobj | 1562 | 1562 |
| Span<T> (signature) | 1538 | 1530 |
| reflection: API call | 1434 | 1339 |
| unsafe pointer (signature) | 1245 | 739 |
| generic method definition | 1109 | 1095 |
| try/finally | 1109 | 1108 |
| interface call | 1108 | 1108 |
| unsafe: Unsafe/MemoryMarshal | 1072 | 1072 |
| boxing | 1042 | 1042 |
| globalization | 1003 | 934 |
| foreign type initializer trigger | 938 | 938 |
| calls P/Invoke | 928 | 928 |
| reflection: ldtoken | 873 | 873 |
| P/Invoke | 831 | 0 |
| reflection: typeof | 807 | 807 |
| Span<T> (local) | 792 | 792 |
| unsafe pointer (local) | 771 | 771 |
| array allocation | 636 | 634 |
| constrained. call | 606 | 606 |
| static constructor | 577 | 577 |
| pinned local (fixed) | 520 | 520 |
| interop: Marshal | 471 | 458 |
| try/catch | 409 | 408 |
| threading: Tasks | 385 | 358 |
| GC API | 383 | 373 |
| sizeof | 343 | 343 |
| delegate or function pointer creation (ldftn) | 336 | 336 |
| threading: volatile. prefix | 325 | 279 |
| delegate allocation | 320 | 320 |
| threading: Interlocked | 310 | 309 |
| stackalloc (localloc) | 301 | 301 |
| floating point | 299 | 247 |
| switch | 282 | 266 |
| threading: Thread | 247 | 229 |
| threading: Monitor | 236 | 234 |
| runtime helpers | 227 | 226 |
| array element address | 208 | 208 |
| interop: COM marshalling | 202 | 197 |
| explicit layout (field access) | 185 | 185 |
| resources | 166 | 163 |
| runtime delegate method | 166 | 0 |
| platform: environment | 129 | 115 |
| interop: SafeHandle | 126 | 118 |
| checked arithmetic | 122 | 120 |
| reflection: activation (not modeled by RTA) | 115 | 93 |
| threading: CancellationToken | 115 | 106 |
| runtime internal call | 113 | 0 |
| rethrow | 111 | 111 |
| threading: Lock | 109 | 101 |
| threading: Volatile | 90 | 89 |
| indirect call (calli) | 89 | 89 |
| interop: GCHandle | 83 | 80 |
| exception filter | 65 | 65 |
| explicit layout (declaring type) | 63 | 58 |
| threading: WaitHandle | 55 | 49 |
| platform: filesystem | 52 | 50 |
| threading: ExecutionContext | 49 | 44 |
| threading: Timer | 35 | 27 |
| threading: ManualResetEvent | 34 | 23 |
| threading: EventWaitHandle | 32 | 28 |
| function pointer (signature) | 31 | 14 |
| threading: AsyncLocal | 30 | 29 |
| threading: SynchronizationContext | 29 | 26 |
| platform: registry | 23 | 17 |
| threading: ReaderWriterLock | 21 | 18 |
| threading: SpinWait | 20 | 20 |
| threading: LazyInitializer | 18 | 18 |
| threading: SemaphoreSlim | 15 | 15 |
| threading: SpinLock | 15 | 15 |
| interop: COM call | 12 | 12 |
| checked native-int conversion | 11 | 10 |
| threading: AutoResetEvent | 8 | 8 |
| no IL body | 5 | 0 |
| try/fault | 2 | 2 |
| raw memory block (cpblk/initblk) | 1 | 1 |

## P/Invoke surface

Reached 773 distinct DLL entry points (831 P/Invoke methods). The loaded Windows Desktop assemblies declare 994 distinct entry points.

| DLL | Reached | Reached from Windows Desktop | Declared by Windows Desktop | Reached entry points |
| --- | ---: | ---: | ---: | --- |
| user32.dll | 149 | 148 | 201 | AdjustWindowRectEx, AdjustWindowRectExForDpi, AreDpiAwarenessContextsEqual, BeginPaint, CallNextHookEx, CallWindowProcW, ChildWindowFromPointEx, ClientToScreen, CloseDesktop, CopyImage, CreateIconFromResourceEx, CreateMenu, CreateWindowExW, DefFrameProcW, DefMDIChildProcW, DefWindowProcW, DestroyCursor, DestroyIcon, DestroyMenu, DestroyWindow, DispatchMessageW, DrawEdge, DrawFrameControl, DrawIconEx, DrawMenuBar, DrawTextExW, EnableMenuItem, EnableWindow, EndPaint, EnumDisplayMonitors, EnumThreadWindows, FillRect, GetActiveWindow, GetAncestor, GetAwarenessFromDpiAwarenessContext, GetCapture, GetClassInfoW, GetClassNameW, GetClientRect, GetClipboardFormatNameW, GetCursor, GetCursorPos, GetDC, GetDlgItem, GetDoubleClickTime, GetDpiForSystem, GetDpiForWindow, GetFocus, GetForegroundWindow, GetIconInfo, GetKeyState, GetKeyboardLayout, GetKeyboardState, GetMenu, GetMenuItemCount, GetMenuItemInfoW, GetMessagePos, GetMessageW, GetMonitorInfoW, GetParent, GetProcessWindowStation, GetScrollInfo, GetSysColor, GetSysColorBrush, GetSystemMenu, GetSystemMetrics, GetSystemMetricsForDpi, GetThreadDpiAwarenessContext, GetThreadDpiHostingBehavior, GetUserObjectInformationW, GetWindow, GetWindowDpiAwarenessContext, GetWindowLongPtrW, GetWindowLongW, GetWindowPlacement, GetWindowRect, GetWindowTextLengthW, GetWindowTextW, GetWindowThreadProcessId, HideCaret, InvalidateRect, InvalidateRgn, IsChild, IsDialogMessageW, IsValidDpiAwarenessContext, IsWindow, IsWindowEnabled, IsWindowVisible, KillTimer, LoadCursorW, LoadStringW, MapWindowPoints, MonitorFromPoint, MonitorFromRect, MonitorFromWindow, MsgWaitForMultipleObjectsEx, NotifyWinEvent, OpenInputDesktop, PeekMessageW, PostMessageW, PostQuitMessage, PostThreadMessageW, RedrawWindow, RegisterClassW, RegisterClipboardFormatW, RegisterWindowMessageW, ReleaseCapture, ReleaseDC, ScrollWindowEx, SendMessageTimeoutW, SendMessageW, SetActiveWindow, SetCapture, SetClassLongPtrW, SetClassLongW, SetCursor, SetCursorPos, SetFocus, SetForegroundWindow, SetLayeredWindowAttributes, SetMenu, SetParent, SetProcessDPIAware, SetProcessDpiAwarenessContext, SetScrollInfo, SetThreadDpiAwarenessContext, SetThreadDpiHostingBehavior, SetTimer, SetWindowDisplayAffinity, SetWindowLongPtrW, SetWindowLongW, SetWindowPlacement, SetWindowPos, SetWindowRgn, SetWindowTextW, SetWindowsHookExW, ShowCaret, ShowWindow, SystemParametersInfoForDpi, SystemParametersInfoW, TrackMouseEvent, TranslateMDISysAccel, TranslateMessage, UnhookWindowsHookEx, UnregisterClassW, UpdateWindow, ValidateRect, WaitMessage, WindowFromPoint |
| qcall.dll | 164 | 0 | 0 | AppDomain_CreateDynamicAssembly, Array_CreateInstance, AssemblyNative_GetEntryAssembly, AssemblyNative_GetExecutingAssembly, AssemblyNative_GetFlags, AssemblyNative_GetLoadedAssemblies, AssemblyNative_GetPublicKey, AssemblyNative_GetSimpleName, AssemblyNative_GetTypeCore, AssemblyNative_GetTypeCoreIgnoreCase, AssemblyNative_GetVersion, AssemblyNative_InitializeAssemblyLoadContext, AssemblyNative_InternalLoad, AssemblyNative_IsApplyUpdateSupported, AssemblyNative_PrepareForAssemblyLoadContextRelease, AssemblyNative_TraceAssemblyResolveHandlerInvoked, Buffer_Clear, Buffer_MemMove, ComWeakRefToObject, ComWrappers_AllocateRefCountedHandle, ComWrappers_GetIReferenceTrackerTargetVftbl, ComWrappers_GetIUnknownImpl, ComWrappers_GetTaggedImpl, ComWrappers_GetUntrackedAddRefRelease, ComWrappers_RegisterIsRootedCallback, CustomAttribute_CreateCustomAttributeInstance, CustomAttribute_CreatePropertyOrFieldData, CustomAttribute_ParseAttributeUsageAttribute, DebugDebugger_Break, DebugDebugger_CustomNotification, DebugDebugger_IsManagedDebuggerAttached, DebugDebugger_Log, Delegate_BindToMethodInfo, Delegate_FindMethodHandle, Delegate_GetMulticastInvokeSlow, Delegate_InternalEqualMethodHandles, DependentHandle_InternalAllocWithGCTransition, DependentHandle_InternalFreeWithGCTransition, Enum_GetValuesAndNames, Environment_Exit, Environment_FailFast, Environment_GetProcessorCount, EventPipeInternal_CreateProvider, EventPipeInternal_DefineEvent, EventPipeInternal_DeleteProvider, EventPipeInternal_EventActivityIdControl, EventPipeInternal_WriteEventData, ExceptionNative_GetFrozenStackTrace, ExceptionNative_GetMessageFromNativeResources, ExceptionNative_GetMethodFromStackTrace, FileLoadException_GetMessageForHR, GCHandle_InternalAllocWithGCTransition, GCHandle_InternalFreeWithGCTransition, GCInterface_AddMemoryPressure, GCInterface_AllocateNewArray, GCInterface_Collect, GCInterface_GetTotalAllocatedBytesPrecise, GCInterface_ReRegisterForFinalize, GCInterface_RemoveMemoryPressure, GetFileLoadExceptionMessage, GetTypeLoadExceptionMessage, IsInstanceOf_NoCacheLookup, MarshalNative_GetExceptionForHR, MarshalNative_GetFunctionPointerForDelegateInternal, MarshalNative_GetHRForException, MarshalNative_GetIUnknownForObject, MarshalNative_GetObjectForIUnknown, MarshalNative_IsBuiltInComSupported, MarshalNative_SizeOfHelper, MarshalNative_TryGetStructMarshalStub, MdUtf8String_EqualsCaseInsensitive, MetadataImport_Enum, MethodTable_AreTypesEquivalent, MethodTable_CanCompareBitsOrUseFastGetHashCode, ModuleBuilder_GetMemberRef, ModuleBuilder_GetMemberRefFromSignature, ModuleBuilder_GetMemberRefOfMethodInfo, ModuleBuilder_GetTokenFromTypeSpec, ModuleBuilder_GetTypeRef, ModuleHandle_GetDynamicMethod, ModuleHandle_GetModuleType, ModuleHandle_ResolveMethod, ModuleHandle_ResolveType, Monitor_Enter_Slowpath, Monitor_Exit_Slowpath, Monitor_GetLockContentionCount, Monitor_Pulse, Monitor_PulseAll, Monitor_Wait, NativeLibrary_GetSymbol, NativeLibrary_LoadByName, NativeRuntimeEventSource_LogContentionLockCreated, NativeRuntimeEventSource_LogContentionStart, NativeRuntimeEventSource_LogContentionStop, NativeRuntimeEventSource_LogThreadPoolIODequeue, NativeRuntimeEventSource_LogThreadPoolIOEnqueue, NativeRuntimeEventSource_LogThreadPoolIOPack, NativeRuntimeEventSource_LogThreadPoolMinMaxThreads, NativeRuntimeEventSource_LogThreadPoolWorkerThreadAdjustmentAdjustment, NativeRuntimeEventSource_LogThreadPoolWorkerThreadAdjustmentSample, NativeRuntimeEventSource_LogThreadPoolWorkerThreadAdjustmentStats, NativeRuntimeEventSource_LogThreadPoolWorkerThreadStart, NativeRuntimeEventSource_LogThreadPoolWorkerThreadStop, NativeRuntimeEventSource_LogThreadPoolWorkerThreadWait, NativeRuntimeEventSource_LogThreadPoolWorkingThreadCount, ObjectNative_AllocateUninitializedClone, ObjectNative_GetHashCodeSlow, ObjectToComWeakRef, QCall_FreeGCHandleForTypeHandle, QCall_GetGCHandleForTypeHandle, ReflectionInvocation_CompileMethod, ReflectionInvocation_RunClassConstructor, ReflectionInvocation_RunModuleConstructor, RuntimeFieldHandle_GetRVAFieldInfo, RuntimeFieldHandle_GetValue, RuntimeFieldHandle_SetValue, RuntimeMethodHandle_ConstructInstantiation, RuntimeMethodHandle_Destroy, RuntimeMethodHandle_GetMethodBody, RuntimeMethodHandle_GetMethodInstantiation, RuntimeMethodHandle_GetStubIfNeededSlow, RuntimeMethodHandle_GetTypicalMethodDefinition, RuntimeMethodHandle_InvokeMethod, RuntimeMethodHandle_IsCAVisibleFromDecoratedType, RuntimeMethodHandle_StripMethodInstantiation, RuntimeTypeHandle_AllocateTypeAssociatedMemory, RuntimeTypeHandle_CreateInstanceForAnotherGenericParameter, RuntimeTypeHandle_GetFields, RuntimeTypeHandle_GetInstantiation, RuntimeTypeHandle_GetInterfaces, RuntimeTypeHandle_GetMethodAt, RuntimeTypeHandle_GetModuleSlow, RuntimeTypeHandle_GetRuntimeTypeFromHandleSlow, RuntimeTypeHandle_InternalAlloc, RuntimeTypeHandle_InternalAllocNoChecks, RuntimeTypeHandle_RegisterCollectibleTypeDependency, Signature_AreEqual, Signature_GetCustomModifiersAtOffset, Signature_Init, StackTrace_GetStackFramesInternal, Stream_HasOverriddenSlow, ThreadNative_GetCurrentThread, ThreadNative_GetIsBackground, ThreadNative_GetThreadState, ThreadNative_InformThreadNameChange, ThreadNative_Initialize, ThreadNative_Join, ThreadNative_SetIsBackground, ThreadNative_SetPriority, ThreadNative_Sleep, ThreadNative_SpinWait, ThreadNative_Start, ThreadNative_YieldThread, ThrowInvalidCastException, TrackerObjectManager_HasReferenceTrackerManager, TrackerObjectManager_RegisterNativeObjectWrapperCache, TrackerObjectManager_TryRegisterReferenceTrackerManager, TypeBuilder_DefineCustomAttribute, TypeHandle_CanCastTo_NoCacheLookup, TypeHandle_GetCorElementType, UnsafeAccessors_ResolveGenericParamToTypeHandle, ValueType_GetHashCodeStrategy, WaitHandle_WaitMultipleIgnoringSyncContext, WaitHandle_WaitOneCore |
| gdiplus.dll | 139 | 139 | 524 | GdipAddPathArc, GdipBitmapGetPixel, GdipBitmapLockBits, GdipBitmapSetPixel, GdipBitmapUnlockBits, GdipCloneFontFamily, GdipCloneImage, GdipCloneRegion, GdipClosePathFigure, GdipCombineRegionRect, GdipCombineRegionRegion, GdipCreateBitmapFromHBITMAP, GdipCreateBitmapFromHICON, GdipCreateBitmapFromScan0, GdipCreateBitmapFromStream, GdipCreateBitmapFromStreamICM, GdipCreateFont, GdipCreateFontFamilyFromName, GdipCreateFontFromLogfontW, GdipCreateFromHDC, GdipCreateFromHWND, GdipCreateHBITMAPFromBitmap, GdipCreateHalftonePalette, GdipCreateImageAttributes, GdipCreateLineBrushFromRect, GdipCreateMatrix, GdipCreatePath, GdipCreatePen1, GdipCreatePen2, GdipCreateRegion, GdipCreateRegionRect, GdipCreateSolidFill, GdipCreateStringFormat, GdipCreateTexture, GdipDeleteBrush, GdipDeleteFont, GdipDeleteFontFamily, GdipDeleteGraphics, GdipDeleteMatrix, GdipDeletePath, GdipDeletePen, GdipDeleteRegion, GdipDeleteStringFormat, GdipDisposeImage, GdipDisposeImageAttributes, GdipDrawImage, GdipDrawImageRect, GdipDrawImageRectRect, GdipDrawLine, GdipDrawLinesI, GdipDrawPath, GdipDrawRectangle, GdipDrawString, GdipFillPath, GdipFillPolygonI, GdipFillRectangle, GdipFillRectanglesI, GdipGetClip, GdipGetDC, GdipGetDpiY, GdipGetEmHeight, GdipGetFamily, GdipGetFamilyName, GdipGetFontHeight, GdipGetFontSize, GdipGetFontStyle, GdipGetFontUnit, GdipGetGenericFontFamilySansSerif, GdipGetImageFlags, GdipGetImageGraphicsContext, GdipGetImageHeight, GdipGetImagePixelFormat, GdipGetImageRawFormat, GdipGetImageType, GdipGetImageWidth, GdipGetLineSpacing, GdipGetLogFontW, GdipGetMatrixElements, GdipGetNearestColor, GdipGetPenDashCount, GdipGetPropertyItem, GdipGetPropertyItemSize, GdipGetRegionBounds, GdipGetRegionHRgn, GdipGetSmoothingMode, GdipGetStringFormatAlign, GdipGetStringFormatFlags, GdipGetStringFormatHotkeyPrefix, GdipGetStringFormatLineAlign, GdipGetStringFormatTrimming, GdipGetTextRenderingHint, GdipGetTextureTransform, GdipGetWorldTransform, GdipGraphicsClear, GdipImageForceValidation, GdipImageGetFrameCount, GdipImageGetFrameDimensionsCount, GdipImageGetFrameDimensionsList, GdipImageRotateFlip, GdipImageSelectActiveFrame, GdipIsEmptyRegion, GdipIsInfiniteRegion, GdipIsMatrixEqual, GdipLoadImageFromStream, GdipLoadImageFromStreamICM, GdipMeasureString, GdipNewInstalledFontCollection, GdipReleaseDC, GdipRestoreGraphics, GdipSaveGraphics, GdipSetClipRect, GdipSetClipRegion, GdipSetImageAttributesColorKeys, GdipSetImageAttributesColorMatrix, GdipSetImageAttributesRemapTable, GdipSetImageAttributesWrapMode, GdipSetInterpolationMode, GdipSetPageUnit, GdipSetPenColor, GdipSetPenDashArray, GdipSetPenDashOffset, GdipSetPenDashStyle, GdipSetPenMode, GdipSetSmoothingMode, GdipSetSolidFillColor, GdipSetStringFormatAlign, GdipSetStringFormatFlags, GdipSetStringFormatHotkeyPrefix, GdipSetStringFormatLineAlign, GdipSetStringFormatTrimming, GdipSetTextRenderingHint, GdipSetTextureTransform, GdipStartPathFigure, GdipTranslateClip, GdipTranslateLineTransform, GdipTranslateMatrix, GdipTranslateRegion, GdipTranslateWorldTransform, GdiplusStartup |
| kernel32.dll | 108 | 23 | 35 | ActivateActCtx, CancelIoEx, CancelThreadpoolIo, CloseHandle, CloseThreadpoolWait, CloseThreadpoolWork, CompareStringEx, CompareStringOrdinal, CreateActCtxW, CreateEventExW, CreateFileW, CreateIoCompletionPort, CreateThreadpoolIo, CreateThreadpoolTimer, CreateThreadpoolWait, CreateThreadpoolWork, DeactivateActCtx, DeleteCriticalSection, DeleteFileW, DeviceIoControl, DuplicateHandle, EnterCriticalSection, EnumCalendarInfoExEx, EnumSystemLocalesEx, EnumTimeFormatsEx, ExpandEnvironmentStringsW, FileTimeToSystemTime, FindFirstFileExW, FindNLSStringEx, FindStringOrdinal, FlushFileBuffers, FormatMessageW, FreeLibrary, GetCPInfo, GetCalendarInfoEx, GetCurrentActCtx, GetCurrentProcess, GetCurrentProcessId, GetCurrentProcessorNumberEx, GetCurrentThread, GetCurrentThreadId, GetDynamicTimeZoneInformation, GetEnvironmentVariableW, GetExitCodeThread, GetFileAttributesExW, GetFileInformationByHandleEx, GetFileType, GetFullPathNameW, GetLastError, GetLocaleInfoEx, GetLongPathNameW, GetModuleHandleW, GetOverlappedResult, GetProcAddress, GetProcessTimes, GetQueuedCompletionStatus, GetQueuedCompletionStatusEx, GetShortPathNameW, GetStartupInfoW, GetSystemDefaultLCID, GetSystemDirectoryW, GetSystemTime, GetSystemTimes, GetThreadIOPendingFlag, GetThreadLocale, GetTickCount64, GetTimeZoneInformation, GetUserPreferredUILanguages, GlobalAlloc, GlobalFree, GlobalLock, GlobalSize, GlobalUnlock, InitializeConditionVariable, InitializeCriticalSection, K32GetProcessMemoryInfo, LCIDToLocaleName, LCMapStringEx, LeaveCriticalSection, LoadLibraryExW, LocalAlloc, LocalFree, LocaleNameToLCID, PostQueuedCompletionStatus, QueryPerformanceCounter, QueryPerformanceFrequency, QueryUnbiasedInterruptTime, ReadFile, ResetEvent, ResolveLocaleName, SetConsoleCtrlHandler, SetEvent, SetFileCompletionNotificationModes, SetFileInformationByHandle, SetLastError, SetThreadErrorMode, SetThreadpoolTimer, SetThreadpoolWait, Sleep, SleepConditionVariableCS, StartThreadpoolIo, SubmitThreadpoolWork, SystemTimeToFileTime, TzSpecificLocalTimeToSystemTime, WaitForThreadpoolWaitCallbacks, WakeConditionVariable, WideCharToMultiByte, WriteFile |
| gdi32.dll | 40 | 40 | 85 | BitBlt, CombineRgn, CreateBitmap, CreateCompatibleBitmap, CreateCompatibleDC, CreateDCW, CreateDIBSection, CreateFontIndirectW, CreatePen, CreateRectRgn, CreateSolidBrush, DeleteDC, DeleteObject, GetClipRgn, GetCurrentObject, GetDIBits, GetDeviceCaps, GetNearestColor, GetObjectType, GetObjectW, GetPaletteEntries, GetStockObject, GetTextExtentPoint32W, GetTextMetricsW, IntersectClipRect, LineTo, MoveToEx, OffsetViewportOrgEx, RealizePalette, Rectangle, RestoreDC, SaveDC, SelectClipRgn, SelectObject, SelectPalette, SetBkColor, SetBkMode, SetROP2, SetTextAlign, SetTextColor |
| ws2_32.dll | 27 | 0 | 0 | FreeAddrInfoExW, FreeAddrInfoW, GetAddrInfoExCancel, GetAddrInfoExW, GetAddrInfoW, GetNameInfoW, WSACleanup, WSAConnect, WSAGetOverlappedResult, WSAIoctl, WSARecv, WSASend, WSASocketW, WSAStartup, accept, bind, closesocket, getpeername, getsockname, getsockopt, ioctlsocket, listen, recv, select, send, setsockopt, shutdown |
| system.globalization.native.dll | 30 | 0 | 0 | GlobalizationNative_ChangeCase, GlobalizationNative_ChangeCaseInvariant, GlobalizationNative_ChangeCaseTurkish, GlobalizationNative_CloseSortHandle, GlobalizationNative_CompareString, GlobalizationNative_EndsWith, GlobalizationNative_EnumCalendarInfo, GlobalizationNative_GetCalendarInfo, GlobalizationNative_GetCalendars, GlobalizationNative_GetJapaneseEraStartDate, GlobalizationNative_GetLatestJapaneseEra, GlobalizationNative_GetLocaleInfoGroupingSizes, GlobalizationNative_GetLocaleInfoInt, GlobalizationNative_GetLocaleInfoString, GlobalizationNative_GetLocaleName, GlobalizationNative_GetLocaleTimeFormat, GlobalizationNative_GetLocales, GlobalizationNative_GetSortHandle, GlobalizationNative_GetSortKey, GlobalizationNative_IanaIdToWindowsId, GlobalizationNative_IndexOf, GlobalizationNative_InitICUFunctions, GlobalizationNative_InitOrdinalCasingPage, GlobalizationNative_IsPredefinedLocale, GlobalizationNative_LastIndexOf, GlobalizationNative_LoadICU, GlobalizationNative_NormalizeString, GlobalizationNative_StartsWith, GlobalizationNative_ToAscii, GlobalizationNative_ToUnicode |
| advapi32.dll | 21 | 0 | 2 | EnumerateTraceGuidsEx, EventActivityIdControl, EventRegister, EventSetInformation, EventUnregister, EventWriteTransfer, GetTokenInformation, ImpersonateLoggedOnUser, LsaLookupNames2, LsaLookupSids, LsaNtStatusToWinError, LsaOpenPolicy, OpenProcessToken, OpenThreadToken, RegCloseKey, RegEnumKeyExW, RegEnumValueW, RegNotifyChangeKeyValue, RegOpenKeyExW, RegQueryValueExW, RevertToSelf |
| ole32.dll | 14 | 10 | 21 | CoCreateGuid, CoCreateInstance, CoGetContextToken, CoRegisterMessageFilter, CoTaskMemAlloc, CoTaskMemFree, DoDragDrop, OleDuplicateData, OleInitialize, OleUninitialize, PropVariantClear, RegisterDragDrop, ReleaseStgMedium, RevokeDragDrop |
| crypt32.dll | 10 | 0 | 0 | CertControlStore, CertDuplicateCertificateContext, CertEnumCertificatesInStore, CertFreeCertificateContext, CertGetCertificateContextProperty, CertGetNameStringW, CertOpenStore, CryptFindOIDInfo, CryptProtectMemory, CryptUnprotectMemory |
| uxtheme.dll | 12 | 12 | 32 | CloseThemeData, DrawThemeBackground, DrawThemeParentBackground, GetCurrentThemeName, GetThemeAppProperties, GetThemeBackgroundContentRect, GetThemeColor, IsAppThemed, IsThemeBackgroundPartiallyTransparent, IsThemePartDefined, OpenThemeData, SetWindowTheme |
| comctl32.dll | 8 | 8 | 20 | ImageList_Add, ImageList_DrawEx, ImageList_GetImageCount, ImageList_GetImageInfo, ImageList_Remove, ImageList_Replace, ImageList_ReplaceIcon, InitCommonControlsEx |
| imm32.dll | 8 | 8 | 10 | ImmAssociateContext, ImmCreateContext, ImmGetContext, ImmGetConversionStatus, ImmGetOpenStatus, ImmReleaseContext, ImmSetConversionStatus, ImmSetOpenStatus |
| uiautomationcore.dll | 6 | 6 | 9 | UiaClientsAreListening, UiaDisconnectProvider, UiaRaiseAutomationEvent, UiaRaiseAutomationPropertyChangedEvent, UiaRaiseNotificationEvent, UiaReturnRawElementProvider |
| iphlpapi.dll | 5 | 0 | 0 | ConvertInterfaceLuidToIndex, ConvertInterfaceNameToLuidW, GetAdaptersAddresses, GetNetworkParams, GetPerAdapterInfo |
| shell32.dll | 5 | 5 | 15 | DragQueryFileW, FindExecutableW, SHDefExtractIconW, SHGetStockIconInfo, ShellExecuteW |
| ucrtbase.dll | 5 | 0 | 0 | _aligned_free, _aligned_malloc, calloc, free, malloc |
| ntdll.dll | 4 | 0 | 0 | NtQueryInformationFile, NtQuerySystemInformation, RtlGetVersion, RtlNtStatusToDosError |
| normaliz.dll | 3 | 0 | 0 | IdnToAscii, IdnToUnicode, NormalizeString |
| oleaut32.dll | 3 | 1 | 18 | LoadRegTypeLib, SysAllocStringLen, SysFreeString |
| api-ms-win-shcore-scaling-l1-1-1.dll | 2 | 2 | 2 | GetProcessDpiAwareness, SetProcessDpiAwareness |
| oleacc.dll | 2 | 2 | 2 | CreateStdAccessibleObject, LresultFromObject |
| winhttp.dll | 2 | 0 | 0 | WinHttpGetIEProxyConfigForCurrentUser, WinHttpOpen |
| bcrypt.dll | 1 | 0 | 0 | BCryptGenRandom |
| dwmapi.dll | 1 | 1 | 2 | DwmSetWindowAttribute |
| hhctrl.ocx.dll | 1 | 1 | 1 | HtmlHelpW |
| shlwapi.dll | 1 | 1 | 1 | SHAutoComplete |
| sspicli.dll | 1 | 0 | 0 | LsaGetLogonSessionData |
| wtsapi32.dll | 1 | 1 | 2 | WTSUnRegisterSessionNotification |

## Reached methods by assembly

| Assembly | Reached | First witness path from the entry |
| --- | ---: | --- |
| System.Private.CoreLib | 12662 | WinFormsReference.Program::Main (entry) → System.ComponentModel.Component::Finalize (finalizer) → System.Object::Finalize (call) |
| System.Windows.Forms | 5684 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) |
| System.Text.Json | 1890 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Private.Windows.JsonData`1::Deserialize (call) → System.Text.Json.JsonSerializer::Deserialize (call) |
| System.Private.Windows.Core | 1003 | WinFormsReference.Program::Main (entry) → ApplicationConfiguration::Initialize (call) → System.Windows.Forms.Application::EnableVisualStyles (call) → Windows.Win32.Foundation.HMODULE::op_Implicit (call) |
| System.ComponentModel.TypeConverter | 712 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.TextBox::Dispose (virtual dispatch) → System.ComponentModel.CollectionChangeEventHandler::.ctor (newobj) |
| System.Diagnostics.DiagnosticSource | 701 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Net.Http.HttpConnectionSettings::.ctor (newobj) → System.Diagnostics.DistributedContextPropagator::get_Current (call) |
| System.Windows.Forms.Primitives | 672 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Padding::.cctor (type initializer) |
| System.Drawing.Common | 578 | WinFormsReference.Program::Main (entry) → System.ComponentModel.Component::Finalize (finalizer) → System.Windows.Forms.Form::Dispose (virtual dispatch) → System.Drawing.Icon::Dispose (call) |
| System.Net.Sockets | 531 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Net.FtpWebRequest::CreateConnection (call) → System.Net.Sockets.Socket::.cctor (type initializer) |
| System.Net.Requests | 407 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Windows.Forms.PictureBox::StartLoadViaWebRequest (call) → System.Net.WebRequest::Create (call) |
| System.Security.Cryptography | 274 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Net.Security.CertificateHelper::GetEligibleClientCertificate (call) → System.Security.Cryptography.X509Certificates.X509CertificateCollection::GetHashCode (virtual dispatch) |
| System.Private.Uri | 272 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Windows.Forms.Help::GetHelpFileType (call) → System.Uri::get_Scheme (callvirt) |
| System.Formats.Nrbf | 251 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Private.Windows.Nrbf.SerializationRecordExtensions::TryGetObjectFromJson (call) → System.Formats.Nrbf.ClassRecord::GetRawValue (callvirt) |
| System.Drawing.Primitives | 217 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Drawing.Size::.ctor (newobj) |
| System.Collections | 187 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Windows.Forms.ToolStripManager::RevertMergeInternal (call) → System.Collections.Generic.Stack`1::GetEnumerator (callvirt) |
| System.Net.Security | 176 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Net.Http.HttpClientHandler::set_CheckCertificateRevocationList (callvirt) → System.Net.Security.SslClientAuthenticationOptions::set_CertificateRevocationCheckMode (callvirt) |
| System.Private.Windows.GdiPlus | 164 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control::Dispose (virtual dispatch) → … → System.Drawing.Font::Dispose (call) → Windows.Win32.PInvokeGdiPlus::GdipDeleteFont (call) |
| System.Net.NameResolution | 153 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Net.Sockets.Socket::Connect (callvirt) → System.Net.Dns::GetHostAddresses (call) |
| System.Net.Primitives | 153 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Net.Http.HttpConnectionSettings::.ctor (newobj) → System.Net.HttpVersion::.cctor (type initializer) |
| System.Collections.Specialized | 147 | WinFormsReference.Program::Main (entry) → System.ComponentModel.Component::Finalize (finalizer) → System.Windows.Forms.Form::Dispose (virtual dispatch) → System.Windows.Forms.Form::.cctor (type initializer) → System.Collections.Specialized.BitVector32::CreateSection (call) |
| System.Security.Principal.Windows | 135 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Net.ContextAwareResult::SafeCaptureIdentity (call) → System.Security.Principal.WindowsIdentity::GetCurrent (call) |
| System.Reflection.Metadata | 118 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Private.Windows.Ole.TypeBinder`1::BindToType (callvirt) → System.Reflection.Metadata.TypeName::get_AssemblyQualifiedName (callvirt) |
| System.Runtime.Numerics | 118 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Formats.Asn1.AsnDecoder::ReadObjectIdentifier (call) → System.Numerics.BigInteger::op_Implicit (call) |
| System.Net.Http | 115 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Windows.Forms.PictureBox::.cctor (type initializer) → System.Net.Http.HttpMessageHandler::Dispose (virtual dispatch) |
| System.Net.NetworkInformation | 107 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Net.Http.HttpWindowsProxy::UpdateConfiguration (call) → System.Net.NetworkInformation.NetworkInterface::GetAllNetworkInterfaces (call) |
| System.Collections.Concurrent | 95 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::FromCurrent (call) → System.Windows.Forms.Application/ThreadContext::.cctor (type initializer) → System.Collections.Concurrent.ConcurrentDictionary`2::.ctor (newobj) |
| Microsoft.Win32.SystemEvents | 93 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Windows.Forms.ToolStripManager::.cctor (type initializer) → Microsoft.Win32.UserPreferenceChangingEventHandler::.ctor (newobj) |
| System.Collections.Immutable | 91 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.TypeExtensions::Matches (call) → System.Collections.Immutable.ImmutableArray`1::get_Length (call) |
| System.ComponentModel.Primitives | 85 | WinFormsReference.Program::Main (entry) → System.ComponentModel.Component::Finalize (finalizer) |
| System.Text.Encodings.Web | 85 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Text.Json.JsonWriterHelper::NeedsEscaping (call) → System.Text.Encodings.Web.JavaScriptEncoder::get_Default (call) |
| System.Formats.Asn1 | 81 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension::DecodeX509EnhancedKeyUsageExtension (call) → System.Formats.Asn1.AsnReader::.ctor (newobj) |
| System.Memory | 69 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Text.Json.Utf8JsonReader::.ctor (call) → System.Buffers.ReadOnlySequence`1::.cctor (type initializer) |
| Microsoft.Win32.Registry | 57 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Button::.ctor (newobj) → … → System.Windows.Forms.Application::GetSystemColorModeInternal (call) → Microsoft.Win32.Registry::GetValue (call) |
| System.Linq | 48 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.ButtonBase::set_AutoSize (virtual dispatch) → … → System.Windows.Forms.ToolTip::RemoveAll (call) → System.Linq.Enumerable::ToArray (call) |
| System.Collections.NonGeneric | 39 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Windows.Forms.Application::get_OpenForms (call) → System.Collections.ReadOnlyCollectionBase::System.Collections.ICollection.CopyTo (virtual dispatch) |
| System.Threading | 39 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.ButtonBase::Dispose (virtual dispatch) → … → System.Drawing.ImageAnimator::Animate (call) → System.Threading.ReaderWriterLock::get_IsReaderLockHeld (callvirt) |
| System.Runtime.InteropServices | 35 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → WinFormsComStrategy::.cctor (type initializer) → System.Runtime.InteropServices.Marshalling.StrategyBasedComWrappers::ComputeVtables (virtual dispatch) |
| System.Net.WebHeaderCollection | 24 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Net.HttpWebRequest::.ctor (newobj) → System.Net.WebHeaderCollection::ToString (virtual dispatch) |
| System.ComponentModel.EventBasedAsync | 23 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → System.Windows.Forms.Application/ThreadContext::RunMessageLoopInner (call) → System.Windows.Forms.WindowsFormsSynchronizationContext::InstallIfNeeded (call) → System.ComponentModel.AsyncOperationManager::get_SynchronizationContext (call) |
| System.Security.Claims | 17 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Security.Principal.WindowsIdentity::.ctor (newobj) → System.Security.Claims.ClaimsIdentity::.ctor (call) |
| System.Text.RegularExpressions | 17 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Text.RegularExpressions.Generated.<RegexGenerator_g>FEBA907BFC8DA5891021ECFC4ACF51500CA89E8FDCD0895B025EB5D8CE91E77E9__CreateIntegerRegex_0::.cctor (type initializer) → System.Text.RegularExpressions.Regex::ToString (virtual dispatch) |
| System.Runtime.Serialization.Formatters | 13 | WinFormsReference.Program::Main (entry) → System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → … → System.Private.Windows.Ole.BinaryFormatUtilities`1::TryReadObjectFromStream (call) → System.Runtime.Serialization.Formatters.Binary.BinaryFormatter::.ctor (newobj) |
| System.ObjectModel | 8 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.ComponentModel.PropertyDescriptor::get_Converter (callvirt) → System.ComponentModel.TypeConverterAttribute::get_ConverterTypeName (callvirt) |
| WinFormsReference | 6 | WinFormsReference.Program::Main (entry) |
| System.ComponentModel | 4 | WinFormsReference.Program::Main (entry) → WinFormsReference.ReferenceForm::.ctor (newobj) → System.Windows.Forms.Control/ControlCollection::Add (callvirt) → … → System.Windows.Forms.ToolStrip::SetBoundsCore (virtual dispatch) → System.ComponentModel.CancelEventArgs::get_Cancel (callvirt) |
| System.IO.Pipelines | 1 | System.Windows.Forms.Application::Run (call) → System.Windows.Forms.Application/ThreadContext::RunMessageLoop (callvirt) → System.Windows.Forms.Application/ThreadContext::RunMessageLoopInner (call) → … → System.Text.Json.PooledByteBufferWriter::.ctor (newobj) → System.IO.Pipelines.PipeWriter::.ctor (call) |

