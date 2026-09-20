# Rally — instrucciones para agentes

## Contexto del proyecto

- Este repositorio contiene un videojuego de rally desarrollado con Unity `6000.6.0f1`.
- El pipeline de render es Universal Render Pipeline (URP).
- El auto principal es el Porsche de `Assets/auto/`. El auto de policía original permanece desactivado en la escena como referencia y alternativa de recuperación.
- La escena de desarrollo para probar el auto es `Assets/JS Vehicle Physics Controller/Scene AMR/PC Controller Scene AMR 01.unity`.
- La primera pista jugable es `Assets/Scenes/Circuit_01.unity`.
- `Active Input Handling` está configurado como `Both` en Player Settings; no cambiarlo.

## Flujo de escenas y carrera

- Ya existe un frontend funcional antes de la carrera. Al entrar en Play, el proyecto debe comenzar en `Assets/Scenes/MainMenu.unity` mediante `EditorSceneManager.playModeStartScene`.
- El botón **Jugar** de `MainMenu` abre `Assets/Scenes/VehicleCircuitSelection.unity`.
- La selección actual ofrece un único auto, **Porsche 911 SC Rally**, y un único circuito, **Circuit 01**. `RallyGameSession` conserva ambas selecciones en PlayerPrefs (`Rally.SelectedVehicle` y `Rally.SelectedCircuit`).
- El botón para iniciar la carrera guarda la selección y carga `Assets/Scenes/Circuit_01.unity`.
- En `Circuit_01`, el GameObject `Race Flow` usa `RallyRaceFlow`: espera que `RallyCheckpointManager.IsFinished` indique el final, guarda el tiempo y carga `Assets/Scenes/RaceResults.unity` después de una pausa breve.
- Desde resultados se puede repetir la carrera, volver a selección o regresar al menú principal.
- El orden esperado en Build Settings es: `MainMenu`, `VehicleCircuitSelection`, `Circuit_01`, `RaceResults`.
- La configuración y reparación de este flujo se centraliza en `Assets/Editor/RallyFrontendSetup.cs` y su comportamiento en `Assets/Scripts/RallyMenuController.cs`. No cambiar los nombres de escena sin actualizar ambas partes.

## Estado de `Circuit_01`

- Es un loop cerrado de rallycross con curvas abiertas y cerradas, al menos dos horquillas y varios tramos con desnivel.
- Tiene checkpoints y cronómetro funcionales, HUD de carrera y reinicio del auto al último checkpoint con la tecla `R`.
- Hay cuatro zonas de charcos visibles. Cada una conserva su trigger y la lógica de frenado o pérdida temporal de velocidad; no mover ni redimensionar estas zonas al cambiar solamente su aspecto visual.
- Los fardos de heno tienen Collider y Rigidbody dinámico para reaccionar a los impactos del auto.
- La ambientación incorporada por Astra incluye Terrain montañoso/desértico, materiales de suelo árido, vegetación seca dispersa y rocas.
- La documentación solicitada menciona espectadores o público en algunas curvas, pero la auditoría actual del repositorio no encontró GameObjects ni assets de público identificables por nombre. Verificarlo visualmente en Unity antes de asumir que esa parte de la ambientación está presente o antes de eliminar objetos aparentemente relacionados.

## Estado de optimización

- Vegetación de `Circuit_01`: 80 árboles Quiver, 850 Searsia Lucida, 180 arbustos rooibos y 195 pastos. Arbustos y pastos usan Terrain Details con `alignToGround = 1`; los árboles siguen la normal del Terrain.
- `Circuit01VegetationRepair.cs` repara/revalida las plantaciones y aumenta densidad sin tocar gameplay. Conservar la corrección de ejes del FBX al hornear mallas (Quiver tiene rotación raíz de -90° en X); no cancelarla con `worldToLocalMatrix` de la raíz. Comprobar el volumen de la planta y el jitter de Terrain Details contra el camino, no solo su centro.

- Ya se aplicaron manualmente Half Res en texturas, menor distancia de sombras, menor Far Clip Plane y Occlusion Culling baked.
- En la optimización más reciente, aproximadamente 292 colocaciones superiores de vegetación (195 pastos y 97 arbustos, que expandían a unos 4092 GameObjects) se migraron a Terrain Details. Se conservaron 73 colocaciones de rocas como GameObjects.
- Los Terrain Details quedaron con resolución `1024`, patch resolution `32`, distancia de dibujo `180 m` y densidad `1`.
- Se desactivó la feature SSAO del renderer de PC (`Assets/Settings/PC_Renderer.asset`). `Circuit_01` no tiene Volumes de postprocesado y su cámara no usa postprocesado.
- Solo la luz direccional principal queda activa en tiempo real; las luces auxiliares encontradas en vehículos están desactivadas.
- Se auditaron 32 texturas de ambiente y ninguna supera 2048 px. Se limitaron a 1024 px y con compresión las texturas de pasto y cinco texturas del sample de agua (`FlipbookTest`, `foam_detail_tiling`, `foam_mask`, `ocean_foam_blend_ramp`, `puddle_norm`).
- `Assets/Editor/Circuit01PerformanceSetup.cs` contiene la automatización de optimización y `Assets/Editor/Circuit01EnvironmentSetup.cs` vuelve a convertir la vegetación a Terrain Details si se reconstruye el ambiente.
- La revisión e incorporación de LOD adicionales sigue siendo trabajo de optimización en curso; no afirmar que está terminada sin volver a medir la escena.

