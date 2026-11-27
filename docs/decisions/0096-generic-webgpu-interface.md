# 0096 · A generic WebGPU interface for C# over webgpu.h

Status is proposed in #313. It records an intention and the agreed shape of the interface. The
milestones below land on the same branch and PR. This builds on decision 0083 (native imports),
and its full surface depends on #275 (pointers and structs).

## Problem

Stages 14, 17 and 69 draw with WebGPU, but the GPU calls live in hand-written JavaScript
(`web/render.mjs`) that reads geometry out of the Wasm module. A C# program cannot drive the GPU.
Each new renderer would need its own JS, and nothing written that way runs on the CLR.

Rust solved this with `wgpu`: applications code against one API. On native targets it runs on
Vulkan, Metal or D3D12, and on `wasm32` the same calls go to the browser's `navigator.gpu`.
C# needs the same thing, and it must keep StandardWasm's rule that the input is an ordinary
compiled .NET Standard DLL.

## Decision

**Target the standard C header, `webgpu.h`**
([webgpu-native/webgpu-headers](https://github.com/webgpu-native/webgpu-headers)), and bind it
with ordinary `[DllImport("webgpu")]`. This header is already the shared ABI of wgpu-native
(Rust's wgpu behind a C API), Dawn (Chromium's implementation) and Emscripten's `emdawnwebgpu`,
which implements it in JavaScript over linear memory. So one compiled DLL has a backend everywhere:

| Where the DLL runs | What answers `webgpu!wgpu*` |
| --- | --- |
| CLR on Windows, Linux, macOS | `webgpu.dll` / `libwebgpu.so` / `libwebgpu.dylib`: wgpu-native or Dawn, unmodified |
| StandardWasm in a browser | decision 0083 native imports, module `native:webgpu`, answered by `scripts/webgpu-imports.mjs` over `navigator.gpu` |

No StandardWasm-only GPU API is invented. The browser registry is one more implementation of
`webgpu.h`, keyed `"webgpu!<entry>"` as decision 0083 requires. A missing entry raises
`EntryPointNotFoundException` exactly as on the CLR.

### Layers (`src/WebGpu/`, assembly `StandardWasm.WebGpu`, netstandard2.0)

1. **Managed API** (`StandardWasm.WebGpu`): `GpuInstance`, `GpuAdapter`, `GpuDevice`, `GpuQueue`,
   `GpuBuffer`, `GpuTexture`, `GpuTextureView`, `GpuSampler`, `GpuShaderModule`,
   `GpuBindGroupLayout`, `GpuBindGroup`, `GpuPipelineLayout`, `GpuRenderPipeline`,
   `GpuComputePipeline`, `GpuCommandEncoder`, `GpuRenderPassEncoder`, `GpuComputePassEncoder`,
   `GpuCommandBuffer` and `GpuSurface`. Names, enums and flag values follow the WebGPU
   specification and `webgpu.h`, so the spec, MDN and wgpu documentation all apply. Descriptors are
   plain C# classes. Applications never see native layouts.
2. **Backend seam** (`IGpuBackend`, internal): the managed API calls a backend through opaque
   `IntPtr` handles. This is the counterpart of wgpu's `wgpu-core`/`webgpu` split. The backends are:
   - `NativeBackend`: `webgpu.h` P/Invoke (`Native/WebGpuNative.cs`). It marshals descriptors into
     header-layout structs and `WGPUStringView`.
   - `RecordingBackend`: records every call with its arguments and allocates fake handles. It lets
     a renderer's frame be tested deterministically on the CLR and in Wasm without a GPU. It also
     serves as the oracle for comparing the CLR with Wasm in the usual StandardWasm fixture style.
3. **Browser registry** (`scripts/webgpu-imports.mjs`): a handle table over `GPUDevice` objects
   that reads descriptor structs from linear memory (decision 0025).

### What lowers today and what waits

Decision 0083 lowers blittable scalars, including `native int`. A WebGPU handle is a pointer-sized
opaque value, so it crosses as `IntPtr`, and these entries **already lower**:

- `wgpu*AddRef` / `wgpu*Release`, `wgpuBufferDestroy`, `wgpuTextureDestroy`
- `wgpuDeviceGetQueue`, `wgpuBufferGetSize`, `wgpuTextureGetWidth/Height`
- `wgpuRenderPassEncoderSetPipeline`, `SetBindGroup` (with a null offsets pointer),
  `SetVertexBuffer`, `SetIndexBuffer`, `SetViewport`, `SetScissorRect`, `Draw`, `DrawIndexed`,
  `DrawIndirect`, `End`
- `wgpuComputePassEncoderSetPipeline`, `DispatchWorkgroups`, `End`

These **wait for #275** (pointers to structs, strings as `WGPUStringView`, `sizeof`):

- every `wgpuDeviceCreate*` (descriptor pointers), `wgpuCreateInstance`
- `wgpuQueueWriteBuffer` / `WriteTexture` (a data pointer into a pinned managed array)
- `wgpuQueueSubmit` (an array of command buffer handles)
- `wgpuCommandEncoderBeginRenderPass` (nested attachments)

These **need callbacks** (decision 0078 function pointers, or a host shim that resolves the future
before returning): `wgpuInstanceRequestAdapter`, `wgpuAdapterRequestDevice`, `wgpuBufferMapAsync`,
`wgpuInstanceWaitAny`. In a browser the host can obtain the adapter and device before `Main` runs
and hand their handles in. That is the first-milestone shim, and it keeps the C# side synchronous.

### Invariants

- Handles are owned by the managed wrapper. `Dispose` calls `Release` exactly once, and a
  disposed wrapper throws `ObjectDisposedException` rather than passing a dangling handle.
- Flag bits (`BufferUsage`, `TextureUsage`, `ShaderStage`, `ColorWriteMask`, `MapMode`) are the
  `webgpu.h` bits and pass through unchanged. Enum ordinals (texture and vertex formats, load
  ops and so on) were renumbered between header releases, so they are managed values that the
  native backend maps explicitly against its pinned header. They are never cast.
- No silent stubs: an entry that a backend does not implement surfaces as
  `EntryPointNotFoundException` (native) or `NotSupportedException` naming the entry (managed).

## First consumer

A procedural lawn and grass renderer written in C# drives the surface. It needs buffers,
storage buffers, a WGSL render pipeline with instancing, compute dispatch for blade culling and
LOD, bind groups, depth textures and a surface.

## Milestones

1. This decision, the managed API and the recording backend build as netstandard2.0. A CLR test
   records a frame.
2. `NativeBackend` over wgpu-native on the CLR clears and draws a triangle. The descriptor struct
   layouts are checked against the pinned header version.
3. After #275, the same DLL draws the same triangle in Edge through `native:webgpu`, with the
   pixels compared.
4. Compute and storage buffers, then the lawn renderer.

## Limitations

- The `webgpu.h` ABI changed in 2024–2025 (`WGPUStringView`, `WGPUFuture`, callback-info structs).
  The native backend pins one header release and records it here when milestone 2 lands.
- The surface creation descriptor is platform-specific (HWND, canvas selector). It is the only
  per-platform part of the API.
