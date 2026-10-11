# Rally — créditos y atribuciones

Auditoría: 2026-10-10. Este archivo es la fuente de la pantalla Créditos y de la copia distribuida con el juego. Incluye recursos activos y recursos históricos todavía presentes en Assets/Resources; estar descargado, ser gratuito o aparecer en un ejemplo no demuestra permiso de uso.

## Estado de licencias

CC0 1.0 Universal: atribución no obligatoria; listado por transparencia. CC-BY: REQUIERE ATRIBUCIÓN, conservar nombre, autor, enlace a obra/licencia y cambios. MIT, Apache y OFL: conservar también sus avisos/licencias completos. Los samples de Unity NO son CC0; tienen condiciones para proyectos dependientes de Unity.

PENDIENTE CRÍTICO: no se encontró autor, URL original ni licencia del Porsche importado por el usuario. No darlo por autorizado para distribución/comercialización. La fuente Electronic Highway Sign de los ejemplos TMP tampoco tiene términos verificables locales. No se identificaron licencias NC/ND expresas en los demás recursos auditados; esto no acredita derechos de marcas, logotipos, liveries o imagen de terceros.

La revisión contrasta SOURCE/SOURCES, manifiestos y licencias locales con Poly Haven API, Sketchfab API, páginas de Freesound/OpenGameArt, Kenney y licencias de los paquetes instalados. Las páginas web de BMW/Audi devolvieron 403; su API pública confirmó CC-BY 4.0. No se adivinó la procedencia del Porsche por su nombre. Inventario por archivo: Docs/AssetLicenseInventory.csv; evidencia de APIs: Docs/CreditsEvidence.json; 63 paquetes y licencias/ausencias locales: Docs/PackageLicenseInventory.csv. Avisos distribuidos: StreamingAssets/Licenses/.

## Autos y vehículo de referencia

### Porsche 911 SC Rally — licencia no clara
- Autor: DESCONOCIDO; aportado/importado por el usuario.
- Fuente: DESCONOCIDA; no hay URL original verificable en el proyecto.
- Licencia: NO CLARA — pendiente de comprobación, no asumir permiso comercial/distribución.
- Rutas: `Assets/auto/porsche-911-sc-rally`.
- Estado: modelo principal activo; FBX 911sc.fbx y nueve texturas originales. La licencia debe cubrir modelo, mapas y livery; pedir el enlace/archivo de licencia al proveedor original. El binder y las conversiones locales no otorgan derechos sobre el modelo.

### [FREE] BMW M3 E30
- Autor: Martin Trafas (TinoD2).
- Fuente: https://sketchfab.com/3d-models/free-bmw-m3-e30-ac3c7013434e403e8faff87948caf422
- Licencia: CC-BY 4.0 International — REQUIERE ATRIBUCIÓN. https://creativecommons.org/licenses/by/4.0/
- Rutas: `Assets/Art/Vehicles/BmwE30`; `Assets/Resources/Vehicles/BmwE30.prefab`.
- Cambios: estudio eliminado, ruedas delanteras enderezadas y cuatro conjuntos de rueda preparados, conversión FBX/URP, escala uniforme 4.30 m y compresión de texturas. Fuente/hash preservados en SOURCE.md. Sin respaldo ni patrocinio del autor o fabricante.

### Audi Quattro S1
- Autor: TonyWony.
- Fuente: https://sketchfab.com/3d-models/audi-quattro-s1-402697365eaa41a68a543b71d0e0bfa8
- Licencia: CC-BY 4.0 International — REQUIERE ATRIBUCIÓN. https://creativecommons.org/licenses/by/4.0/
- Rutas: `Assets/Art/Vehicles/AudiQuattro`; `Assets/Resources/Vehicles/AudiQuattro.prefab`.
- Cambios: Collada/DAE a FBX, separación de ruedas originales, pivotes nombrados, escala uniforme 4.25 m, materiales URP y empaquetado PBR. Fuente/hash en SOURCE.md. Sin respaldo ni patrocinio del autor o fabricante.

