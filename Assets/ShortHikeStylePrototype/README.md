# Short Hike Style Prototype

This prototype is a standalone test of an A Short Hike-like rendering recipe:

- Final camera color downsampled to a lower-resolution texture, then point-upscaled back to the screen.
- Adjustable low-resolution render scale on `ShortHikePixelCamera`.
- Screen-space color/luma edge detection applied to the low-resolution texture.
- No camera MSAA or post anti-aliasing.
- Low poly island silhouettes with vertex color variation.
- Two-to-three band toon lighting.
- Flat, graphic water with procedural wave and foam lines.

Use `Tools/Short Hike Style Prototype/Build Demo Scene` to rebuild:

`Assets/ShortHikeStylePrototype/Scenes/ShortHikeStyleDemo.unity`

The main runtime pieces are:

- `ShortHikePixelCamera`: stores the per-camera style settings. It does not change `Camera.targetTexture`.
- `ShortHikeLowResolutionRendererFeature`: runs in URP after post-processing, downsamples the active camera color to a low-resolution RenderTexture, applies the edge composite at that lower resolution, then point-upscales back to the camera color.
- `ShortHikeOrbitCamera`: lets the demo camera orbit with right mouse drag, `Q/E`, and mouse wheel.
- `ShortHikeDemoMotion`: adds small character bobbing and water animation.

This folder is independent from the other prototype folders in the project.
