# Preparación de carrera y stutters de Windows

## Alcance y evidencia

Sin cambios a escenas, trazado, física, perfiles de jugadores/bots, calidad, resolución normal del juego ni bindings. El trabajo se coordinó con las cachés descritas en `Stutter_Runtime_Caches.md`.

La inspección confirmó que `RallyCheckpointManager.TryPass` no instancia objetos ni escribe PlayerPrefs. El guardado de mejores tiempos ocurre en `RallyRaceFlow.FinishRace`, después de finalizar la vuelta. No había un Save en mitad del recorrido que se pudiera eliminar.

La captura inicial de Windows/DX11 mostró trabajo de carga de escena en frames de 714–1121 ms, con marcadores de shaders de hasta 40 ms y asignaciones durante la integración/inicialización. Es evidencia de trabajo de entrada, no una medición válida de framerate: la ventana se lanzó oculta, no renderizaba normalmente y llenó el buffer de 40000 frames antes de acabar la prueba. Tampoco pasó checkpoints. Esas capturas se conservan en `Logs/Stutter/Baseline_*` pero **no prueban FPS, una vuelta completa ni la causa de lagazos de mitad de vuelta**.

## Cambios

- `RallyRaceLoading` presenta un Canvas opaco antes de `LoadSceneAsync`, congela tiempo/física y bloquea navegación y pausa durante la preparación.
- Espera Start y el rig 2P; prepara modelos seleccionables, clips de audio, dígitos/etiquetas TMP y la capacidad de los ParticleSystems existentes. No crea efectos nuevos en cada entrada de charco.
- Renderiza 48 vistas reales de la ruta detrás de la pantalla de carga, con las cámaras y URP existentes, para adelantar el primer uso de Terrain, LOD, agua, materiales alpha y shaders. Espera dos eventos `endCameraRendering` de cada cámara por vista: avanzar una corrutina con la ventana minimizada no equivale a renderizar. No ejecuta `WarmupAllShaders` ni carga todos los assets históricos de Resources.
- Restaura cámaras/emisores y estados enabled originales, recoge basura temporal bajo la carga y espera tres frames antes de habilitar la carrera. Si falla un recurso, mantiene el juego detenido y ofrece regresar al menú.
- Lanzar desde selección y reiniciar desde pausa/resultados utilizan el mismo gate. Cargar directamente una escena de carrera también instala la preparación.
- Búsquedas periódicas de vehículos/cámaras, etiquetas y texto numérico repetido se cachean. Véase la auditoría separada: las pruebas aisladas demuestran reducción de asignaciones, no atribuyen todos los hitches a GC.

## Verificación

El rerun del 2026-10-11 de `Tools/RallyLoadingCheck.cs` terminó en **COMPLETE: PASS** en `Logs/race-loading-check.txt`: C01/C02/C03, 1P y 2P, autos inmóviles durante carga pese a acelerador mantenido, timer/checkpoint cero, controles restaurados, cuatro corredores en 1P/dos en 2P, cuatro clips cargados y cachés completas de 4/2 autos y 1/2 cámaras. Exige renders reales, no solo yields. Son pruebas de Editor, no FPS de Windows; sus tiempos de preparación incluyen trabajo/variaciones del Editor y no prometen esos tiempos en el build.

Esta prueba detectó y permitió reparar un error de integración: los Start preexistentes podían construir una caché con solo Porsche y sin input local antes de que el Start del rig split, agregado por sceneLoaded, crease J2. Los consumidores ahora esperan `IsRosterReady`, y la carga deja terminar sus Start antes de inventariar recursos. No cambian controles ni física.

## Medición reproducible

`RallyFrameProbe` solo se instala con `-rally-frame-probe`. No hay profiler, autoconducción ni escritura de CSV por frame durante juego normal. Captura en memoria y escribe al finalizar; guarda y restaura PlayerPrefs a través del runner. Cuenta wall time, main thread, GC y shaders; comprueba foco, presentación y framebuffer para validar el render. Un contador no disponible se marca -1, no cero; Batches válido pero siempre cero no es un control positivo. La autoconducción usa gamepads virtuales con las físicas reales; no equivale a conducción humana.

