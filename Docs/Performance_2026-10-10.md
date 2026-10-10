# Optimización intermedia de circuitos y pantalla dividida

Estado: optimización aplicada, tres bakes nuevos y seis comparaciones de rendimiento completas. Verificación funcional detallada al final.

## Método y diagnóstico inicial

Recorrido reproducible del centro del circuito mediante `RallyRenderBenchmark`: Unity Editor 6000.6.0f1, salida 2560 × 1440, AMD Radeon(TM) Graphics, 6 s de calentamiento y 24 s de recorrido por caso. En 2P usa las dos cámaras/HUD reales, separadas un cuarto de vuelta, sin bots; en 1P conserva los tres bots. Desactiva VSync y el límite de FPS solo durante la prueba. Restaura escena, selección, modo local y ajustes al terminar. No es una vuelta conducida ni una build standalone.

Se capturaron contadores y marcadores mediante ProfilerRecorder: PlayerLoop, CPU main/render thread, GPU frame, Physics.Simulate, BehaviourUpdate, RenderLoop.Draw, Canvas.BuildBatch, asignaciones y FrameTimingManager. Son marcadores anidados/no aditivos y el editor añade trabajo de repaint; no confundirlos con un desglose exclusivo del frame completo. Las duraciones GPU del editor no bastan para certificar el cuello de botella GPU de una build.

En la pasada controlada previa: Physics.Simulate 0,53–1,14 ms/frame; BehaviourUpdate 0,26–0,33 ms/frame; PlayerLoop 6,9–13,7 ms frente a frames completos de aproximadamente 24–47 ms. Circuit_02 dibujaba 1,58 M/3,52 M triángulos en 1P/2P; Circuit_03 tenía 166/258 draws. Física y scripts de vehículo no fueron los costes dominantes medidos; se priorizó reducir geometría/rendering. El trabajo del editor/repaint pesa en el tiempo total, por lo que estos datos no permiten atribuir toda la lentitud a CPU o GPU de una build. Las asignaciones incluyen editor e instrumentación, no diagnostican una fuga runtime.

Se conservan las primeras pasadas `Logs/render-benchmark-intermediate-before.txt` y `Logs/render-benchmark-intermediate-after.txt`, pero **no se usan como ganancia causal**: mostraron una diferencia excesiva de estado/calentamiento del editor. La comparación final usa `Logs/render-benchmark-intermediate-controlled-before.txt` y `Logs/render-benchmark-intermediate-controlled-after.txt`: seis casos por pasada, cobertura 100% y `COMPLETE`. La pasada controlada anterior restituye temporalmente las 18 texturas a 2048 y el presupuesto LOD/Terrain anterior únicamente en las copias Play. Ambas pasadas conservan los bakes nuevos y los dos cambios de CPU: mide el efecto del presupuesto visual, no cada intervención por separado. Al terminar se restituyó 1024 y se conservaron intactas las escenas optimizadas.

Las unidades de ProfilerRecorder se obtienen de `UnitType`: `GC.Alloc` es tiempo, no bytes; `GC Allocated In Frame` sí cuenta bytes. La etiqueta inicial de GC.Alloc de las primeras pasadas era incorrecta; los informes controlados ya la corrigen. FrameTiming descarta timestamps repetidos, pero sus tiempos GPU en editor no sustituyen una captura GPU de build.

## Resultado controlado: antes → después

FPS medios aproximados; no son mínimos ni una promesa de 60 FPS.

| Circuito | 1 jugador | 2 jugadores | Variación 1P / 2P |
| --- | --- | --- | --- |
| Circuit_01 | 33,3 → 38,1 | 34,0 → 35,6 | +14,4% / +4,7% |
| Circuit_02 | 29,0 → 32,2 | 21,2 → 27,2 | +11,0% / +28,3% |
| Circuit_03 | 41,2 → 38,8 | 39,9 → 42,8 | −5,8% / +7,3% |

| Caso | Draws medios | Triángulos medios | Frame p99, ms |
| --- | --- | --- | --- |
| C01 1P | 79 → 65 | 472723 → 357441 | 85,17 → 55,47 |
| C01 2P | 130 → 108 | 827120 → 643249 | 47,30 → 50,41 |
| C02 1P | 105 → 95 | 1584951 → 1299960 | 68,69 → 73,75 |
| C02 2P | 229 → 203 | 3515648 → 2894501 | 112,02 → 70,09 |
| C03 1P | 166 → 119 | 154100 → 148360 | 40,76 → 45,91 |
| C03 2P | 258 → 183 | 263267 → 255783 | 39,74 → 37,02 |

La mejora más importante aparece en C02 dividido: frame medio 47,11 → 36,71 ms. **C03 individual no mejoró FPS**, aunque disminuyeron sus draws; algunos p99 tampoco mejoran. No se oculta esta regresión ni se reemplaza por la pasada inicial favorable. Falta repetir/perfilar una build para separar variación del editor, coste GPU y rendimiento de conducción real.

## Cambios y coste visual

