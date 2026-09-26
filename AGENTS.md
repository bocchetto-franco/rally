# Rally — instrucciones para agentes

## Contexto del proyecto

- Este repositorio contiene un videojuego de rally desarrollado con Unity `6000.6.0f1`.
- El pipeline de render es Universal Render Pipeline (URP).
- El auto principal es el Porsche de `Assets/auto/`. El auto de policía original permanece desactivado en la escena como referencia y alternativa de recuperación.
- La escena de desarrollo para probar el auto es `Assets/JS Vehicle Physics Controller/Scene AMR/PC Controller Scene AMR 01.unity`.
- Hay tres circuitos jugables y seleccionables: `Circuit_01`, `Circuit_02` y `Circuit_03` (`Assets/Scenes/Circuit_0X.unity`). `Circuit_01` es un loop desértico recortado; `Circuit_02` es otro loop desértico de estilo similar; `Circuit_03` es un circuito de bosque/montaña con sus propias texturas y vegetación. No reutilizar en `Circuit_03` los assets desérticos.
- `Active Input Handling` está configurado como `Both` en Player Settings; no cambiarlo.

## Flujo de escenas y carrera

- Ya existe un frontend funcional antes de la carrera. Al entrar en Play, el proyecto debe comenzar en `Assets/Scenes/MainMenu.unity` mediante `EditorSceneManager.playModeStartScene`.
- El botón **Jugar** de `MainMenu` abre `Assets/Scenes/VehicleCircuitSelection.unity`.
- La selección ofrece un único auto, **Porsche 911 SC Rally**, y tres circuitos: **Circuit_01**, **Circuit_02** y **Circuit_03**. `RallyGameSession` conserva ambas selecciones en PlayerPrefs (`Rally.SelectedVehicle` y `Rally.SelectedCircuit`).
- La selección incluye los tres circuitos; cada botón actualiza la pista y **Comenzar carrera** carga la escena correcta. `Circuit_01` tiene carrera cronometrada, checkpoints y resultados. `Circuit_02` y `Circuit_03` son jugables en modo recorrido libre; todavía no tienen checkpoints, timer ni resultados.
- En `Circuit_01`, el GameObject `Race Flow` usa `RallyRaceFlow`: al finalizar congela el Porsche, deshabilita el control y la dinámica adicional, pausa con `Time.timeScale = 0` y muestra un Canvas con tiempo final, reinicio, ranking y regreso a selección. El ranking guarda los cinco mejores tiempos por circuito en PlayerPrefs bajo `Rally.BestTimes.*`. Los botones restauran `Time.timeScale = 1` antes de cambiar o recargar escena.
- Desde resultados se puede repetir la carrera, volver a selección o regresar al menú principal.
- Al completar una vuelta en `Circuit_01`, aparece el menú de fin de carrera con el tiempo, **Reiniciar**, **Ver tiempos** (cinco mejores guardados por pista mediante PlayerPrefs) y **Volver al menú**, que retorna a la selección de auto/pista.
- El orden esperado en Build Settings es: `MainMenu`, `VehicleCircuitSelection`, `Circuit_01`, `Circuit_02`, `Circuit_03`, `RaceResults`.
- La configuración y reparación de este flujo se centraliza en `Assets/Editor/RallyFrontendSetup.cs` y su comportamiento en `Assets/Scripts/RallyMenuController.cs`. No cambiar los nombres de escena sin actualizar ambas partes.

## Estado de `Circuit_01`