### FREE Lancia Delta HF Integrale evo 2 — histórico
- Autor: TARANTULA (begemot.988888).
- Fuente: https://sketchfab.com/3d-models/free-lancia-delta-hf-integrale-evo-2-85614131e0dc4613a948472aaa935fc7
- Licencia: CC-BY 4.0 International — REQUIERE ATRIBUCIÓN. https://creativecommons.org/licenses/by/4.0/
- Rutas: `Assets/Art/Vehicles/LanciaDelta`; `Assets/Resources/Vehicles/LanciaDelta.prefab`.
- Estado/cambios: no seleccionable, conservado y potencialmente incluido por Resources. Ruedas agrupadas/separadas, pivotes, escala, FBX y materiales URP adaptados. Fuente/hash en SOURCE.md.

### Low Poly Mini Cooper — histórico
- Autor: Gilang Romadhan.
- Fuente: https://poly.pizza/m/2whNujq5rNd
- Licencia: CC-BY 3.0 Unported — REQUIERE ATRIBUCIÓN. https://creativecommons.org/licenses/by/3.0/
- Rutas: `Assets/Art/Vehicles/ClassicMini`; `Assets/Resources/Vehicles/ClassicMini.prefab`.
- Estado/cambios: no seleccionable, conservado y potencialmente incluido por Resources. Base de exhibición retirada, ruedas originales separadas, pivotes, escala 3.10 m, GLTF a FBX y materiales URP. Fuente/hash en SOURCE.md.

### JRS / JS Vehicle Physics Controller y recursos AMR
- Autor: Samborlang Pyrtuh / Jermesa Studio.
- Fuente: https://github.com/Jermesa-Studio/JRS_Vehicle_Physics_Controller
- Licencia: MIT declarada por el repositorio; conservar copyright/permiso/descargo completos. https://github.com/Jermesa-Studio/JRS_Vehicle_Physics_Controller/blob/main/LICENSE
- Rutas: `Assets/JS Vehicle Physics Controller`.
- Alcance: scripts (cabeceras copyright 2023; LICENSE upstream 2024), auto/rueda de policía, modelos de plataformas/puentes/piedras/rampas, polvo dust_01.png, mapas de vehículo/plataformas, paleta, UI de controles, Police_siren5.wav y Evening Sky1 .png. Los 44 archivos fuente de arte/audio locales coinciden byte a byte con los blobs del repo del autor. No se encontró una exclusión/licencia separada en ese repo; no confundir MIT con CC0 ni atribuirlos a Poly Haven. Policía conservado como referencia; polvo aún reutilizado. Adaptaciones Unity/URP y controlador realizadas por el proyecto.

## Terreno y caminos — Poly Haven

Todos los sets siguientes usan CC0 1.0 Universal: https://creativecommons.org/publicdomain/zero/1.0/ — atribución no obligatoria. Política del proveedor: https://polyhaven.com/license . Mapas originales, máscaras reempaquetadas, TerrainLayers, LOD y copias privadas conservan la procedencia; no son assets adicionales de otro autor.

### Dry Ground 01
- Autor: Rob Tuytel.
- Fuente: https://polyhaven.com/a/dry_ground_01
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/dry_ground_01`.

### Gravelly Sand
- Autor: Dario Barresi.
- Fuente: https://polyhaven.com/a/gravelly_sand
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/gravelly_sand`.

### Rock Boulder Dry
- Autor: Dimitrios Savva (fotografía), Rico Cilliers (procesado).
- Fuente: https://polyhaven.com/a/rock_boulder_dry
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/rock_boulder_dry`.

### Rocky Terrain
- Autor: Amal Kumar.
- Fuente: https://polyhaven.com/a/rocky_terrain
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Sierras/Circuit02/Textures/rocky_terrain`.

### Aerial Grass Rock
- Autor: Rob Tuytel.
- Fuente: https://polyhaven.com/a/aerial_grass_rock
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Sierras/Circuit02/Textures/aerial_grass_rock`.

### Rock Face 03
- Autor: Dario Barresi (fotografía), Rico Cilliers (procesado).
- Fuente: https://polyhaven.com/a/rock_face_03
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Sierras/Circuit02/Textures/rock_face_03`.

### Forest Floor
- Autor: eye-candy.xyz.
- Fuente: https://polyhaven.com/a/forest_floor
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Forest/PolyHaven/forest_floor`.

### Mud Forest
- Autor: eye-candy.xyz.
- Fuente: https://polyhaven.com/a/mud_forest
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Forest/PolyHaven/mud_forest`.