`Tools/RallyStutterBuild.cs` generó el Development Diagnostic preservado; rechaza sobrescribir cualquier build existente. Para generar otro desde la fuente corregida, añadir una entrada/ruta nueva explícita, no reutilizar la carpeta Diagnostic de estas capturas. `Tools/RunStutterProbe.ps1 -Build Diagnostic -Circuit Circuit_03 -Players 1 -Seconds 160 -Label Cold -SkipPreparation -Visible` y la misma orden con `-Label Prepared` sin SkipPreparation comparan el gate en **el mismo ejecutable**, con las cachés comunes. Eso aísla la preparación, no mide por separado las ganancias de cada caché. Repetir en 2P, con Editor inactivo; no mezclar resoluciones, API, foco o trabajo simultáneo. `Tools/AnalyzeStutterProbe.ps1 -Csv <archivo>` muestra percentiles/picos y cobertura temporal; comprobar además checkpoints, ruta, foco y presentación real. After contiene la instrumentación anterior a habilitar Rendering explícitamente; Release no expone todos los marcadores y no sustituye al Development para este diagnóstico. No analizar CSV ni compilar/exportar durante una captura medida.

## Capturas históricas con Windows bloqueado

Windows permaneció bloqueado durante estas primeras pruebas; se detuvo la automatización de ventanas y se pidió al usuario desbloquear. Los procesos diagnósticos no interactuaron con la pantalla de bloqueo: usaron inputs virtuales y capturaron solo el framebuffer del juego. **Estas capturas no constituyen un benchmark de FPS válido en una sesión normal ni una comparación Windows 2P válida.** Se conservan como evidencia histórica; las pasadas desbloqueadas se documentan abajo. No declarar eliminados todos los stutters.

Capturas preservadas del mismo Development build `Build/Stutter_After_20261010/Rally1.exe`, DX11, AMD Radeon(TM) Graphics, 1280×720, fixture con límite diagnóstico 120 fps y Editor inactivo:

| Circuit_03, 1P | Sin preparación (`Cold`) | Con preparación (`Prepared`) |
| --- | ---: | ---: |
| Frames con creación de shader después de habilitar control | 6 | 0 |
| Mayor intervalo registrado después de habilitar control | 681.117 ms | 72.564 ms |
| Intervalos de más de 100 ms durante ese periodo | 3 | 0 |
| Cobertura | 160 s, checkpoint 8; detenido por contacto con bot | Vuelta completa, 78.99 s de prueba; resultados `01:17.900` |

Estos son intervalos y marcadores diagnósticos, **no FPS comparables ni una mejora porcentual**: la sesión estaba bloqueada, las coberturas fueron distintas y `Batches` figuró Valid pero permaneció cero en ambas. El framebuffer final sí mostró escenas reales y, en Prepared, el menú de resultados. Render Thread/Async Upload no estuvieron disponibles (-1). La comparación apoya que el primer uso de shaders/recursos se trasladó a la preparación, pero no demuestra la causa de todos los picos de mitad de vuelta.

En Cold hubo integración/inicialización en intervalos de 813–1364 ms y un pico inicial de carrera de 681 ms con trabajo de shader en los marcadores próximos (hasta 46.1 ms). En Prepared esos marcadores quedaron bajo la carga, que terminó aproximadamente a los 4.5 s. Los contadores remiten al frame anterior; no atribuir la totalidad del pico a shaders. El pico Cold de 124.6 ms a los 134.4 s no coincidió con GC ni creación de shader: falta un Timeline normal para atribuirlo a física, espera de driver o scheduling. No se modificó la física por conjetura.

Se corrigió después la herramienta para habilitar explícitamente los módulos Rendering/Memory del Profiler: activar CPU con `Profiler.enabled` no garantiza todos los módulos. Los CSV anteriores no se reetiquetaron como válidos. El runner queda listo para repetir con Windows desbloqueado; exigir contadores poblados, foco/pacing normal y cobertura comparable antes de publicar cifras de FPS.

Las 48 vistas no garantizan todos los PSO/mips en cualquier GPU/API, y la música continúa usando su importación streaming. La carga asincrónica desplaza integración e inicialización fuera del control del jugador; no elimina su coste.

## Diagnostic visible, sesión desbloqueada (2026-10-11)

