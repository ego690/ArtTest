# Roystan Grass Shader Demo

Open `Assets/RoystanGrassDemo/Scenes/RoystanGrassDemo.unity`.

This demo ports the core ideas from Roystan's grass shader article into this project's URP setup:

- A dense rolling mesh provides grass spawn triangles.
- `RoystanGeometryGrassURP.shader` uses a geometry shader to generate one multi-segment blade per input triangle.
- Each blade gets stable random width, height, facing direction, forward bend, and wind sway from its world position.
- The material exposes the main tuning controls: blade size, curve, wind strength/frequency/scale, top/bottom color, shadow color, fog, and translucency.

The original article also uses tessellation. This demo keeps density in the mesh instead so it is easier to validate in URP first.
