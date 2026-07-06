# Water Foam Prototype

This prototype now includes a Roystan-style toon water demo for URP.
The main demo shader samples the camera depth texture, compares it with the water surface depth, and uses that difference to drive shallow/deep color and shoreline foam.

## Try It

In Unity, use one of these menu entries:

- `Tools/Water Foam Prototype/Build Demo Scene`
- `Tools/Water Foam Prototype/Create Self Contained Demo In Current Scene`
- `Tools/Water Foam Prototype/Prepare Current Scene For World Height Water`

`Build Demo Scene` creates `Assets/WaterFoamPrototype/Scenes/WaterFoam2DDemo.unity` with:

- a transparent water plane using `WaterFoamPrototype/RoystanToonWater`
- a sloped island and sea floor that produce depth-based water color
- half-submerged posts, rocks, and a bobbing test sphere that show contact foam
- a camera with URP depth texture enabled

## Core Logic

The Roystan-style shader logic is:

```hlsl
float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUv), _ZBufferParams);
float waterDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
float depthDifference = max(sceneDepth - waterDepth, 0.0);
```

That depth difference drives the water color. Small differences mean shallow water; larger differences mean deep water.

Foam is built from the same depth difference, then masked by scrolling noise and a distortion texture:

```hlsl
float shoreMask = 1.0 - saturate(depthDifference / foamDistance);
float shoreFoam = smoothstep(cutoff - softness, cutoff + softness, foamNoise);
```

The shader also samples URP's normals texture when available. The difference between the water normal and the scene normal adjusts foam width so vertical posts and sloped shores do not all receive identical foam bands.

## Main Parameters

- `Shallow Color` / `Deep Color`: depth-based water color ramp.
- `Depth Max Distance`: world depth range before the water reaches the deep color.
- `Foam Min Distance` / `Foam Max Distance`: foam band width range.
- `Foam Cutoff`: noise threshold for shoreline foam.
- `Foam Softness`: antialiasing width around the toon foam cutoff.
- `Foam Noise Scale` / `Foam Speed`: scrolling foam texture controls.
- `Distortion Strength` / `Distortion Scale` / `Distortion Speed`: UV distortion for less mechanical foam motion.
- `Open Water Foam Amount`: optional broken white flecks away from the shore.
- `Normal Foam Strength`: how strongly scene normals widen foam.

## Notes

The water is still a single transparent plane. Because this version is screen-depth based, it reacts to whatever opaque geometry the camera can see under or intersecting the water. It is close to Roystan's original article, adapted to URP's `DeclareDepthTexture.hlsl` and `DeclareNormalsTexture.hlsl`.

The earlier world-height approach is a useful alternative when the water effect must be independent from camera angle. This demo intentionally follows the article's screen-space technique instead.