- Es un loop corto de rallycross de aproximadamente 1512 m (antes 3637 m). Conserva los primeros ~578 m del recorrido anterior, con curvas de 110°, chicana y desniveles; una horquilla nueva de 180° y radio 22 m inicia el retorno exterior a la salida. No restaurar el recorrido largo sin pedido explícito. El marcador `Circuit 01 Short Loop v1` hace que la ruta compartida de `Circuit01LoopSetup` genere el trazado corto.
- Tiene checkpoints y cronómetro funcionales, HUD de carrera y reinicio del auto al último checkpoint con la tecla `R`.
- Queda el charco del tramo inicial (aprox. 100 m), con posición, tamaño y trigger originales. Usa el Shader Graph oficial del sample de agua con refracción leve, profundidad translúcida, reflejo ambiental y normales onduladas; genera salpicaduras livianas al atravesarlo desde 35 km/h. Su gameplay aplica `5 m/s²` de resistencia base, hasta `2.5 m/s²` extra a alta velocidad y aquaplaning direccional progresivo entre 55 y 110 km/h sin modificar permanentemente los WheelColliders. Se retiraron los tres charcos del recorrido eliminado.
- Quedan seis fardos de heno, con Collider y Rigidbody dinámico originales; se retiraron 18 del recorrido eliminado. Hay diez gates ordenados, salida y meta separadas, aproximadamente cada 167 m.
- La ambientación incorporada por Astra incluye Terrain montañoso/desértico, materiales de suelo árido, vegetación seca dispersa y rocas.
- Hay 12 espectadores estáticos de Quaternius (LowPoly Posed Humans, CC0), en cuatro grupos de tres bajo `Rally Spectators - Outside Barriers`. Assets y licencia en `Assets/Art/Environment/QuaterniusPeople/`. `Circuit01SpectatorsSetup.cs` coloca y valida el público: cuerpo completo al exterior de las barreras reales, separación mínima exigida de 3 m (medida actual: 5.98 m), fuera del camino y mirando a la pista. Si se cambia el trazado o las barreras, revalidar estas posiciones; no mover público dentro del área jugable.

## Estado de optimización

- El Terrain junto a la recta larga del retorno (487 m, desde `(255.16, 0, 432.41)` hasta `(255.16, 0, -55)`) tiene una transición localizada de 141 m por lado, con fundido de 60 m en los extremos. La franja anterior de 20 m producía paredes de hasta ~80 m junto al camino. `Circuit01StraightTerrainRepair.cs` aplica la corrección solo fuera del corredor de 14 m, protege otros tramos y plataformas del público, y vuelve a apoyar las rocas afectadas. No regenerar globalmente el Terrain ni volver a ejecutar `FitTerrain` del recorte, porque restauraría el corte. El informe y respaldo están en `Logs/straight-terrain-repair.txt` y `Logs/SceneBackups/StraightTerrain_20260923_182450/`.

- `Circuit01CrowdSetup.cs` agregó 80 espectadores estáticos en cuatro grupos de 20, además de los 12 anteriores. Buscar `Large Rally Crowds - Outside Barriers` en Hierarchy. Poses existentes de Quaternius, posición/yaw/escala y ropa variadas; mallas combinadas por material y por zona: 28 MeshRenderers, sin colliders, Rigidbody ni Animator adicionales, sombras proyectadas desactivadas y culling por LODGroup. No se ha medido todavía un framerate comparativo. Separación mínima validada del cuerpo completo a barreras: 5.38 m. Posiciones individuales originales registradas en `Assets/Art/Environment/QuaterniusPeople/LargeCrowdPlacements.txt`; se hornearon en las mallas, no hay un GameObject por persona. Al cambiar la pista, revalidar tanto esta raíz como el público anterior.

- El recorte se aplicó con `Circuit01ShortLoopSetup.cs`; el terreno se ajustó al nuevo retorno, se regeneraron las barreras y se retiraron/desactivaron 55 colocaciones de vegetación del nuevo margen de escape. Se conservó el ambiente lejano. Los conteos de vegetación siguientes son previos al recorte; no asumir que todos siguen activos. Respaldo local del circuito largo: `Logs/SceneBackups/ShortLoop_20260921_105154/`.

