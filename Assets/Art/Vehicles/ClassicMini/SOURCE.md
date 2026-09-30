# Mini Classic Rally

Original asset: **Low Poly Mini Cooper** by **Gilang Romadhan**.
Source: https://poly.pizza/m/2whNujq5rNd
Download: https://static.poly.pizza/e999a7af-291b-4c65-a46b-409493490e28.glb
License: **Creative Commons Attribution 3.0 Unported (CC-BY 3.0)**.
License terms: https://creativecommons.org/licenses/by/3.0/
Downloaded 2026-09-28. Attribution must be retained when distributing the game.
Source SHA-256: `08efa6b0428a0c89890ac673ad5d26986cd49839ae1a8b62d18fe775c9d7e751`.
No endorsement by the artist or vehicle manufacturer is implied.

Adaptations: removed display plinth; separated the four existing disconnected
tyre/hub assemblies; named FL/FR/BL/BR; centred wheel pivots; normalized length to
3.10 m; converted public GLTF to FBX; prepared URP solid-colour materials. No new
car geometry or textures were generated. This is a low-poly visual, not a
photorealistic racing livery. The source did not provide a ready-to-drive FBX.

Original download preserved in `Source~/mini.glb` (ignored by Unity's importer).
Reproducible adaptation: `Tools/prepare_classic_mini.py` (Blender).
Unity prefab: `Assets/Resources/Vehicles/ClassicMini.prefab`.
Rebuild using `Tools > Rally > Build Classic Mini Visual`.

Runtime selection reuses the existing player root, Rigidbody, four WheelColliders,
JrsVehicleController and RallyVehicleDynamics in each circuit. Only visual meshes
change; the body and wheel visuals are fitted to the Porsche's physical rig.
All physics remains identical, including wheelbase, radii, body collider and inertia.
The previous model-specific collider fitting was removed at the user's request.
The root retains its historical name for compatibility with existing references.
