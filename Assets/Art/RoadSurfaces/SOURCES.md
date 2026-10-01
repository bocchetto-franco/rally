# Road surfaces — Poly Haven CC0

Downloaded 2026-10-01, original 2048 x 2048 maps, by Amal Kumar:

- Rocky Trail 02: https://polyhaven.com/a/rocky_trail_02 — dry gravel/compacted dirt for Circuit_01 and Circuit_02.
- Muddy Tracks: https://polyhaven.com/a/muddy_tracks — dark, damp tracked mud for Circuit_03.
- License: CC0, https://polyhaven.com/license (https://creativecommons.org/publicdomain/zero/1.0/).

Each folder includes original diffuse, OpenGL normal and ARM JPG maps. URLs and SHA-256 checksums are in DOWNLOAD_MANIFEST.json. `Tools/prepare_road_surfaces.py` verifies the provider's MD5 before packing the maps. No generated artwork or external shader is used.

`urp_mask.png` repacks ARM to URP Lit: R = metallic (source B), G = occlusion (source R), B = 0, A = 1 - roughness (source G). This one texture is shared by the metallic/smoothness and occlusion slots. No displacement/tessellation, extra mesh or runtime component.

Materials updated in place (existing scene references preserved):

- `Assets/Art/Environment/Materials/Road - dry gravel.mat`: Rocky Trail 02, 2 m repeat, normal strength 0.8, smoothness multiplier 0.55, occlusion 0.65.
- `Assets/Art/Forest/Materials/Wet forest road.mat`: Muddy Tracks, 2.25 m repeat, normal strength 0.8, smoothness multiplier 0.70, occlusion 0.65.

Road UVs are world XZ / 6; material scales are 3 and 2.6666667 respectively. Max Size 2048, compression, mipmaps, trilinear filtering and anisotropy 4. Normals use NormalMap import mode and linear sampling; masks are linear, albedo sRGB. Existing global Half Res setting is unchanged. No terrain, scene geometry, collider or handling changes. Previous textures remain available for terrain/shoulders.
