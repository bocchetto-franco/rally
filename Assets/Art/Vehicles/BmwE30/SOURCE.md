# BMW M3 E30 — Martin Trafas (TinoD2)

- Original: [FREE BMW M3 E30](https://sketchfab.com/3d-models/free-bmw-m3-e30-ac3c7013434e403e8faff87948caf422).
- Author: Martin Trafas, profile https://sketchfab.com/TinoD2.
- License: [Creative Commons Attribution 4.0 International](https://creativecommons.org/licenses/by/4.0/). Attribution is required; this is not CC0.
- Downloaded 2026-10-06 from the Google Drive link published by the author in the model description: https://drive.google.com/file/d/1Q90hgp1deq4El61YaqiDfI7eSyEpCtjE/view.
- Original archive: BMW E30_Final01.rar, SHA-256 `5D21FB238D02FB6C9A10F53E9B790F6A9924616071C3EAB90C3027505D1F0828`.
- Adaptations: removed studio/presentation nodes; straightened the posed front wheels; grouped original tyre/rim/disc meshes into four named wheels; uniformly scaled body length to 4.30 m; converted Blender materials to URP Lit; texture import capped at 2048 with compression. No vehicle geometry generated from scratch.
- Imported model: `Assets/Art/Vehicles/BmwE30/BmwE30.fbx`.
- Runtime visual prefab: `Assets/Resources/Vehicles/BmwE30.prefab`; pivots `wheel_FL`, `wheel_FR`, `wheel_BL`, `wheel_BR`.
- Rebuild: `Tools/prepare_bmw_replacement.py` using Blender with auto-execution disabled, then `BmwReplacementSetup.Build` from `Tools/BmwReplacementSetup.cs` via Unity Pipeline.

Include in distributed credits: “BMW M3 E30 by Martin Trafas (TinoD2), CC BY 4.0, via Sketchfab. Modified for Unity (scale, wheel preparation and materials).” Keep the source and license links with the attribution. The license covers the uploaded artwork, not an endorsement by the vehicle manufacturer.