- Vegetación de `Circuit_01`: 80 árboles Quiver, 850 Searsia Lucida, 180 arbustos rooibos y 195 pastos. Arbustos y pastos usan Terrain Details con `alignToGround = 1`; los árboles siguen la normal del Terrain.
- `Circuit01VegetationRepair.cs` repara/revalida las plantaciones y aumenta densidad sin tocar gameplay. Conservar la corrección de ejes del FBX al hornear mallas (Quiver tiene rotación raíz de -90° en X); no cancelarla con `worldToLocalMatrix` de la raíz. Comprobar el volumen de la planta y el jitter de Terrain Details contra el camino, no solo su centro.

- Ya se aplicaron manualmente Half Res en texturas, menor distancia de sombras y menor Far Clip Plane. En la revisión posterior se comprobó que ningún `Circuit_0*` tenía un asset de Occlusion Culling baked referenciado; ahora cada uno tiene su propio bake en `Assets/Scenes/Circuit_01/`, `Circuit_02/` y `Circuit_03/` (archivo `OcclusionCullingData.asset`). Rehornear la escena correspondiente después de cambiar su geometría o terreno.
- En la optimización más reciente, aproximadamente 292 colocaciones superiores de vegetación (195 pastos y 97 arbustos, que expandían a unos 4092 GameObjects) se migraron a Terrain Details. Se conservaron 73 colocaciones de rocas como GameObjects.
- Los Terrain Details quedaron con resolución `1024`, patch resolution `32`, distancia de dibujo `180 m` y densidad `1`.
- Se desactivó la feature SSAO del renderer de PC (`Assets/Settings/PC_Renderer.asset`). `Circuit_01` no tiene Volumes de postprocesado y su cámara no usa postprocesado.
- Solo la luz direccional principal queda activa en tiempo real; las luces auxiliares encontradas en vehículos están desactivadas.
- Se auditaron 32 texturas de ambiente y ninguna supera 2048 px. Se limitaron a 1024 px y con compresión las texturas de pasto y cinco texturas del sample de agua (`FlipbookTest`, `foam_detail_tiling`, `foam_mask`, `ocean_foam_blend_ramp`, `puddle_norm`).
- `Assets/Editor/Circuit01PerformanceSetup.cs` contiene la automatización de optimización y `Assets/Editor/Circuit01EnvironmentSetup.cs` vuelve a convertir la vegetación a Terrain Details si se reconstruye el ambiente.
- LOD auditado en los tres circuitos: vegetación y grupos grandes de público ya tenían `LODGroup`; se añadieron grupos de LOD con descarte conservador a 1.5% de altura de pantalla a los cuatro grupos pequeños de espectadores de `Circuit_01`. Esto no reemplaza las mallas por variantes de menor poligonaje; no afirmar una mejora de FPS sin medirla.
- Los tres Terrain mantienen `Tree Distance = 5000 m` y `Detail Distance = 180 m`. Las nuevas texturas de bosque/Poly Haven son 2K con compresión en el importador; no se modificaron sombras, resolución ni otros ajustes generales de calidad durante esta revisión.

## Assets reutilizables para nuevos circuitos

Usar estas rutas verificadas en `main` como base para nuevos circuitos. Las fuentes y licencias están documentadas en `Assets/Art/Environment/ASSET_SOURCES.md` y `Assets/Art/Environment/DOWNLOAD_MANIFEST.json`.

