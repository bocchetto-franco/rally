# Revisión de cierre — 24 de octubre de 2026

## Correcciones y alcance

- `RallyGameSession.FormatTime`: redondea una sola vez a milisegundos antes de separar minutos/segundos. Evita `00:60.000`, coincide con el HUD y rechaza tiempos no finitos/fuera de rango.
- `RallyMenuNavigation`: las páginas compartidas aceptan el mando actualmente utilizado, incluido J2 si se desconecta J1. Los paneles individuales y la pausa con dueño mantienen su emparejamiento exclusivo.
- `RallyCheckpointManager`: el reset conserva progreso/rumbo y obtiene la altura de la superficie transitable en el centro del checkpoint. No reutiliza la altura del auto al cruzar desde una banquina baja o en un salto. Solo considera los colliders de camino/puente conocidos; si no encuentra apoyo, conserva la pose segura anterior. Usa la geometría de ruedas/suspensión existente, sin modificarla.
- `RallyRacePositions`: entre jugadores con cronómetro, un piloto que terminó conserva su resultado delante de otro sin terminar; dos tiempos finales se ordenan por tiempo, con empate estable. La comparación contra bots conserva el progreso geométrico previo.
- `RallySplitScreenVerification`: el verificador de Editor deja de escribir su heartbeat cada segundo cuando no hay prueba activa/restauración.
- TMP: se actualizó exclusivamente el pragma obsoleto de símbolos de depuración en los dos shaders móviles. No se reemplazó su renderizado.
- Se activó el prebake de colisión triangular en seis mallas embebidas que ya usaban MeshCollider (dos por circuito). `Tools/VerifyFinalMeshBake.ps1` verifica byte por byte que ningún otro dato de las tres escenas cambió.
- El verificador `RallyCheckpointLapCheck` ahora espera a que termine la precarga antes de apagar cámaras para su recorrido cinemático. El primer intento de esta auditoría fue inválido por ese conflicto del fixture y se conserva en `Logs/FinalAudit/checkpoint-fixture-old-failure.txt`.

Se conservaron el tuning de jugador/bots, geometría, ubicación/orden de checkpoints, waypoints, turbos, condiciones atmosféricas y presupuesto visual de carrera. No se descargaron assets, no se actualizaron paquetes y no se publicaron commits.

## Optimización comprobada de memoria/carga

`RallyMenuAssetLifetime` coordina la limpieza de assets entre los dos estudios de autos y el paisaje del menú. Al cambiar de pista se conserva la última imagen del fondo, se destruye el paisaje anterior y se libera lo no utilizado antes de integrar otro Terrain. Las cargas/instanciaciones pendientes se registran incluso si el panel desaparece. Una limpieza compartida con espera de inactividad evita que cada vista lance su propia operación durante otra carga.

La limpieza pendiente del menú se consume bajo la pantalla de carga de carrera. La carrera precarga el audio de los vehículos realmente instanciados, sin retener por defecto los tres prefabs seleccionables, incluidos los no usados en 2P. El audio de derrape evita dos consultas adicionales de contacto cuando la detección compartida indica que no hay derrape.

No se redujo resolución de texturas, distancia de dibujado, sombras, LOD ni calidad de materiales en esta revisión. HUD, ranking y minimapa ya tienen caché y cadencias limitadas; no se reescribieron por suposición.

Prueba de residencia del menú (mismo Editor/DX11; valores nativos, incluyen overhead del Editor; no equivalen a RAM total del proceso):

| Estado asentado | Antes | Después |
|---|---:|---:|
| TerrainData tras recorrer las tres pistas | 3 | 1 |
| Bosque: memoria nativa asignada | 1.499 MB | 859 MB |
| Bosque: texturas residentes | 104 MB | 40 MB |
| Sierras al final de la secuencia: memoria nativa | 1.500 MB | 1.368 MB |
| Sierras al final: texturas residentes | 104 MB | 51 MB |