### Mossy Rock
- Autor: Rob Tuytel.
- Fuente: https://polyhaven.com/a/mossy_rock
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Forest/PolyHaven/mossy_rock`.

### Rocky Trail 02
- Autor: Amal Kumar.
- Fuente: https://polyhaven.com/a/rocky_trail_02
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/RoadSurfaces/rocky_trail_02`.
- Uso/cambios: ripio de Circuit_01/02; máscaras ARM convertidas a URP, normal GL, tiling y tintes ajustados. Reutilizado también en bordes/charcos.

### Muddy Tracks
- Autor: Amal Kumar.
- Fuente: https://polyhaven.com/a/muddy_tracks
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/RoadSurfaces/muddy_tracks`.
- Uso/cambios: camino húmedo Circuit_03; máscaras ARM a URP, normal GL, tiling/tinte. Reutilizado en barro de charcos.

## Vegetación, rocas y decoración

### Wild Rooibos Bush
- Autor: James Ray Cock (modelado), Jenelle van Heerden (fotografía).
- Fuente: https://polyhaven.com/a/wild_rooibos_bush
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/wild_rooibos_bush`.

### Grass Medium 01
- Autor: Rob Tuytel (fotografía), Rico Cilliers (modelado).
- Fuente: https://polyhaven.com/a/grass_medium_01
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/grass_medium_01`.

### Namaqualand Rocks 01
- Autor: Greg Zaal (fotografía), Jenelle van Heerden (modelado).
- Fuente: https://polyhaven.com/a/namaqualand_rocks_01
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/namaqualand_rocks_01`.

### Quiver Tree 01
- Autor: James Ray Cock y Dario Barresi (fotografía), Rico Cilliers (modelado).
- Fuente: https://polyhaven.com/a/quiver_tree_01
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/quiver_tree_01`.

### Searsia Lucida
- Autor: James Ray Cock (modelado), Jenelle van Heerden (fotografía).
- Fuente: https://polyhaven.com/a/searsia_lucida
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/PolyHaven/searsia_lucida`.

### Nature Kit 2.1
- Autor: Kenney.
- Fuente: https://kenney.nl/assets/nature-kit
- Licencia: CC0 1.0 Universal — atribución no obligatoria; License.txt original conservado.
- Rutas: `Assets/Art/Forest/KenneyNature`; `Assets/Art/Forest/Meshes`.
- Selección: tree_pineRoundC, tree_pineTallA_detailed, tree_pineTallB_detailed, plant_bushDetailed, plant_flatShort y rock_largeA. Pinos/arbustos/plantas/rocas del bosque; conversiones de materiales, prefabs URP y LOD. No afirmar que Fern sea un helecho botánicamente exacto.

### Racing Kit 2.0
- Autor: Kenney.
- Fuente: https://kenney.nl/assets/racing-kit
- Licencia: CC0 1.0 Universal — atribución no obligatoria; License.txt original conservado.
- Rutas: `Assets/Art/Environment/Circuit01Dressing/KenneyRacing`.
- Selección: billboardLow/billboardLower, flagCheckers/Red/Green/Tankco, tent/tentLong, bannerTowerRed y texturas TankCo/cuadros. Prefabs, escala, LOD y materiales URP adaptados; TankCo es ficticio.

## Público y espectadores