- **Texturas de terreno desértico:** fuentes PBR de Poly Haven en `Assets/Art/Environment/PolyHaven/`, en las carpetas `dry_ground_01/`, `gravelly_sand/` y `rock_boulder_dry/`. Para configurar un Terrain, reutilizar las capas `Assets/Art/Environment/Terrain/dry_ground_01.terrainlayer`, `gravelly_sand.terrainlayer` y `rock_boulder_dry.terrainlayer`. Los mapas adaptados/empaquetados están en `Assets/Art/Environment/PackedTextures/`; el Terrain actual es `Assets/Art/Environment/Terrain/Circuit01_AridTerrain.asset`.
- **Vegetación árida:** prefabs listos en `Assets/Art/Environment/Prefabs/`: `quiver_tree_01_optimized.prefab`, `searsia_lucida_optimized.prefab`, `wild_rooibos_bush_upright.prefab` y `grass_medium_01_upright.prefab`. Los modelos y mapas fuente están en las subcarpetas con el mismo nombre bajo `Assets/Art/Environment/PolyHaven/`.
- **Personas/espectadores:** pack Quaternius LowPoly Posed Humans en `Assets/Art/Environment/QuaterniusPeople/`. Incluye FBX, prefabs, mallas combinadas y materiales; licencia en `Assets/Art/Environment/QuaterniusPeople/License.txt` y fuente en `SOURCE.md`. Para grupos grandes, reutilizar las mallas combinadas que ya están allí.
- **Material y shader de agua:** material listo de los charcos: `Assets/Art/Environment/Materials/Puddles - Unity sample.mat`. Usa el Shader Graph URP `Assets/Art/Environment/UnityWaterSample/ProductionReady/Environment/Water/WaterSimple_FoamMask.shadergraph`; sus dependencias viven bajo `Assets/Art/Environment/UnityWaterSample/`. Licencia del sample en `Assets/Art/Environment/UnityWaterSample/LICENSE.md`.
- **Fardos de heno:** no hay un modelo importado independiente. `Assets/Editor/Circuit01LoopSetup.cs`, método `CreateProps()`, genera cilindros ProBuilder en la escena y les agrega `CapsuleCollider` y `Rigidbody`. Reutilizar el material `Assets/Scenes/Circuit_01_HayPlaceholder.mat` y esa rutina para mantener su forma y comportamiento.
- **Checkpoints y timer:** la lógica principal está en `Assets/Scripts/RallyCheckpointManager.cs` (orden de checkpoints, cronómetro y reinicio con R) y `Assets/Scripts/RallyCheckpointTrigger.cs` (trigger individual). `Assets/Editor/Circuit01CheckpointSetup.cs` instala los gates; para la geometría cerrada actual, `Circuit01LoopSetup.RebuildCheckpoints()` los reconstruye siguiendo el loop. HUD: `Assets/Scripts/RallyRaceHud.cs`; flujo de meta/resultados: `Assets/Scripts/RallyRaceFlow.cs`.

## Circuitos adicionales (geometría y ambientación)

- `Assets/Scenes/Circuit_02.unity`: loop desértico independiente de aproximadamente 1631 m, dos horquillas de 180°, chicana, una recta larga de 150 m, anchos de 7–12 m y tres desniveles (+8, +12 y -5 m). Reutiliza exclusivamente los assets áridos existentes. Terrain, mallas de público y mapa de recorrido: `Assets/Art/Environment/Circuit02/`. Generador: `Assets/Editor/Circuit02Builder.cs`.
- `Assets/Scenes/Circuit_03.unity`: loop de bosque/montaña independiente de aproximadamente 1700 m, dos horquillas de 180° (radios 28/22 m), chicana y curvas encadenadas, una recta larga de 130 m y tres desniveles más pronunciados (+20, +28 y -10 m). Tiene 380 coníferas, 240 arbustos, 180 plantas bajas y 50 rocas low-poly con mallas/materiales compartidos, instancing y descarte por distancia. Generadores: `Assets/Editor/Circuit03Builder.cs` y `Circuit03ForestAssets.cs`.
- Ambos tienen cuatro charcos de 6 × 10 m sobre hondonadas de 0.28 m, fardos visuales y cuatro grupos de 20 espectadores combinados por material. El público queda fuera de las barreras; separación mínima del cuerpo completo medida: 14.42 m en Circuit_02 y 14.39 m en Circuit_03.
- Por pedido explícito, **no tienen checkpoints, timer, lógica de charcos ni física de fardos**. No agregar esos sistemas automáticamente. Los Porsche de prueba están activos en la salida y conservan su configuración de Circuit_01.
- Para conducirlos desde el editor: **Tools → Rally → Play Circuit 02/03 - Free Drive**. Al salir se restaura la escena de inicio anterior. El frontend también ofrece ambas pistas como recorridos libres. Todavía no tienen resultados ni cronómetro.
- No reemplazar ni regenerar el Terrain de Circuit_01. Cada escena nueva tiene su propio TerrainData. `Circuit02Layout.json` / `Circuit03Layout.json`, junto a sus respectivos terrenos, documentan línea central, distancias, anchos, charcos y público para conectar gameplay más adelante.
- No reutilizar el bake de oclusión de Circuit_01 en Circuit_02/03: cada circuito tiene su `Assets/Scenes/Circuit_0X/OcclusionCullingData.asset` propio y su cámara con Occlusion Culling activado. Rehornear por separado cuando cambie la geometría estática.