MB decimales, redondeados. La reserva del allocator de Unity no se devuelve inmediatamente al sistema; no se confunde memoria reservada con asignada. El control de limpieza explícita posterior confirma que el menú optimizado ya había desalojado el paisaje anterior. Informes completos: `Logs/FinalAudit/menu-memory-before.txt` y `menu-memory-after.txt`.

## Verificaciones

- `time-format.txt`: PASS, límites de minuto/hora, paridad con HUD, culturas es-AR/en-US y valores inválidos.
- `menu-ownership.txt`: PASS, emparejamiento J1/J2, mando compartido, desconexión/reemplazo, fallback de teclado. Prueba virtual, no hardware físico.
- `menu-navigation.txt`: PASS + RESTORED, inputs virtuales en Play, confirmación de autos, navegación entre paneles, selección 2P individual y una sola fila de pista compartida, créditos/previsualización.
- `race-state-before.txt` reprodujo reset bajo el suelo y orden final incorrecto. `race-state-after.txt`: PASS; incluye preservación del progreso contra bots.
- `race-loading.txt`: PASS en C01/C02/C03 × 1P/2P. Físicas y timer congelados bajo la carga, audio listo, roster/cámaras correctos, cuatro autos en 1P y solo dos en 2P.
- `checkpoint-laps.txt`: COMPLETE: PASS en nueve casos con triggers físicos reales siguiendo el recorrido cinemáticamente: centro, margen +9 m y 2P centro/-9 m, en cada circuito. Contó 10/10/11 puertas, detuvo el tiempo, deshabilitó el control y mostró resultados en pausa. Los nombres Porsche/BMW de ese informe son etiquetas históricas del fixture: sus índices corresponden a los modelos actuales Lancia/Peugeot; no se reinstalaron modelos retirados. No son vueltas conducidas por humanos ni pruebas de manejo autónomo de bots.
- `turbo-physics.txt`: COMPLETE: PASS + RESTORED. Las nueve zonas activan en su franja ampliada, rechazan borde/reversa/inmovilidad/pausa/control terminado, no acumulan boosts y caducan; hubo incremento de velocidad medido respecto a una pasada de control sin zona. Incluye bots y activación independiente J1/J2. El helper de teletransporte del fixture sincroniza también el Transform interpolado con el Rigidbody.
- `mesh-preservation.txt`: PASS, únicamente seis flags de prebake en las tres escenas.

Los archivos temporales de Editor usados para las mediciones se archivaron fuera de Assets en `Logs/FinalAudit/EditorHelpers`; las entradas reproducibles permanecen en `Tools/RallyFinal*.cs`. No forman parte del Player.

## Medición de Windows

Referencia: ejecutable del 22 de octubre; seis capturas `Before_*_Final24.csv`, 25 s de conducción automatizada por caso, 1280×720, DX11/AMD Radeon integrada, límite diagnóstico de 120 fps, misma semilla. Se separa la carga inicial de la carrera; no son vueltas completas ni representan todas las zonas del circuito. Capturas PNG del framebuffer y foco registrado en los CSV. El contador de tiempo de frame es utilizable; varios contadores de GPU/draw calls/GC no están disponibles en Release y no se utilizan para atribuir un cuello de botella.

El build nuevo completó las seis capturas automáticas (salida 0, todas con foco registrado, sin excepciones de juego en sus logs). Se revisaron los PNG escritos por el propio juego: circuito/HUD, velocidad, lluvia/fog y dos vistas sin bots cuando corresponde. El control nativo de Windows recibió Escape y se detuvo; no se enviaron más acciones visuales ni se realizó una sesión manual adicional del menú en el ejecutable. La navegación del menú sí fue verificada en Play del Editor.

| Circuito | Modo | FPS medios antes → después | P95 de frame antes → después |
|---|---|---:|---:|
| Desierto | 1P | 117,1 → 119,1 | 10,01 → 9,42 ms |
| Desierto | 2P | 119,1 → 119,6 | 9,37 → 9,25 ms |
| Sierras | 1P | 95,8 → 105,0 | 13,82 → 12,08 ms |
| Sierras | 2P | 69,6 → 79,4 | 19,01 → 16,40 ms |
| Bosque | 1P | 119,5 → 119,5 | 9,52 → 9,39 ms |
| Bosque | 2P | referencia anterior no comparable por pérdida de foco → 119,6 | anterior no comparable → 9,23 ms |

