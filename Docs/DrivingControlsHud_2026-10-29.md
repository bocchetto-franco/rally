# Guía pequeña de controles en carrera

La guía se crea en runtime desde `RallyRaceHud`, a la derecha del minimapa de cada jugador. Tres filas muestran Frenar / Acelerar, Turbo y Derrapar con los botones actuales. No cambia el input.

| Dispositivo | Frenar / Acelerar | Turbo | Derrapar |
| --- | --- | --- | --- |
| DualSense / PlayStation | L2 / R2 | Triángulo | Cuadrado |
| Xbox / gamepad genérico | LT / RT | Y | X |

Los símbolos se dibujan con geometría UI y los textos reutilizan la fuente del velocímetro. Fondo oscuro con alpha 0,68; tamaño 292×124 unidades del Canvas en 1P, 274×116 en 2P. Queda abajo a la izquierda, junto al mapa y fuera de los avisos superiores y del inventario central de turbo.

En modo local cada guía consulta exclusivamente su `RallyLocalPlayerInput`. Quien juega con teclado no ve la guía. En 1P sin componente local, usar las teclas de conducción oculta la guía aunque haya un mando conectado; volver al mando la muestra. No se consumen eventos de pulsación ni se cambia el emparejamiento. No hay búsquedas/creación de UI por frame y solo se cambia el texto al cambiar de mando.

Archivos de runtime: `Assets/Scripts/RallyDrivingControlsHud.cs`, `RallyDrivingControlGlyph.cs` y dos líneas de instalación en `RallyRaceHud.cs`. Ningún archivo de escena ni perfil de auto necesita regenerarse. El ejecutable existente no se modifica: necesita un build nuevo.

## Validación reproducible

`Assets/Editor/RallyDrivingControlsHudCheck.cs`, menú **Tools / Rally / Verify Driving Controls Guide**. Ejecuta una prueba aislada con Canvas y mandos virtuales, sin cargar/guardar circuitos. Comprueba etiquetas PlayStation/Xbox, ausencia de duplicados/recortes, panel fuera del minimapa, visibilidad por dueño, pausa, desconexión y alternancia teclado/mando sin consumir input. Informe y tres capturas: `Logs/DrivingControlsHud/`.

No equivale a prueba con hardware real, vuelta conducida ni benchmark. Exigir `COMPLETE: PASS` en el informe para afirmar éxito.

## Resultado actual

El informe `Logs/DrivingControlsHud/check.txt` terminó en **COMPLETE: PASS** en la pasada final: DualSense y gamepad genérico virtuales, etiquetas/símbolos, no duplicación, límites de panel/textos, ausencia de raycasts, pausa, desconexión y alternancia teclado/mando. La compilación de Unity terminó sin errores.

La captura inicial en PreviewScene no mostraba el Canvas; se corrigió solo el fixture para renderizar una escena UI temporal normal, sin guardar escenas. El intento intermedio de crearla aditivamente con la escena inicial sin guardar fue rechazado por Unity antes de la prueba; batch usa ahora una escena temporal Single y la ejecución interactiva conserva el modo aditivo. La última pasada exige contenido gráfico no vacío y terminó con salida 0 en `Logs/driving-controls-visual-editor.log`.

Las tres imágenes finales **DualSense_1P.png**, **Xbox_1P.png** y **MixedPads_2P.png** fueron revisadas: iconos visibles, textos sin recortes, panel separado del marco que representa el minimapa y uno por viewport con sus botones correctos. Son capturas de un Canvas aislado, no de una vuelta del juego ni prueba con mandos físicos. Se restauran preferencias, timeScale y dispositivos temporales. No se modificaron escenas de circuitos, asignaciones de botones ni tuning.