### LowPoly Posed Humans / Posed Background Characters
- Autor: Quaternius.
- Fuente: https://opengameart.org/content/lowpoly-posed-humans
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Environment/QuaterniusPeople`.
- Cambios: cuatro poses de pie, materiales URP/ropa, distribución y mallas combinadas/LOD. License.txt tiene encabezado erróneo Knight Pack, pero menciona Background characters; la página original de este pack confirma autor y CC0.

## Cielo y agua

### Kloofendal Overcast
- Autor: Greg Zaal.
- Fuente: https://polyhaven.com/a/kloofendal_overcast
- Licencia: CC0 1.0 Universal — atribución no obligatoria.
- Rutas: `Assets/Art/Forest/PolyHaven/kloofendal_overcast`.
- Uso: HDRI de cielo fresco del bosque, importación/tinte/exposición adaptados.

### Agua — Production Ready Shaders de Shader Graph 17.6.0
- Autor: Unity Technologies ApS (copyright 2020 en LICENSE.md).
- Fuente: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Shader-Graph-Sample-Production-Ready-Water.html
- Licencia: Unity Companion License para proyectos dependientes de Unity — NO CC0. https://unity.com/legal/licenses/unity-companion-license
- Rutas: `Assets/Art/Environment/UnityWaterSample`.
- Alcance/cambios: WaterLake.shadergraph, WaterSimple_FoamMask, subgraphs/dependencias, texturas/normal puddle_norm y material original. Smoothness/ondas/refracción/color adaptados. Usado por charcos, río y arroyo; conservar aviso original y cumplir la licencia de Unity. No es una licencia libre para redistribuirlo en cualquier motor.

### Skybox procedural y recursos de plantilla Unity
- Autor: Unity Technologies.
- Fuente: https://docs.unity3d.com/6000.0/Documentation/Manual/shader-skybox-procedural.html
- Licencia: recursos integrados de Unity bajo términos del Editor; recursos de URP/Shader Graph bajo Unity Companion License. NO CC0. https://unity.com/legal/terms-of-service/software
- Rutas: `Assets/Settings`; `Assets/TutorialInfo`; `Assets/Readme.asset`.
- Alcance: shader Skybox/Procedural, materiales privados de cielo cálido/serrano, icono e información de plantilla. El PNG Evening Sky1 del pack JRS está incluido en la entrada JRS, no aquí.

## Sonidos y música

### Sedan engine loop — motor
- Autor: Dmitry_mansurev64.
- Fuente: https://freesound.org/people/Dmitry_mansurev64/sounds/748027/
- Licencia: CC0 1.0 Universal — atribución no obligatoria, confirmada en la página original.
- Rutas: `Assets/Resources/Audio/EngineLoop.wav`.
- Cambios: HQ preview público MP3 a WAV mono 44.1 kHz, recorte/nivel y fundido circular. Manifiesto de descarga conservado.

### screeching tyres / tires — derrape
- Autor: johnnydekk.
- Fuente: https://freesound.org/people/johnnydekk/sounds/614627/
- Licencia: CC0 1.0 Universal — atribución no obligatoria, confirmada en la página original.
- Rutas: `Assets/Resources/Audio/TyreSkid.wav`.
- Cambios: preview HQ convertido, fragmento de dos segundos, nivel/fundido. Es foley de goma, no grabación literal de neumáticos de auto.

### Collision — impacto
- Autor: qubodup.
- Fuente: https://freesound.org/people/qubodup/sounds/332058/
- Licencia: CC0 1.0 Universal — atribución no obligatoria; indicada explícitamente en la ficha del sonido.
- Rutas: `Assets/Resources/Audio/Impact.wav`.
- Cambios: conversión a WAV mono, recorte/nivel. El autor declara derivación de una grabación de dron publicada como CC0 al descargarla; no confundir el aviso genérico de CC-BY de su perfil con esta obra específica.

### Racing Game Title — menú
- Autor: MintoDog.
- Fuente: https://opengameart.org/content/racing-game-title
- Licencia: CC0 1.0 Universal — atribución no obligatoria, confirmada en la página original.
- Rutas: `Assets/Resources/Audio/MenuMusic.ogg`.
- Cambios: renombrado; música Ogg original sin modificación artística.

### Hot Roadway — carrera
- Autor: MintoDog.
- Fuente: https://opengameart.org/content/hot-roadway
- Licencia: CC0 1.0 Universal — atribución no obligatoria, confirmada en la página original.
- Rutas: `Assets/Resources/Audio/RaceMusic.ogg`.
- Cambios: versión hot_roadway_bpm160, renombrada; Ogg original sin modificación artística.

## Tipografías e interfaz

### Oswald Bold
- Autor: Vernon Adams, copyright 2011–2012; Reserved Font Name Oswald.
- Fuente: https://github.com/googlefonts/OswaldFont
- Licencia: SIL Open Font License 1.1 — conservar aviso y licencia; no vender la fuente por sí sola. https://openfontlicense.org/open-font-license-official-text/
- Rutas: `Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold.ttf`; `Assets/TextMesh Pro/Examples & Extras/Fonts/Oswald-Bold - OFL.txt`; `Assets/Resources/MenuFonts/Oswald.asset`.
- Uso/cambios: titulares; atlas SDF preparado para español, fuente original sin editar.

### Roboto Bold
- Autor: Google / Christian Robertson; fuente distribuida con los ejemplos TMP.
- Fuente: https://github.com/googlefonts/roboto
- Licencia: Apache License 2.0 según la copia importada — conservar licencia/avisos. https://www.apache.org/licenses/LICENSE-2.0
- Rutas: `Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf`; `Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold - License.txt`; `Assets/Resources/MenuFonts/Roboto.asset`.
- Uso/cambios: texto del menú, atlas SDF español. No cambiar la licencia de esta copia por la de una revisión moderna de Roboto.

### Liberation Sans
- Autor: Google (2010), Red Hat (2012); Reserved Font Names Arimo/Tinos/Cousine/Liberation según aviso local.
- Fuente: https://github.com/liberationfonts/liberation-fonts
- Licencia: SIL Open Font License 1.1 — conservar copyright/licencia.
- Rutas: `Assets/TextMesh Pro/Fonts/LiberationSans.ttf`; `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`; `Assets/TextMesh Pro/Resources/Fonts & Materials`.
- Uso: fuente TMP por defecto/HUD y variantes de material; atlas derivados.

### Anton y Bangers — ejemplos TMP
- Autor: Vernon Adams, copyright 2011 (Anton) y 2010 (Bangers).
- Fuente: https://github.com/google/fonts/tree/main/ofl/anton ; https://github.com/google/fonts/tree/main/ofl/bangers
- Licencia: SIL Open Font License 1.1, avisos originales locales conservados.
- Rutas: `Assets/TextMesh Pro/Examples & Extras/Fonts/Anton.ttf`; `Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf`; `Assets/TextMesh Pro/Examples & Extras/Fonts/Anton OFL.txt`; `Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers - OFL.txt`.
- Estado: muestras del paquete, no fuentes principales del menú.

### Unity font — ejemplos TMP
- Autor: Unity, copyright 2020–2021; Reserved Font Name Unity.
- Fuente: https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/index.html
- Licencia: SIL Open Font License 1.1 según Unity - OFL.txt local.
- Rutas: `Assets/TextMesh Pro/Examples & Extras/Fonts/Unity.ttf`; `Assets/TextMesh Pro/Examples & Extras/Fonts/Unity - OFL.txt`.

### Electronic Highway Sign — términos no claros
- Autor: atribuido a Ash Pikachu Font por catálogos; no se encontró confirmación primaria en el proyecto.
- Fuente: importado con Examples & Extras de TMP; URL original del autor no verificada.
- Licencia: NO CLARA — no hay licencia local; etiquetas de catálogos Freeware/Personal Use contradictorias NO acreditan uso comercial/distribución.
- Rutas: `Assets/TextMesh Pro/Examples & Extras/Fonts/Electronic Highway Sign.TTF`.
- Estado: ejemplo, no seleccionado por el menú/HUD. Conservar marcado; verificar o excluir de una distribución, sin borrar automáticamente en esta tarea.

### TextMesh Pro — recursos esenciales y Examples & Extras
- Autor: Unity Technologies / TextMesh Pro (Stephan Bouchard); terceros de fuentes listados por separado.
- Fuente: https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/license/LICENSE.html
- Licencia: Unity Companion License para los recursos del paquete; NO extenderla a fuentes de terceros sin sus términos. https://unity.com/legal/licenses/unity-companion-license
- Rutas: `Assets/TextMesh Pro`.
- Alcance: shaders TMP, materiales/estilos/sprites y texturas demostrativas (Default Sprites, DropCap, Wipe Pattern, Floor/Crate/Brushed Metal, gradients y screenshots). No se encontraron avisos separados de autor de esas imágenes; documentadas como ejemplos oficiales, no como CC0 ni descarga artística independiente. Las fuentes anteriores prevalecen sobre esta entrada genérica.

## Paquetes y software de terceros

### Unity URP, Shader Graph, Input System, ProBuilder, uGUI y Timeline
- Autor: Unity Technologies ApS / Unity Technologies.
- Fuente: https://docs.unity3d.com/Manual/PackagesList.html
- Licencia: Unity Companion License según LICENSE.md instalado; NO CC0. Sus propios Third Party Notices tienen términos adicionales que deben conservarse.
- Rutas: `Packages/manifest.json`; `Packages/packages-lock.json`.
- Versiones: URP/Core/Shader Graph 17.6.0, Input System 1.20.0, ProBuilder 6.1.2, uGUI 2.6.0, Timeline 6.6.0, AI Navigation 2.0.14. Avisos completos de todos los paquetes instalados se copian a StreamingAssets/Licenses/Packages, incluyendo dependencias y herramientas de editor; no significa que todas se ejecuten en el juego.

### Herramientas Unity y dependencias transitivas
- Autor: Unity Technologies, JetBrains, Microsoft y autores indicados en avisos de cada paquete.
- Fuente: https://docs.unity3d.com/Manual/PackagesList.html
- Licencia: específica por paquete: Rider/Visual Studio MIT; Version Control/Pipeline/Visual Scripting/Custom NUnit declaran Unity Package Distribution License; Searcher declara Unity Companion Package License v1.0; Test Framework/Performance y Settings Manager declaran Unity Companion License. No homogeneizar como CC0 o MIT. Burst solo tiene una copia runtime parcial sin LICENSE local: verificar la licencia/avisos de la versión completa antes de distribuir sus componentes.
- Rutas: `Library/PackageCache`.
- Evidencia: LICENSE y Third Party Notices instalados, versionados en la copia distribuida de avisos; revisar tras actualizar paquetes. Módulos internos del Editor bajo términos de Unity.

### Unity Graph Authoring 1.0.0 y Burst 2.0.0 shim — avisos incompletos
- Autor: Unity Technologies (paquetes internos de Unity).
- Fuente: https://docs.unity3d.com/Packages/com.unity.graph-authoring@1.0/ ; https://docs.unity3d.com/Packages/com.unity.burst@2.0/
- Licencia: SIN AVISO LOCAL específico en las copias instaladas; no extrapolar términos de Burst 1.8 a 2.0. Verificar avisos de esta versión del Editor antes de redistribuir estos componentes por separado.
- Estado: no son assets artísticos descargados, sino tooling interno y shim del Editor. No se presume una licencia CC0; las URL oficiales de licencia para estas versiones no fueron accesibles durante la auditoría. Detalle en PackageLicenseInventory.csv.

## Creaciones y derivados locales — sin descarga nueva

### Geometría, terreno, UI y derivados de Rally
- Autor: proyecto Rally (trabajo local del proyecto asistido por herramientas).
- Fuente: repositorio del proyecto; no proveedor externo. https://github.com/bocchetto-franco/rally
- Licencia: no se declara una licencia general nueva del proyecto en esta auditoría. Los derivados conservan las licencias de sus fuentes CC0, CC-BY, MIT, OFL/Apache o Unity; no relabelar todos como CC0.
- Rutas: `Assets/Art/Environment/PackedTextures`; `Assets/Art/Environment/Materials`; `Assets/Art/Environment/Prefabs`; `Assets/Art/Environment/Terrain`; `Assets/Art/Environment/Circuit02`; `Assets/Art/Environment/Circuit01Dressing`; `Assets/Art/Forest/Materials`; `Assets/Art/Forest/Prefabs`; `Assets/Art/Forest/Terrain`; `Assets/Art/Forest/Circuit03`; `Assets/Art/Sierras/Circuit02`; `Assets/Art/PerformanceLOD`; `Assets/Art/PuddleDressing`; `Assets/Art/RallyFinish`; `Assets/Art/RoadTerrainBlend`; `Assets/Resources/MenuPreviews`; `Assets/Resources/MenuBackdrops`; `Assets/Resources/Audio/RallyAudioLibrary.asset`; `Assets/Prefabs`; `Assets/Scenes`.
- Alcance: trazados ProBuilder, mapas de alturas y depresiones, puente/río, arcos de meta a cuadros, antiguos fardos de heno (geometría propia; retirados), máscaras PBR/alpha, prefabs/LOD/multitudes combinadas, materiales agua/bordes, capturas y copias visuales de menú. No hay modelo de fardo descargado. Agua deriva de Unity; vegetación/terreno/público/decoración de los autores anteriores. El material Sierra daylight sky y las copias de cielo/agua heredan el origen ya listado.

## Notas de distribución

Conservar CREDITS.md y StreamingAssets/Licenses en las builds. La pantalla muestra esta misma lista, incluidas las advertencias; no reemplaza los textos legales completos. BMW, Audi y los dos modelos archivados requieren atribución incluso si se redistribuyen como recursos. Mantener las indicaciones de cambios y no sugerir patrocinio.

Porsche: antes de publicar/vender, obtener autor, enlace y licencia del modelo/mapas; si no se consigue autorización, reemplazarlo o excluirlo explícitamente. Electronic Highway Sign: verificar términos o excluir el ejemplo de la build. Las marcas de autos, patrocinadores y liveries pueden tener derechos distintos de la licencia del archivo 3D; este inventario no los concede. Esta auditoría técnica no sustituye una revisión legal de publicación.
