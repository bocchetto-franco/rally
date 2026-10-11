# Stutters: auditoría y cachés de carrera (2026-10-10)

## Cambios de runtime

No se modificaron escenas, geometría, parámetros de física, dificultad, input bindings ni presupuestos visuales.

| Fuente encontrada | Corrección |
| --- | --- |
| HUD: strings interpolados de velocidad, puesto/vuelta, checkpoints y tiempo. El tiempo se comparaba como float y se formateaba cada frame. | Formato numérico de `TMP_Text.SetText`, caché de valores visibles y refresco visual del reloj a 30 Hz. El cronómetro sigue acumulando el mismo `deltaTime`; la meta y los resets se muestran inmediatamente, con milisegundos. |
| `RallyCheckpointManager.FormattedTime` creaba strings incluso al consultar repetidamente el mismo tiempo. | Getter cacheado por milisegundo visible. Corrige también el acarreo `00:60.000` a `01:00.000`. |
| `RallyBrakeWarningTrigger.NoteLabel` concatenaba dirección y grado al compararse cada Update del aviso. | Doce etiquetas constantes reutilizables; grado/dirección siguen siendo configurables. |
| Aviso de vuelco y controles de recuperación consultaban componentes y reconstruían el texto repetidamente. | Referencias al enlazar/arrancar el auto; etiqueta cambia solo al cambiar dispositivo/propietario/modo. Configuración explícita notifica cambios de input/rol sin búsquedas por frame. |
| Charcos visuales: `FindObjectsByType` y un array de autos nuevos cada 5 segundos. | `RallyVehiclePresentationCache`: un scan por escena al completar el roster real. Los Start de efectos/notas esperan `IsRosterReady`, porque el rig agregado en sceneLoaded puede empezar al frame siguiente pese a su execution order. Cache compartida y liberada al descargar escena; reset `SubsystemRegistration` para Domain Reload desactivado. Los componentes deshabilitados temporalmente por la carga conservan sus referencias; los GameObjects inactivos se excluyen. |
| Charcos de gameplay: `GetComponent` cada FixedUpdate por auto dentro del agua; capacidad de contactos creada al primer paso. | Controller cacheado y contenedores reservados en carga; efectos físicos, fuerzas y límites intactos. |
| Notas: HashSets de contactos locales creados en la primera curva. | Preparados en Start, con capacidad inicial. Se conservan al desactivar/reactivar un trigger. |
| Presupuesto de render: `Camera.allCameras` (array nuevo) y búsqueda de datos URP cada 2 segundos. | Cámaras/datos URP cacheados en Start, esperando explícitamente el rig split completo (no solo su execution order). Los mismos ajustes periódicos recorren referencias existentes. |
| Bot: primera preparación del recorrido y posible búsqueda global desde FixedUpdate. | Ruta preparada en Start/configuración explícita. No cambia el algoritmo de manejo ni los valores del perfil. |
| Flecha de copiloto: lista nueva al reconstruir su malla UI. | Buffer de 34 puntos reutilizado. |
| Etiquetas de dispositivos de selección 2P concatenadas todos los Update. | Literales por jugador/dispositivo; sigue detectándose hotplug sin cambiar asignaciones. |
| Librería de audio cargada repetidamente por cada fuente/ajuste. | Una referencia compartida, con reset al iniciar Play sin Domain Reload. |

Ranking ya utilizaba arrays/segmentos cacheados y cálculo a 10 Hz: no se reescribió. Bots, vegetación y público ya estaban colocados en escena o creados al iniciar, no aparecían mediante instanciaciones durante una vuelta. Polvo/humo/salpicaduras ya reutilizaban ParticleSystems; la preparación de carga adicional adelanta su primer uso y recursos gráficos/audio.

No hay `Resources.Load`, `GameObject.Find` ni `GetComponent` en los loops normales de Update/FixedUpdate/LateUpdate de HUD, reloj, recuperación, ranking, polvo/audio o presupuesto de render. Las consultas en carga/configuración y callbacks de entrada para un eventual auto nuevo son intencionales. Las escenas actuales no cambian el roster durante una vuelta; un futuro spawner debe enlazar explícitamente las referencias de efectos.