- **LOD:** 2891 grupos ajustados (335/562/1994 en C01/02/03), umbrales de transición/descarte ×1,25 en vegetación, rocas, público y decoración nueva (aproximadamente 20% menos distancia para un mismo tamaño/proyección). No se alteran mallas cercanas ni posiciones; aumenta el popping y desaparecen antes detalles lejanos. Sin crossfade costoso. Los objetos activos ya tenían LOD: no se duplicaron componentes. Las consultas a objetos inactivos deben incluir sus padres inactivos.
- **Terrain:** Detail Distance 65 → 50 m; density 0,65 → 0,55; Tree Distance 650 → 500 m; billboard 55 → 45 m. En Circuit_02, Heightmap Pixel Error 7 → 14 y basemap 180 → 100 m; Circuit_01/03 conservan pixel error 20 y basemap 80 m. Menos pasto lejano y menos precisión visual del terreno lejano. No cambia TerrainData, alturas, pintura, huecos ni superficie física.
- **Texturas:** 18 importadores ambientales/skybox bajan de 2048 a 1024; conservan compresión. Camino y autos permanecen hasta 2048; no se alteran fuentes originales. Font atlases y assets originales del auto de policía quedan excluidos. Menor nitidez ambiental cercana y del cielo, sin quitar normales/PBR. La textura de camino compartida con banquinas sigue en 2048 porque también se usa en la carretera.
- **Draw calls:** static batching selectivo de decoración/rocas/público; materiales compartidos. Su beneficio final depende de la build, no se atribuye toda la reducción medida a batching. Terrain Details de C01/02 ya eran GPU-instanced y todos sus prototipos siguen válidos. Público grande ya combinado por material, no se desarma en cientos de personas. Los materiales repetidos conservan enableInstancing; SRP Batcher puede tener prioridad sobre instancing convencional de MeshRenderer, por lo que la casilla por sí sola no demuestra una reducción de draws. GPU Resident Drawer permanece desactivado por su regresión documentada al descargar escenas.
- **Iluminación:** las tres escenas ya tienen una sola luz principal y ningún Volume activo; los objetos pequeños ya carecen de sombras. Se mantiene ese presupuesto, y las sombras de árboles/afloramientos grandes de C02. No se recorta nuevamente render scale, resolución, distancia global de sombras, refracción de charcos ni calidad de autos/camino.
- **CPU:** `RallyRacePositions` actualiza su tabla compartida a 10 Hz en vez de cada render frame; la inscripción de corredores refresca inmediatamente. `RallyPuddleMudVisual` comparte una búsqueda de vehículos cada 5 s entre charcos y cachea Rigidbody. Conserva frecuencia física, umbrales y cantidad de partículas, inputs, cronómetro y frenado de agua.
- **Occlusion:** bake independiente nuevo en cada circuito, verificado por finalización, referencia propia, timestamp y archivo no vacío: C01 8324553 bytes, C02 5675310, C03 4743935. Informe `Logs/intermediate-optimization.txt`: `BAKES COMPLETE: PASS`. No reutilizar datos entre pistas.

## Preservación y reproducción

Editor: `Tools/RallyIntermediateOptimization.cs`, entradas explícitas `Apply`, `CompleteRockLODs`, `CompleteRemainingDecoration`, `Bake`. Estas fases se ejecutaron una vez; no repetir los multiplicadores de LOD sobre las escenas terminadas. Informe `Logs/intermediate-optimization.txt`.

Respaldo de escenas/bakes: `Logs/SceneBackups/IntermediatePerformance_20261010_143115/`. Los 18 `.meta` originales de texturas están en el respaldo de la primera pasada `IntermediatePerformance_20261010_142747/`, cuyo informe de importación se conserva en `Logs/intermediate-optimization-previous-pass.txt`.

`Tools/VerifyIntermediateOptimization.ps1 -Baseline Logs/SceneBackups/IntermediatePerformance_20261010_143115` compara byte a byte transforms, referencias de mallas, física, colliders, scripts/gameplay y ProBuilder. `Tools/RallyRankingPerformanceCheck.cs` prueba ordenamiento, sobrepaso, reset/teleport y cadencia en escenas de preview aisladas. La prueba de pantalla dividida existente comprueba ambos HUDs, input exclusivo, pausa y flujo de carrera con gamepads virtuales; no equivale a hardware físico.

- Preservación: `Logs/intermediate-preservation.txt`, **COMPLETE: PASS**, 3056/5019/6241 componentes protegidos idénticos en C01/02/03; nombres/actividad y componentes de GameObjects conservados salvo flags estáticos permitidos.
- Ranking: `Logs/ranking-performance-check.txt`, **COMPLETE: PASS**, tres circuitos, sobrepaso, teleport/reset y cadencia de 10 Hz.
- Pantalla dividida: `Logs/split-screen-test.txt`, **COMPLETE: PASS** en ejecución completa del 2026-10-10: dos DualSense virtuales, selección independiente, HUDs, dirección/gatillos/freno de mano/reset/enderezado exclusivos, pausa por dueño, ambos cronómetros y resultados, fallback teclado/mandos y 1P con tres bots en las tres pistas. La primera ejecución falló en una aserción de posición posterior al reset; no se reprodujo en la segunda ejecución completa y no se cambió código de input/reset ni se debilitó la prueba. El editor quedó fuera de Play y restauró preferencias/escena. Se retiraron únicamente las mutaciones incidentales de atlases TMP producidas por las pruebas, conservando respaldo local.
- No se cambian ajustes de física, controles, geometría, alturas físicas del Terrain, checkpoints, waypoints ni charcos. La menor frecuencia del ranking puede añadir hasta 0,1 s de latencia visual; no retrasa cronómetros ni triggers.
- Pendiente de medición adicional: build standalone, hardware gamepad real, vueltas completas conducidas y captura GPU. La revisión no garantiza 60 FPS ni mejora todos los percentiles.