La referencia C03/2P registró 1.770 frames sin foco; se conserva como evidencia, pero se excluye de comparaciones estrictas. Los demás casos no tuvieron frames sin foco. Son capturas orientativas de una ejecución por versión, con límite de 120 fps, no un benchmark estadístico repetido ni pruebas de todo el trazado. La física y las colisiones pueden producir trayectorias ligeramente diferentes. No se atribuye todo el cambio de FPS a una única corrección ni se promete ese porcentaje en otra máquina. Se conserva un frame >50 ms por captura posterior; no se afirma haber eliminado cualquier stutter posible. Datos completos: `frame-summary.csv/json/md`, generados por `Tools/AnalyzeFinalAudit.py`.

## Build y comprobación del artefacto

- **`C:\Users\franc\Downloads\rally\Build\Windows\Rally.exe`**; Windows x64 Release/Mono, Unity 6000.6.0f1, producto Rally1, versión 0.1.0. Entrada MainMenu, seis escenas guardadas incluidas, input Both.
- Finalizó el 24/10/2026 a las 21:05:22 de Argentina: **Succeeded, 0 errores, 1 advertencia**, 3 min 45 s; 642.581.561 bytes reportados. `BuildLog.txt` y `BuildInfo.txt` están junto al EXE; log de Unity en `Logs/FinalAudit/WindowsBuild.log`.
- Único aviso de build: `Pipeline: No RuntimePipelineConfig asset found ... Pipeline will be disabled in Player builds.` Es el servidor de automatización del paquete, no URP ni un error de juego. No se habilitó un servidor remoto en el Player para esconder el aviso. Desaparecieron los avisos previos de TMP y prebake de las seis mallas. Sin errores/warnings de C# en la compilación final.
- El assembly del Player contiene `RallyMenuAssetLifetime`; no contiene los helpers temporales de la auditoría. No se distribuye `Assembly-CSharp-Editor.dll` ni `Unity.Pipeline.Editor.dll`.
- SHA-256 `Assembly-CSharp.dll`: `C39D4EA18D965F2AD41BA79CCB85C714BF14AF66F78C0CF63680F770C52203D2`.
- SHA-256 `Rally_Data/data.unity3d`: `BBA8B3C0C9FD84D8AA9DC7A0990B5BF3BB5894852F71051E1682E8F55C28A3F0`.
- Hashes anteriores/posteriores en `build-before-hashes.csv` y `build-after-hashes.csv`. El EXE es el launcher estándar de Unity y puede conservar su hash aunque cambie el juego; la actualización se verifica también con los assemblies y datos.
- Copia recuperable íntegra anterior: `Build/Windows_Previous_20261024_FinalAudit/`. El acceso directo `Rally.lnk` del Escritorio ya apunta al destino actual y no necesitó reemplazarse. No separar el EXE de Rally_Data, UnityPlayer.dll y archivos vecinos.
- Todas las pruebas restauraron preferencias/dispositivos/escenas. Unity quedó cerrado después de salir limpiamente de las pruebas; el build se generó en batch mode y los seis procesos de captura terminaron. No se publicó un commit ni se subió el binario a GitHub.

## Límites conocidos

No se puede garantizar ausencia de cualquier bug o stutter en todo hardware. El alcance probado se documenta por separado del auditado. Los fallos iniciales de herramientas/fixtures no se presentan como regresiones del juego ni como pruebas aprobadas.

Los logs del ejecutable anterior contienen avisos de URP sobre passes de profundidad de campo/Panini que fueron eliminados por stripping; las cámaras del juego tienen postprocesado desactivado. No se agregaron efectos ni se desactivó el stripping para ocultarlos. La instrumentación Release ahora evita intentar habilitar un Profiler que ese tipo de build no soporta.