Se completó `Build/Stutter_Diagnostic_20261010/Rally1.exe`, Development nuevo: cero errores, tres advertencias preexistentes, 6m58.934s, 738898637 bytes (`Logs/stutter-build-Diagnostic.txt`). Cuatro pasadas en el **mismo ejecutable**, DX11/AMD Radeon(TM) Graphics, ventana visible 1280×720, límite diagnóstico 120 fps, foco verdadero en todos los frames de carrera. La autoconducción usó el input/las físicas reales y J1 atravesó los once checkpoints hasta meta en las cuatro. El modo 2P conserva dos autos/cámaras reales; la captura termina al finalizar J1, no prueba que J2 haya finalizado también.

| Circuit_03 | Frames de carrera | P50 / P95 / P99 (ms) | Máximo (ms) | >50 / >100 ms | Creación de shader en carrera |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1P sin preparación | 9170 | 8.47 / 10.20 / 15.74 | 384.287 | 3 / 1 | 6 |
| 1P preparado | 9417 | 8.47 / 10.11 / 15.08 | 171.890 | 3 / 1 | 0 |
| 2P sin preparación | 9591 | 8.61 / 10.67 / 13.12 | 254.331 | 2 / 1 | 4 |
| 2P preparado | 9552 | 8.62 / 10.59 / 13.15 | 205.392 | 2 / 1 | 0 |

Archivos `Logs/Stutter/Diagnostic_Circuit_03_{1,2}P_{ColdUnlocked,PreparedUnlocked}.csv`, con metadatos, inventario de marcadores, log y framebuffer final. `Gfx.PresentFrame` tuvo muestras positivas en ~99.5% de los frames; la ventana/foco y las imágenes confirmaron presentación real. **Batches Count quedó válido pero siempre cero**, incluso con Rendering habilitado: no es utilizable para draw calls. Render Thread/Async Upload no estuvieron disponibles. No sumar tiempos de marcadores anidados ni tratar el valor del frame anterior como el trabajo exacto de la misma fila.

### Efecto de la propia instrumentación

Los máximos iniciales de esta tabla **no representan exclusivamente el juego**. El probe inventariaba marcadores después de activar `race_ready`: el getter `ProfilerRecorderDescription.Name` decodifica UTF8 y crea un string. Se leía nuevamente en cada iteración del bucle de 18 marcadores, provocando ~4.3 MB temporales, ~170–205 ms de configuración y un frame siguiente de GC/recuperación de física de ~70–79 ms. Se corrigió la fuente para leer Name una vez por handle y registrar el inventario antes del reloj de carrera. La implementación se puede verificar en el [código oficial de Unity](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Profiler/ScriptBindings/ProfilerRecorder.bindings.cs). **El Diagnostic de esta tabla es anterior a esa corrección**; no se reetiquetaron sus CSV ni se ocultaron sus picos. El Release existente no contiene ese inventario adicional y se usa para la verificación final.

Hubo también análisis de CSV al empezar alguna captura posterior; esas cuatro pruebas no son una comparación aislada de FPS para publicar porcentajes de mejora. La evidencia más clara del gate es que pasó de 6/4 muestras de shader posteriores a habilitar control a cero en 1P/2P, manteniendo cobertura completa de J1.

### Picos de mitad de vuelta: atribución limitada

En 1P preparado quedó un intervalo de 54.030 ms hacia 38.93 s/checkpoint 4. La muestra próxima de Main Thread fue 51.13 ms, pero física ~1.12 ms, BehaviourUpdate ~0.28 ms, Present ~0.63 ms, sin creación de shader/colección GC ni carga significativa. Los marcadores disponibles **no explican todo el intervalo**: falta Timeline nativo/GPU/scheduling para atribuirlo. En 1P frío hubo otro de 57.220 ms en checkpoint 6; física explica parte (~19.24 ms), no todo. No se alteró timestep, física ni presupuesto visual por conjetura. En 2P preparado, después de los dos primeros segundos diagnósticos, el máximo fue 36.686 ms y no hubo intervalos >50 ms; ese recorte se explica únicamente por el artefacto de configuración anterior, no se presenta como toda la captura libre de hitches.

Unity recomienda precalentar con configuraciones reales de render para evitar limitaciones de métodos genéricos: [documentación oficial Unity 6.6](https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reduce-stalling/shader-prevent-stutter/shader-prewarm-other).

