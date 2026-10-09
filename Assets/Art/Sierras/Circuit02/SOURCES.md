# Circuit_02 — sierras inspired by Mina Clavero, Córdoba

Environment art only. The existing 1631 m road, shoulders, collision barriers,
checkpoints, AI waypoints, start grid, vehicles and water gameplay are preserved.
This is an artistic interpretation, not a geographic reconstruction.

## New textures (downloaded 2026-10-07)

All three sets are from Poly Haven, under CC0 1.0:
https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/

- **Rocky Terrain**, Amal Kumar: https://polyhaven.com/a/rocky_terrain
  - `Textures/rocky_terrain/rocky_terrain_diff_2k.jpg`, MD5 `4abb5d65394b6af07752099bd34ddd02`
  - `Textures/rocky_terrain/rocky_terrain_nor_gl_2k.jpg`, MD5 `05034535c6a4d24bf1886bd6331b9d39`
- **Aerial Grass Rock**, Rob Tuytel: https://polyhaven.com/a/aerial_grass_rock
  - `Textures/aerial_grass_rock/aerial_grass_rock_diff_2k.jpg`, MD5 `024018554c0002620127749d1f585f3d`
  - `Textures/aerial_grass_rock/aerial_grass_rock_nor_gl_2k.jpg`, MD5 `0c5423ce365169df7c242933b5137696`
- **Rock Face 03**, Dario Barresi (photography), Rico Cilliers (processing): https://polyhaven.com/a/rock_face_03
  - `Textures/rock_face_03/rock_face_03_diff_2k.jpg`, MD5 `657add9e8a8dffaeed19bdfc58078b59`
  - `Textures/rock_face_03/rock_face_03_nor_gl_2k.jpg`, MD5 `279b33e71ae6275cafa24e6597024c98`

Downloads were resolved from `https://api.polyhaven.com/files/{asset_id}`;
original MD5 hashes were checked after download. Original image bytes retained.
Unity imports use maximum 2048, compression, mipmaps, anisotropy 4 and OpenGL
normal maps imported as Normal Map. Tint, tiling and normal strength are set on
private TerrainLayers; no new textures were synthesized.

## Reused assets

- Poly Haven CC0 `namaqualand_rocks_01`, `grass_medium_01`,
  `wild_rooibos_bush` and `searsia_lucida` from `Assets/Art/Environment/`.
  Native upright prefabs are reused with private, greener material copies.
  These are visual substitutes for low serrano vegetation, not claimed native species.
- The four existing groups of 20 Quaternius spectators and their platforms
  stay in place beyond the containment barriers. Original licensing remains in
  `Assets/Art/Environment/QuaterniusPeople/`.
- The stream reuses the existing Unity URP WaterLake sample through a private
  copy of `Assets/Art/Forest/Circuit03/RiverBridge/Flowing forest river.mat`.
  This shader is supplied under the Unity sample license, **not CC0**; see
  `Assets/Art/Environment/UnityWaterSample/LICENSE.md`.
- The sky is a private copy of the built-in procedural sky material.

The terrain relief and stream surface are scene geometry, built locally by
`Tools/Circuit02Sierras.cs`. No new plant or rock models were generated/downloaded.
The source desert TerrainData is retained for recovery; the new scene uses
`SierraTerrain.asset` in this folder. Shared materials are not edited.
