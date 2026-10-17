# Mini-mapa de carrera

`RallyRaceHud.Start` instala `RallyMiniMap` una vez que termina la creación del roster local. Funciona en Circuit_01/02/03 sin guardar ni regenerar escenas. Se ubica abajo a la izquierda; en 2P crea una vista por HUD dentro de su `Player Viewport`, con los dos jugadores en ambas vistas.

La forma viene de los hijos ordenados de `AI_Waypoints`, con cierre último→primero. Proyección X/Z vista desde arriba, sin rotación y con escala uniforme para no deformar curvas. `RallyMiniMapRoute` genera una malla UI de línea cerrada; no requiere imágenes, cámaras, RenderTextures ni render del escenario. Una futura modificación dinámica de waypoints requeriría reconstruir explícitamente la ruta.

Los participantes se toman de `RallyVehiclePresentationCache` una sola vez, con referencias a sus Rigidbody. J1 naranja, J2 celeste, bots rojo; marcador propio ligeramente mayor y borde oscuro. Actualiza solamente posiciones de iconos a 20 Hz, sin strings, búsquedas globales ni listas nuevas en el bucle. Los puntos fuera del encuadre se limitan al margen del mapa, sin modificar la posición física. No cambia gameplay, ranking, cronómetro ni tuning.

Presupuesto: panel 246×234 UI en 1P, 186×178 en 2P; fondo oscuro alpha 0.78. Ruta de aproximadamente 112–158 puntos, dos vértices y dos triángulos por segmento; 4 marcadores en 1P, 2 en cada vista local. Textos estáticos y gráficos sin raycasts para no bloquear controles.

Prueba externa con Unity idle: `Tools/RallyMiniMapCheck.cs`, entrada `Run`. Recorre las seis combinaciones C01/C02/C03 1P/2P, comprueba roster, geometría cerrada, movimiento de iconos, colores, límites del viewport y que mover autos no reconstruya la ruta. Informe `Logs/mini-map-check.txt`, capturas `Logs/Minimap_Circuit_0X_1P.png` / `_2P.png`. Restaura preferencias y escenas al terminar; solo afirmar validación final con un informe actual `COMPLETE: PASS` y `RESTORED`. No reemplaza una vuelta conducida ni una medición de FPS.

Validación final del 2026-10-16: los seis casos terminaron **COMPLETE: PASS** y **RESTORED** en Unity 6000.6.0f1. Se verificaron tres bots reales en 1P y ninguno en 2P, marcadores asociados al Rigidbody correcto, colores distintos, cierre de ruta, ausencia de superposición con tiempo/notas de curva, límites de cada viewport y ausencia de reconstrucción de la ruta al mover autos. Las seis capturas se revisaron visualmente. La prueba congela la física y cambia posiciones para aislar la UI: no es una vuelta conducida ni un benchmark. El ejecutable de Windows anterior no se regeneró con este cambio.