## Prueba aislada de asignaciones

`Tools/RallyStutterAllocationCheck.cs`, entrada `Run("before")` / `Run("after")`, crea una escena preview descartable y mide 2000 llamadas después de calentar los métodos. Usa `GC.Alloc` limitado al hilo actual, con profiler activado y control positivo de una asignación por llamada. No mide FPS ni bytes; no incluye la reconstrucción/renderización posterior de las mallas TMP.

| Caso (Editor) | Antes, asignaciones/call | Después |
| --- | ---: | ---: |
| HUD, tiempo y velocidad cambiando a 60 Hz | 6 | 3 |
| HUD, valores visibles sin cambios | 0 | 0 |
| Getter FormattedTime sin cambios | 3 | 0 |
| Getter NoteLabel sin cambios | 2 | 0 |

Ambas ejecuciones terminaron PASS. El TMP instalado genera un char[] y un string para el Inspector bajo `#if UNITY_EDITOR`, incluso con SetText numérico; por eso Editor no alcanza cero en textos que cambian. Esa rama no existe en Windows. No se modificó el paquete TMP para eliminarla. El formato numérico y el límite de 30 Hz evitan los strings de aplicación y reducen trabajo de UI.

`GC.GetAllocatedBytesForCurrentThread()` devolvía cero en este Mono, incluso con asignaciones: no se usaron esas cifras como evidencia. La medición Windows de frames/cargas y warmup se documenta aparte; una reducción de asignaciones no demuestra por sí sola que todos los picos sean GC.

## Validación de integración

- Compilación runtime/editor completada con **0 errores y 0 advertencias** después de las cachés y la preparación de carga.
- `Logs/ranking-performance-check.txt`: **COMPLETE: PASS**, progreso ordenado, sobrepasos, teletransporte/reset y cálculo compartido a 10 Hz en los tres circuitos.
- `Logs/vehicle-recovery-test.txt`: **COMPLETE: PASS**, controles de teclado/gamepad, propiedad local del aviso, recuperación sin mover al checkpoint ni perder progreso y bots sin cambios. El test ahora compara el aviso con `RallyLocalKeyboard.RecoveryLabel`, en lugar de asumir que la etiqueta vigente era solamente M; los bindings no se modificaron.
- La regresión de pantalla dividida espera que termine el gate de carga antes de enviar controles en cada entrada a carrera. `Logs/split-screen-three-circuits-stutter.txt` conserva los casos con dos gamepads: los tres circuitos pasaron estructura, HUDs, motores, freno, recuperación, checkpoints, relojes, pausa y resultados independientes. Esa ejecución completa no terminó PASS porque falló después, en la aserción de un gamepad + teclado. Tras corregir la inicialización y aprobar el gate, `Tools/RallySplitFallbackCheck.cs` volvió a ejecutar el caso acotado y terminó **COMPLETE: PASS** en `Logs/split-fallback-test.txt`, incluida selección por teclado, conducción independiente y reconexión. No presentarlo como un rerun completo de toda la suite, ni como prueba con hardware físico.
- `Logs/race-loading-check.txt`: **COMPLETE: PASS** del rerun reforzado en los seis casos C01/C02/C03, 1P/2P. Comprueba 4/2 vehículos con sus referencias correctas, 1/2 cámaras cacheadas, física/timers inmóviles durante preparación y clips listos. La preparación exige eventos reales de render de ambas cámaras, no solo avanzar frames de corutina. El chequeo detectó la caché prematura de un solo auto en split; los guards explícitos de Start y el frame de asentamiento del loader la corrigieron, sin scans durante conducción. Las mediciones de Windows se documentan en `Stutters_Windows_2026-10-10.md`.

No afirmar ausencia total de stutters en otras GPU, resoluciones o máquinas sin medirlas. La carga y las cachés eliminan/reubican fuentes verificadas de trabajo tardío; no demuestran por sí solas la causa de cada pico reportado por el usuario.
