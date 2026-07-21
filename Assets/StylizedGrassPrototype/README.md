# Stylized Mask Grass Prototype

Open `Assets/StylizedGrassPrototype/Scenes/StylizedMaskGrassDemo.unity`.

The `Stylized Grass Field` component is the main control surface:

- `Distribution Source` selects how the component obtains its geometry. `Existing Mesh` preserves the current `MeshFilter` mesh and never replaces it when field dimensions change. `Generated Plane` creates the rectangular grid controlled by `Field Size` and `Subdivisions`. Switching back to `Existing Mesh`, disabling the component, or removing it restores the original mesh.
- `Area Sampling` controls average blade density in blades per square meter. Each source triangle receives a stochastic blade count from its world-space area, capped by `Max Blades Per Triangle`; subdivide unusually large triangles when the cap limits density. `Slope Filter` is off by default so blades follow the entire mesh; enable it to omit blades from steep or downward-facing surfaces without hiding the underlying mesh.
- `Distribution Plane` controls the generated field size, supporting subdivision density, and deterministic random seed. Subdivisions no longer define grass density directly, but the source triangles must remain small enough that the per-triangle blade cap is not reached.
- `Feature Toggles` independently disable wind, drifting color noise, dynamic main-light shadows, and grass interaction. Disabled features take early shader exits instead of continuing to evaluate their procedural calculations.
- `Blade Mask And Shape` uses `GrassBladeMask.png`; white pixels remain and black pixels are clipped. Vertical-axis rotation is intentionally dominant, while `Other Axis Coefficient` limits X/Z tilt.
- `Shared Noise And Gradient` keeps separate ground and grass-tip palettes while reusing the same world-space procedural noise.
- `Gradient Ramp Mode` converts that noise into a color mask. `Linear` gives an even cartoon-style transition, `Smooth` preserves the previous eased look, and `Stepped` quantizes the mask into `Gradient Ramp Steps` color bands.
- `Global Style` adjusts hue, saturation, brightness, and the unlit blade lift for the entire field.
- `Animated Perlin Wind` evaluates non-tiling procedural noise twice: a `0.03` large-wave field plus a `0.15` detail field at `0.25` strength. `Wind Noise Axis Scale (X/Z)` multiplies world coordinates before evaluation; unequal values stretch the field into wave bands. The advected result drives moving color bands, blade height, segmented bending, and additional rotation from the same value.
- `Dynamic Object Shadow Mask` remaps URP main-light shadow attenuation into a soft black/white mask.

The grass shader intentionally has no `ShadowCaster` pass, and the field renderer forces shadow casting off. The ground and blades can receive shadows from the orange demo objects without grass self-shadowing or casting onto other objects.

Each grass card is emitted as a four-segment strip so low wind values can visibly press it down while high values lift it with a gentler bend. The implementation uses a geometry shader (`Shader Model 4.5`) and targets desktop-class URP renderers.

The previous field/wind PNG assets remain in the project only for backward compatibility with existing serialized scenes. The shader and rebuild tool no longer sample or regenerate them.

The base `Noise Contrast` varies slowly by about 22% over time. The same procedural color field drifts with the shared wind direction but uses its own `Cloud Shadow Speed`; `Wind Speed` independently controls grass geometry motion.

## Procedural Leaf Trees

The same demo now includes three `ProceduralLeafTree` examples. Each component builds a low-poly ellipsoid as an implicit source surface, samples its triangles by area, and expands every sampled point into three intersecting leaf cards. Scale, yaw, pitch, roll, point count, source resolution, crown dimensions, and seed are editable and deterministic.

`Leaf Density` is a per-tree multiplier over the saved `Leaf Point Count`; `1` preserves the authored density. The material's final HSV controls provide a shared global adjustment, while each tree's `Hue Shift`, `Saturation`, and `Value` fields add an independent override. Both layers are applied after the lighting, noise, and crown-bottom color layers and before emission.

Leaf motion reads the active `StylizedGrassField` wind settings without modifying them. Disabling wind on that grass field also bypasses leaf wind in both the forward and shadow passes. Each leaf cluster stores its local pivot in the second UV channel; the passes sample the same large/detail procedural noise as the grass from the pivot's world-space X/Z position, then apply coherent pivot rotation plus directional and vertical vertex displacement. `Wind Frequency Multiplier` changes only that tree's animation rate, while `Wind Amplitude Multiplier` is a master strength control over the existing `Wind Displacement Multiplier` and `Wind Rotation Multiplier` settings.

`LeafClusterMask.png` keeps the black pixels from the supplied texture and clips the white background. `StylizedProceduralLeavesURP.shader` uses a fixed object-space `(0, 0, 1)` normal, a `1.0` dark diffuse branch and `1.7` bright branch, two constant color ramps, object-space value noise with a linear ramp, a bottom-to-top linear darkening ramp, and an emission-style final output. The material still receives the URP main-light shadow mask and casts cutout leaf shadows.

Maintain the demo scene directly in Unity. Use the component rebuild controls when procedural grass or tree geometry needs to be regenerated.

## Procedural Cloud Clusters

`ProceduralCloudCluster` grows a hierarchy of overlapping spheres from one core sphere. Each growth iteration places smaller child spheres on the previous generation's surfaces, then samples every sphere by surface area and expands each sample into three mutually perpendicular cloud-mask cards on the local XY, XZ, and YZ planes. The core and every iterated child sphere also render a centered three-card cluster whose maximum size follows that sphere's diameter; `Growth Sphere Card Scale` controls the shared multiplier without changing the existing surface-cloudlet random sequence. `Growth Iterations`, `Child Spheres Per Parent`, `Child Radius Ratio`, axis scaling, vertical bias, density, card size, random scale, and seed are all independently adjustable.

The cloud shader uses `CloudClusterMask.png` as an inverse black mask multiplied by texture alpha, so transparent and white pixels are clipped while black pixels remain. Inside that silhouette, the mask is multiplied by the project's `T_BlueNoise128.png`. Its coordinates are generated procedurally with world-space triplanar projection, so large and small cards share a consistent spatial grain size; `Blue Noise Scale` makes that grain finer as it increases, and `Blue Noise Blend` controls how strongly it breaks up the original mask. Each cloudlet keeps one spherical surface normal across all three cards for a consistent shadow/base/highlight band, plus a shared random brightness variation. To reduce confusing edge-on overlaps, each card also stores its real geometric normal and smoothly clips away between `Card Edge Fade Start` and `Card Edge Fade End` as it turns side-on to the camera.


## Immediate Interaction

Add `Stylized Grass Interactor` to a character or moving prop. Up to 16 active interactors affect every stylized grass field without creating colliders for individual blades. The component exposes a local foot-position offset, world-space radius, bend strength, flattening, falloff, movement-direction influence, and velocity influence.

The non-texture trail mode records independent influence points at a fixed `Trail Sample Interval`; points are never connected into path segments. Sampling positions are interpolated between frame positions so fast movement can still produce multiple points in one frame, while stationary objects do not create duplicates. Every point affects only grass inside its own radius and uses smootherstep age recovery until its weight reaches zero. `Recovery Time` controls point lifetime and `Max Trail Points` limits the cost. Current interactors are uploaded first, followed by the newest samples, with a global limit of 64 influence points. Set `Recovery Time` to `0` to restore immediate recovery.