Disponibilidad y módulos de contadores: [ProfilerRecorder](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html), [Profiler.SetAreaEnabled](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Profiling.Profiler.SetAreaEnabled.html). Valid identifica el marcador, no prueba que haya valores positivos registrados.

## Release aislado, foco mantenido (2026-10-11)

El usuario dejó el juego al frente y no hubo análisis, compilación ni capturas de escritorio durante estas dos pasadas. Mismo Release preservado, DX11, AMD Radeon(TM) Graphics, 1280×720 y cap diagnóstico de 120 fps, Porsche de referencia. Todas las filas de carrera tienen foco verdadero. Ambos procesos finalizaron con exit=0, preferencias restauradas y logs sin errores/excepciones.

| Circuit_03 preparado | Frames de carrera | P50 / P95 / P99 (ms) | Máximo (ms) | >50 / >100 ms | Cobertura real |
| --- | ---: | ---: | ---: | ---: | --- |
| 2P | 10035 | 8.38 / 9.56 / 14.60 | 147.953 | 3 / 1 | J1 completó los once gates en 85.57 s de prueba; J2 aún no había cruzado meta |
| 1P | 19154 | 8.36 / 9.35 / 9.66 | 134.004 | 3 / 1 | 160.40 s; autoconductor quedó detenido en la primera horquilla, checkpoint 1 |

Archivos `Logs/Stutter/Release_Circuit_03_{1,2}P_PreparedIsolated.csv`, metadatos/log/framebuffer y respaldos de preferencias. El framebuffer 2P muestra tiempo final de J1 `01:24.599` y J2 aún corriendo a 88 km/h, CP10/10: **no prueba resultados de ambos**. El probe se detiene cuando termina J1.

En 2P se reprodujeron intervalos de **67.093 ms a 25.99 s (CP3)** y **147.953 ms a 38.47 s (CP4)**, con Main Thread próximo de 64.34/148.01 ms y sin una colección GC registrada. El otro intervalo >50 ms (75.600 ms) fue la configuración inicial de gamepads virtuales del diagnóstico, después de abrir el gate; no representa exclusivamente la entrada normal del juego. Los marcadores de shaders/render/bytes GC no están disponibles en Release: no interpretar su ausencia como cero coste. La coincidencia de CP4 con un pico anterior de Diagnostic orienta el siguiente perfil, **no identifica aún la causa**.

En 1P solo hubo 884 frames con velocidad >5 km/h, hasta 11.18 s, máximo 22.13 ms en ese breve tramo. El percentil global está dominado por el coche detenido y **no valida rendimiento durante una vuelta completa**. El pico de 134.004 ms ocurrió a 97.03 s, estando detenido, sin colección GC registrada; no se puede atribuir a un checkpoint que no se estaba cruzando. No se cambió física/IA para favorecer esta prueba.

La preparación mantuvo el trabajo inicial bajo el Canvas: primeros registros `race_ready` alrededor de 4.59 s (2P) y 3.67 s (1P), incluyendo después el setup del probe. No garantizan el mismo tiempo de carga en todo equipo. Los percentiles anteriores describen este fixture y cap, no una garantía general de 120 fps. **Queda un hitch real de carrera por atribuir; no declarar eliminados todos los lagazos.**