## Reglas de trabajo

- Cada vez que se cree o actualice una pista, dejar el Porsche principal activo al inicio, orientado hacia el recorrido y apoyado correctamente sobre el camino, con controles y cámara de seguimiento funcionando. Verificar su posición antes de guardar la escena; no dejar pistas de prueba sin auto salvo pedido explícito.
- El auto principal utiliza el asset de físicas de `Assets/JS Vehicle Physics Controller/`. Reutilizar y configurar ese sistema; no reescribir las físicas desde cero.
- Los cuatro WheelColliders y la física del Porsche fueron afinados manualmente durante muchas iteraciones para un feeling específico de rally arcade: alto derrape lateral trasero, `Mass` ajustada, `Angular Damping` aumentado, mayor `Steer Angle`, respuesta de dirección más rápida y curvas de `Sideways Friction` y `Forward Friction` personalizadas.
- No revertir ni modificar `Mass`, `Angular Damping`, `Sideways Friction`, `Forward Friction`, `Steer Angle` o la velocidad de respuesta del volante sin una petición explícita del usuario.
- Nunca modificar ni borrar el auto de policía original incluido dentro de la carpeta del asset. Para hacer pruebas o variantes, desactivarlo o duplicarlo.
- Mantener URP y usar shaders y materiales compatibles con URP.
- No romper la geometría, los checkpoints, los triggers de charcos ni la física de los fardos al trabajar únicamente sobre arte o rendimiento.

## Efectos de conducción implementados

- El Porsche tiene downforce cuadrático fuerte que comienza a `65 km/h`, usa coeficiente `3.2` y está limitado a `4500 N`. Se aplica en el centro de masa y solo cuando ambos ejes tienen contacto con el suelo, para evitar torque artificial o levantar un eje.
- La frenada de servicio se activa con `S` o flecha abajo mientras el auto avanza y usa `4200 Nm` por rueda delantera y `1800 Nm` por rueda trasera, equivalentes a un reparto aproximado 70/30. Incluye modulación por `forwardSlip` para reducir bloqueos; Espacio sigue siendo el freno de mano trasero.
- Para retrasar el trompo sin quitar el derrape normal, `Angular Damping` quedó en `3.0`, el `Asymptote Slip` lateral trasero en `0.72`, y la fricción longitudinal trasera en `1.05` de Extremum Value y `0.76` de Asymptote Value.
- Las dos ruedas traseras reutilizan los Particle Systems y el material de polvo incluidos en el asset. Cada emisor se activa independientemente al superar `0.18` de `sidewaysSlip`, se apaga por debajo de `0.12` y requiere al menos `25 km/h`.
- El humo es solamente visual: no modifica las curvas de fricción ni aplica fuerzas al auto.

## Pendientes conocidos

- Continuar midiendo rendimiento y aplicar LOD donde aporte una mejora comprobable sin una pérdida visual notoria.

## Estructura actual de `Assets`

- `Assets/auto/`: modelo, materiales y texturas del Porsche; también contiene utilidades para vincularlo al sistema de vehículo.
- `Assets/Art/Environment/`: assets ambientales, texturas, materiales y prefabs usados en el paisaje árido.
- `Assets/Editor/`: herramientas de configuración del frontend, circuito, ambiente y optimización.
- `Assets/JS Vehicle Physics Controller/`: asset principal de físicas del vehículo, con modelos, audio, materiales, postprocesado, prefabs, escenas, scripts, skyboxes y UI. No alterar el auto de policía original.
- `Assets/Scenes/`: escenas propias, incluidas `MainMenu`, `VehicleCircuitSelection`, `Circuit_01`, `RaceResults` y escenas auxiliares como `Circuit_Test_01`.
- `Assets/Scripts/`: lógica propia de menú y selección, flujo de carrera, checkpoints, timer, HUD, reinicio, charcos y comportamiento adicional del vehículo.
- `Assets/Settings/`: configuración de URP, renderers, perfiles de volumen y ajustes globales de render.
- `Assets/TutorialInfo/`: recursos y archivos informativos generados por la plantilla o tutorial de Unity.