### Assets de bosque reutilizables (Circuit_03)

- Suelos nuevos CC0 de Poly Haven: `Assets/Art/Forest/PolyHaven/mud_forest/`, `forest_floor/` y `mossy_rock/`. Color y normales de 2K comprimidos; **no son texturas del desierto**. Capas listas: `Assets/Art/Forest/Terrain/*.terrainlayer`.
- Camino: `Assets/Art/Forest/Materials/Wet forest road.mat`; banquina: `Forest floor.mat`; Terrain: `Forest Terrain.mat`, en esa misma carpeta.
- Cielo: `Assets/Art/Forest/Materials/Overcast sky.mat`, con HDRI CC0 `Assets/Art/Forest/PolyHaven/kloofendal_overcast/kloofendal_overcast_2k.hdr`.
- Vegetación: Kenney Nature Kit CC0 en `Assets/Art/Forest/KenneyNature/`, licencia original incluida. Prefabs URP preparados en `Assets/Art/Forest/Prefabs/{PineA,PineB,PineC,Bush,Fern,Rock}.prefab`. `Fern` es el nombre interno del prefab de planta baja `plant_flatShort`, no un asset botánico específico.
- Terrain de la escena: `Assets/Art/Forest/Circuit03/Circuit03_ForestTerrain.asset`. Fuentes/licencias/checksums y guía de uso en `Assets/Art/Forest/ASSET_SOURCES.md`, `DOWNLOAD_MANIFEST.json` y `README.md`.
- El público, material de agua y fardos usan las rutas compartidas ya documentadas arriba. No cambiar el material de agua compartido para afinar una sola escena.
- Se verificaron cierre, raycasts del camino, ajuste de altura del Terrain, depresiones y conservación de tuning. Informes y renders en `Logs/circuit02-build.txt`, `Logs/circuit03-build.txt` y `Logs/Circuit02_Preview_*.png` / `Circuit03_Preview_*.png`. No hay todavía medición comparativa de FPS ni validación manual de una vuelta completa.

## Reglas de trabajo

- Cada vez que se cree o actualice una pista, dejar el Porsche principal activo al inicio, orientado hacia el recorrido y apoyado correctamente sobre el camino, con controles y cámara de seguimiento funcionando. Verificar su posición antes de guardar la escena; no dejar pistas de prueba sin auto salvo pedido explícito.
- El auto principal utiliza el asset de físicas de `Assets/JS Vehicle Physics Controller/`. Reutilizar y configurar ese sistema; no reescribir las físicas desde cero.
- Los cuatro WheelColliders y la física del Porsche fueron afinados manualmente durante muchas iteraciones para un feeling específico de rally arcade: alto derrape lateral trasero, `Mass` ajustada, `Angular Damping` aumentado, mayor `Steer Angle`, respuesta de dirección más rápida y curvas de `Sideways Friction` y `Forward Friction` personalizadas.
- No revertir ni modificar `Mass`, `Angular Damping`, `Sideways Friction`, `Forward Friction`, `Steer Angle` o la velocidad de respuesta del volante sin una petición explícita del usuario.
- La asistencia de estabilización anti-trompo de `RallyVehicleDynamics` es parte del tuning intencional del Porsche. No desactivarla ni revertir su umbral o fuerza correctiva sin petición explícita.
- Nunca modificar ni borrar el auto de policía original incluido dentro de la carpeta del asset. Para hacer pruebas o variantes, desactivarlo o duplicarlo.
- Mantener URP y usar shaders y materiales compatibles con URP.
- No romper la geometría, los checkpoints, los triggers de charcos ni la física de los fardos al trabajar únicamente sobre arte o rendimiento.