`RunStutterProbe.ps1 -Timeline` permite una captura nativa acotada en Development, con `-profiler-enable`, `-profiler-log-file`, `-profiler-capture-frame-count 6000` y buffer de 64 MiB; no altera el juego normal. Su coste de instrumentación/escritura impide usarla como benchmark Release. `Tools/RallyStutterTimeline.cs` analiza scopes inclusivos/self y preserva primero los datos del Profiler de Editor. Referencia de estos argumentos: [Unity 6.6, documentación oficial](https://docs.unity3d.com/6000.6/Documentation/Manual/profiler-command-line-arguments.html). La nueva captura está pendiente de disponibilidad del usuario para mantener el foco.

## Ejecutable y cierre

- Ejecutable normal: `Build/Stutter_Release_20261010/Rally1.exe` (generado 2026-10-11). Release sin Development, seis escenas incluidas; 658762970 bytes. No se sobrescribió el build previo `Windows_Test_2026-10-10`.
- Development After y Release terminaron `COMPLETE: PASS`, cero errores, tres advertencias preexistentes: prebake de colisión deshabilitado en seis mallas ProBuilder, configuración Runtime del plugin Pipeline ausente (no del URP del juego), pragma antiguo del shader TMP. Informes `Logs/stutter-build-After.txt` y `stutter-build-Release.txt`. No se modificaron geometría/colisiones ni paquetes para silenciarlas.
- Smoke del Release en Circuit_03, 1P y 2P, 20 s cada uno: ambos procesos terminaron exit=0, controles virtuales hicieron avanzar el auto, guardado de preferencias restaurado y logs sin Exception/Error/Missing/Failed. Framebuffers en `Logs/Stutter/Release_Circuit_03_1P_Smoke.csv.png` y `Release_Circuit_03_2P_Smoke.csv.png`. No equivale a vueltas completas en ambos modos ni prueba de mandos físicos. Los marcadores de GC/shader/render no están disponibles en este Release y no se usan como cero coste.
- Pasadas posteriores de Release desbloqueado, preservadas sin ocultar fallos: `Release_Circuit_03_1P_PreparedCleanUnlocked.csv` terminó a los 160.84 s de prueba, volcado en checkpoint 3 con el Audi que estaba seleccionado en las preferencias; no valida una vuelta completa. Hubo 5083/9527 frames de carrera sin foco. `Release_Circuit_03_2P_PreparedReferenceUnlocked.csv` usa Porsche para ambos, completó once gates y el framebuffer confirma **resultados de J1 y J2 (01:25.060)**; 2845/3685 frames sin foco. Ambos procesos exit=0, logs sin errores/excepciones y preferencias restauradas.
- **Estas dos pasadas tampoco certifican FPS aislados**: la captura de ventana mostró otra aplicación reproduciendo video delante del juego y no se pudo mantener foco. Hubo intervalos >100 ms (máximos 1279.792/633.702 ms en carrera); se registran como problema reproducido bajo esas condiciones, no se atribuyen al juego ni al video sin un Timeline/control experimental. Solo Main Thread está disponible en este Release; GC/shader/render siguen -1. No se publican porcentajes de mejora a partir de estos datos. Se pidió al usuario una ventana de 2–3 minutos sin actividad gráfica externa para repetir.
- El runner ofrece `-ReferencePorsche`: fija temporalmente las dos preferencias existentes de modelo, resueltas por sus nombres reales de Unity, y las restaura byte a byte al finalizar. Esto controla el fixture de autoconducción, no cambia la selección del usuario ni el tuning del juego. La primera captura fallida anterior permanece intacta.
- Comprobación posterior: preferencias de Release conservadas byte a byte (valores, tipos y cantidad de claves). Compilación local de `Assembly-CSharp` y `Assembly-CSharp-Editor`, incluyendo el probe corregido y las fuentes actuales del preview desarrollado en paralelo: **cero errores/cero advertencias**, 30.30 s, `Logs/stutter-csharp-final-restored.log`. El primer intento `--no-restore` falló porque Unity había eliminado el `project.assets.json` temporal; el restore estándar reconstruyó ese archivo, sin modificar fuentes/paquetes del proyecto. `recompile_status` de Unity informó completed/failed=false/errors=[]/compilationFailed=false. Esto valida C#, no es una nueva generación del ejecutable Release ni la medición de rendimiento pendiente.
- Se restauraron las fuentes/cachés de URP generadas por build y exclusivamente la adición automática de InputActions a `preloadedAssets`. Escenas, prefabs, perfiles de física, texturas, gráficos y Active Input Handling sin diff de esta tarea. No se hizo commit/push.
- Pendiente: atribuir con Timeline los picos tardíos de la pasada aislada. El foco ya se mantuvo en ambos modos, pero 1P tuvo cobertura parcial y 2P conservó un pico de 148 ms. La implementación está lista y las fuentes de trabajo verificadas quedaron adelantadas/cacheadas; no se garantiza la eliminación total de lagazos. El Release es una instantánea de esta tarea, anterior al nuevo preview 3D de autos desarrollado en paralelo; no contiene ese cambio del menú.
