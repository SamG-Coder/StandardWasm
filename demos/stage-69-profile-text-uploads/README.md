# Stage 69 · Profile WebGPU text uploads

Run `npm run demo -- stage-69-profile-text-uploads` on Windows with Edge
and WebGPU. The runner builds the self-hosted compiler and compiles the
shared Profile DLL inside Edge before verifying Windows and browser
interaction.

The first frame uploads the full text texture. Edits and Clear change
only the preview label, which is clipped to its control bounds. The GPU
retains the pixels outside that rectangle between uploads. The form
still redraws the canvas each frame; the upload area is the quantity
being reduced.

The benchmark compares full and partial WebGPU uploads with Canvas 2D
on the same device. The verifier requires exact full-frame parity between
the two WebGPU paths, including cleared text, and checks that ordinary
editing leaves pixels outside the preview intact. The output includes
pixel counts, CPU submission times and WebGPU queue drain timings at
`artifacts/profile-text-uploads/results.json`.
