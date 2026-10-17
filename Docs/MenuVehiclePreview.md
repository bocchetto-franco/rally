# Previsualización 3D de autos

La selección muestra un estudio 3D neutro por jugador: cámara independiente, plataforma gris e iluminación sin sombras ni post-procesado. El modelo gira 12°/s sobre Y, usando tiempo no escalado. Una transición de 0.22 s de salida y entrada suaviza el cambio; solicitudes rápidas se agrupan durante 0.12 s.

`RallyMenuVehiclePreview` carga solamente el prefab visual solicitado con `Resources.LoadAsync`, destruye el modelo anterior antes de instanciar el siguiente y libera recursos no usados una vez estabilizados ambos paneles. No precarga ni conserva una colección de los tres autos. En 1P hay una vista; en 2P, una por piloto, con layers 30 y 29, separados del fondo de circuito (31). Se liberan cámaras, estudio y RenderTextures al salir o reconstruir la interfaz.

Resolución máxima de la vista 768 px en 1P y 640 px por panel en 2P, con altura proporcional al marco. Sin AudioListener, físicas, input de conducción o componentes de gameplay. No modifica calidad global ni escenas/tuning de carrera.

## Recursos y reconstrucción

- `Assets/Resources/MenuVehicles/Porsche911.prefab`: geometría del Porsche original de `Assets/Prefabs/Bot_Car.prefab`, sin scripts ni física.
- `Assets/Resources/MenuVehicles/BmwE30.prefab`: geometría de `Assets/Resources/Vehicles/BmwE30.prefab`.
- `Assets/Resources/MenuVehicles/AudiQuattro.prefab`: geometría de `Assets/Resources/Vehicles/AudiQuattro.prefab`.
- `Assets/Art/MenuVehicles/`: mallas derivadas combinadas por material, conservando todas las submallas, normales y UV originales.
- `Assets/Resources/MenuVehicles/StudioFloor.mat`: material URP Unlit de la plataforma; incluido en Resources para conservar el shader en builds.

Exportación explícita con Unity idle: `unity command run_script --file Tools/RallyMenuVehiclePreviewSetup.cs --entry RallyMenuVehiclePreviewSetup.Build --json`. Los modelos/materiales originales no se modifican. Conservar sus créditos y licencias; las vistas derivadas no cambian la licencia del contenido. La fuente/licencia del Porsche original sigue sin verificar, como se documenta en CREDITS.md.

## Controles

Arriba/abajo navega entre filas. En selección individual, Derecha recorre Porsche → BMW → Audi y solo desde Audi pasa a terreno; Izquierda desde terreno vuelve, recordando el foco. Stick, D-Pad y flechas comparten esa regla; L2/R2 no cambian panel. En 2P izquierda/derecha recorre la fila de autos de cada piloto. Mouse conserva selección directa. Navegar solo previsualiza y Cruz/Enter/Espacio confirma según los controles de cada piloto, conservando el sistema Listo existente. El verificador específico de paneles es `Tools/RallySelectionUxCheck.cs`, entrada `RunMenusOnly`.

Solo se muestra el nombre: la ficha de peso/física se retiró por pedido explícito del 2026-10-17. El preview no aplica ningún valor a la física.

## Verificación

`Tools/RallyMenuVehiclePreviewCheck.cs`, entrada `Run`: prueba Play con teclado/gamepads virtuales, los tres modelos, giro y fade, cambios rápidos, descarga de prefabs descartados, paneles locales independientes y limpieza al volver al menú. Informe `Logs/menu-vehicle-preview-test.txt`; capturas `Logs/Menu_Car_*.png` y `Menu_Cars_Local.png`. Exigir un informe actual `COMPLETE: PASS`; no equivale a hardware real ni a una medición de FPS en build.

El verificador externo requiere Disable Domain Reload, ya habilitado en este proyecto. Para que funcione aun con Unity minimizado, usa una copia temporal de InputSettings que permite input virtual sin foco; restaura la configuración original, preferencias, dispositivos y escena al terminar. Las capturas se toman antes de enviar el siguiente input, evitando mostrar el nombre nuevo sobre el modelo anterior durante el fade.

Validación del 2026-10-11: exportación y prueba final en Play terminaron en `COMPLETE: PASS`, sin diagnósticos de compilación del verificador. Pasaron los tres modelos, giro, opacidad intermedia del fade, cambios rápidos, teclado, dos mandos exclusivos, fallback de teclado compartido, confirmación independiente y desbloqueo de carrera al marcar ambos Listo. Ningún prefab de previsualización descartado permaneció cargado tras estabilizarse las vistas; cámaras y RenderTextures fueron retirados al salir. Las cuatro capturas finales fueron revisadas: modelos completos, nombres correctos y texto sin superposición. No se regeneró el ejecutable Windows anterior para este cambio.
