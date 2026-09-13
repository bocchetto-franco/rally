# Rally — instrucciones para agentes

## Contexto del proyecto

- Este repositorio contiene un videojuego de rally desarrollado con Unity `6000.6.0f1`.
- El pipeline de render del proyecto es Universal Render Pipeline (URP).
- La escena principal de desarrollo es `Assets/JS Vehicle Physics Controller/Scene AMR/PC Controller Scene AMR 01.unity`.

## Reglas de trabajo

- El auto principal utiliza el asset de físicas ubicado en `Assets/JS Vehicle Physics Controller/` (referido también como `JS_Vehicle_Physics_Controller`). Reutilizar y configurar ese sistema; no reescribir las físicas del vehículo desde cero.
- Nunca modificar ni borrar el auto de policía original incluido dentro de la carpeta del asset. Para hacer pruebas o variantes, desactivarlo o duplicarlo.
- `Active Input Handling` está configurado como `Both` en Player Settings. No cambiar esta configuración.
- Mantener URP como pipeline de render y usar shaders y materiales compatibles con URP.

## Estructura actual de `Assets`

- `Assets/auto/`: modelo, materiales y texturas del Porsche; también contiene utilidades para vincularlo al sistema de vehículo.
- `Assets/Editor/`: herramientas de editor usadas para crear y configurar circuitos, vehículos y checkpoints.
- `Assets/JS Vehicle Physics Controller/`: asset principal de físicas del vehículo, con modelos, audio, materiales, postprocesado, prefabs, escenas, scripts, skyboxes y UI. No alterar el auto de policía original.
- `Assets/Scenes/`: escenas propias del proyecto, incluidos `Circuit_Test_01` y `Circuit_01`, junto con materiales asociados.
- `Assets/Scripts/`: scripts propios del juego, actualmente incluido el sistema de checkpoints y cronómetro.
- `Assets/Settings/`: configuración de URP, renderers, perfiles de volumen y ajustes globales de render.
- `Assets/TutorialInfo/`: recursos y archivos informativos generados por la plantilla/tutorial de Unity.