## Efectos de conducción implementados

- El Porsche tiene downforce cuadrático fuerte que comienza a `65 km/h`, usa coeficiente `3.2` y está limitado a `4500 N`. Se aplica en el centro de masa y solo cuando ambos ejes tienen contacto con el suelo, para evitar torque artificial o levantar un eje.
- La frenada de servicio se activa con `S` o flecha abajo mientras el auto avanza y usa `4200 Nm` por rueda delantera y `1800 Nm` por rueda trasera, equivalentes a un reparto aproximado 70/30. Incluye modulación por `forwardSlip` para reducir bloqueos; Espacio sigue siendo el freno de mano trasero.
- Para retrasar el trompo sin quitar el derrape normal, `Angular Damping` quedó en `3.0`, el `Asymptote Slip` lateral trasero en `0.72`, y la fricción longitudinal trasera en `1.05` de Extremum Value y `0.76` de Asymptote Value.
- La asistencia anti-trompo de `RallyVehicleDynamics` comienza únicamente al superar `1.75 rad/s` de velocidad de guiñada local y aplica hasta `4.5 rad/s²` de aceleración angular correctiva mediante una entrada progresiva. Solo actúa con ambos ejes apoyados; estos dos valores están serializados bajo `Arcade spin stability assist` y pueden afinarse desde el Inspector sin modificar las fricciones.
- Las dos ruedas traseras reutilizan los Particle Systems y el material de polvo incluidos en el asset. Cada emisor se activa independientemente al superar `0.18` de `sidewaysSlip`, se apaga por debajo de `0.12` y requiere al menos `25 km/h`.
- El humo es solamente visual: no modifica las curvas de fricción ni aplica fuerzas al auto.

## Pendientes conocidos

- Medir el rendimiento en ejecución y evaluar LOD con mallas de menor detalle donde aporte una mejora comprobable sin pérdida visual notoria.
- Próximo desarrollo: IA de rivales controlados por bots, con waypoints y comportamiento de manejo.
- Próximo desarrollo: avisos de frenada estilo rally (notas del copiloto) antes de curvas; todavía no implementados.

## Estructura actual de `Assets`

- `Assets/auto/`: modelo, materiales y texturas del Porsche; también contiene utilidades para vincularlo al sistema de vehículo.
- `Assets/Art/Environment/`: assets ambientales, texturas, materiales y prefabs usados en el paisaje árido.
- `Assets/Editor/`: herramientas de configuración del frontend, circuito, ambiente y optimización.
- `Assets/JS Vehicle Physics Controller/`: asset principal de físicas del vehículo, con modelos, audio, materiales, postprocesado, prefabs, escenas, scripts, skyboxes y UI. No alterar el auto de policía original.
- `Assets/Scenes/`: escenas propias, incluidas `MainMenu`, `VehicleCircuitSelection`, `Circuit_01`, `Circuit_02`, `Circuit_03`, `RaceResults` y escenas auxiliares como `Circuit_Test_01`.
- `Assets/Scripts/`: lógica propia de menú y selección, flujo de carrera, checkpoints, timer, HUD, reinicio, charcos y comportamiento adicional del vehículo.
- `Assets/Settings/`: configuración de URP, renderers, perfiles de volumen y ajustes globales de render.
- `Assets/TutorialInfo/`: recursos y archivos informativos generados por la plantilla o tutorial de Unity.
