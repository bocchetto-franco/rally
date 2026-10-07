# Audi Quattro S1 — TonyWony

- Original: https://sketchfab.com/3d-models/audi-quattro-s1-402697365eaa41a68a543b71d0e0bfa8.
- Author: TonyWony, https://sketchfab.com/TonyWony.
- License: Creative Commons Attribution 4.0 International (CC-BY 4.0), https://creativecommons.org/licenses/by/4.0/. Credit is required; this is not CC0.
- Downloaded through the official Sketchfab download button on 2026-10-06.
- Original archive: `audi-quattro-s1.zip`, 53,453,809 bytes; SHA-256 `8097427EA4A301B93B52F9B73A4A70292994F55072473A628970D7668968A5D4`.
- The original is Collada/DAE with PBR textures and one combined mesh, not a pre-rigged FBX. `Tools/prepare_audi_replacement.py` reads its geometry/UVs, separates disconnected existing tyre/rim/bolt/disc pieces into four wheel assemblies, preserves the authored geometry, and exports an FBX. Each wheel has eight original pieces; no new wheels or car geometry were generated.
- Uniform length: 4.25 m including the original body kit. Wheel pivots `wheel_FL`, `wheel_FR`, `wheel_BL`, `wheel_BR`; tyre radii approximately 0.3025 m. Orientation is +Z forward / +Y up in the prepared Unity prefab.
- PBR material: original albedo, normal, AO and a metallic/smoothness map packed from the author's metallic and roughness textures. URP Lit; import resolution capped at 2048, compression and mipmaps enabled. `Tools/AudiReplacementSetup.cs` builds the materials and prefab.
- Model: `Assets/Art/Vehicles/AudiQuattro/AudiQuattro.fbx`. Prefab: `Assets/Resources/Vehicles/AudiQuattro.prefab`.

Distributed attribution: “Audi Quattro S1 by TonyWony, CC BY 4.0, via Sketchfab. Modified for Unity (format conversion, wheel separation, scale and materials).” Retain the source and license links. Manufacturer/livery trademarks are part of the depicted vehicle; this artwork license does not imply endorsement by those brands.
