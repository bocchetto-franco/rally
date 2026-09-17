# Circuit_01 — ambiente árido

## Assets descargados

Todos los siguientes assets son de Poly Haven, licencia **CC0** (https://polyhaven.com/license). Las URLs de cada archivo, tamaños y hashes MD5 verificados están en `DOWNLOAD_MANIFEST.json`.

| Asset | Fuente | Uso |
| --- | --- | --- |
| Dry Ground 01 | https://polyhaven.com/a/dry_ground_01 | Tierra seca del Terrain y banquinas; PBR 2K |
| Gravelly Sand | https://polyhaven.com/a/gravelly_sand | Camino de grava y zonas arenosas del Terrain; PBR 2K |
| Rock Boulder Dry | https://polyhaven.com/a/rock_boulder_dry | Roca en pendientes del Terrain; PBR 2K |
| Wild Rooibos Bush | https://polyhaven.com/a/wild_rooibos_bush | Arbusto real, modelo FBX y PBR 1K |
| Grass Medium 01 | https://polyhaven.com/a/grass_medium_01 | Pastizal disperso, variante de textura `dry_diff`, FBX y PBR 1K |
| Namaqualand Rocks 01 | https://polyhaven.com/a/namaqualand_rocks_01 | Rocas escaneadas, FBX y PBR 1K |

Se consultó **Free Low Poly Desert Pack**, de 23 Space Robots and Counting, en https://assetstore.unity.com/packages/3d/environments/free-low-poly-desert-pack-106709. La descarga solicitó iniciar sesión en Unity ID. No se descargó ni se incorporó ese paquete. Se utilizaron los modelos CC0 anteriores como alternativa realista y gratuita. No se obtuvieron assets de sitios de redistribución no oficiales.

## Recursos oficiales de Unity

- Agua: `WaterSimple_FoamMask.shadergraph` y material base `Water.mat`, del sample **Production Ready Shaders**, paquete `com.unity.shadergraph` 17.6.0 instalado junto a URP 17.6.0. Se copió únicamente el conjunto de dependencias del shader/material conservando los GUID originales. Licencia original incluida en `UnityWaterSample/LICENSE.md`. Documentación: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Shader-Graph-Sample-Production-Ready-Water.html
- Cielo: shader integrado **Skybox/Procedural**, con parámetros de color cálidos. Sin HDRI externo.
- Superficies: shaders oficiales **Universal Render Pipeline/Lit** y **Universal Render Pipeline/Terrain/Lit**.

## Qué se creó/adaptó localmente

- Un Unity Terrain con relieve adaptado al camino y a las banquinas existentes. Se generó el mapa de alturas porque debe encajar exactamente con el trazado específico del proyecto; no se generaron nuevas texturas artísticas ni modelos de plantas/rocas.
- Empaquetado de mapas existentes: roughness → smoothness, AO y máscara Terrain; albedo + alpha para vegetación. Los archivos derivados están en `PackedTextures`; las imágenes descargadas permanecen intactas.
- Materiales URP, TerrainLayers, prefabs y distribución dispersa de los modelos descargados. La vegetación está fuera del camino y las banquinas.
- Material de charcos derivado del sample oficial, con color tenue, refracción reducida y ondas lentas. Se conserva el tamaño, la posición y los triggers/lógica de frenado de los cuatro charcos.
- Viento omitido: los FBX descargados no incluyen un shader URP de viento listo para usar. No se creó un shader de vegetación nuevo.

## Edición y revisión

- Escena: `Assets/Scenes/Circuit_01.unity`.
- Jerarquía del ambiente: `Circuit 01 - Arid Mountains`.
- Herramienta explícita de editor: `Tools > Rally > Environment > Build Arid Landscape`. No se ejecuta automáticamente al abrir escenas ni en el juego.
- La herramienta conserva una copia anterior de la escena en `Logs/SceneBackups` y comprueba posiciones del camino, parámetros de física y checkpoints/charcos antes de guardar.
- No ejecutar los antiguos generadores de trazado/props para retocar la estética: reconstruyen los objetos y podrían volver a aplicar materiales placeholder. Usar los materiales y el Terrain desde el Inspector.
