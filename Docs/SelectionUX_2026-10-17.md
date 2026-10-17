# Selección y HUD — 2026-10-17

- Navegar por autos solo previsualiza. Cruz/A, Enter o clic confirma el auto: su botón permanece amarillo, independientemente del borde de foco. Solo hay uno por panel; en 2P cada jugador conserva su elección independiente y su propio foco.
- `RallyMenuNavigation` opta a navegación espacial únicamente en selección. Arriba/abajo busca controles debajo/encima en lugar de recorrer la lista de creación; izquierda/derecha conserva el ciclo de autos y se mueve dentro de las otras filas. Los paneles 2P declaran filas explícitas: autos → Listo → pistas → jugadores/acciones (estas últimas, solo J1). Otros menús y sliders conservan su navegación anterior y repetición/deadzone.
- Etiquetas públicas de circuitos: `Desierto`, `Sierras`, `Bosque`. `CircuitNames`, IDs de escena y PlayerPrefs no cambian. `CircuitDisplayNames` es solo presentación, incluidos título de pista, menú y resultados del frontend.
- No se crean el contador de autos, la ficha «FÍSICA COMPARTIDA…» ni la línea abreviada de atribución en selección. Se conservan modelos giratorios, nombres de auto, fade y carga bajo demanda. Los créditos completos de BMW y Audi permanecen en `CREDITS.md`, su copia distribuida y la página de créditos.
- `RallyRaceHud.PrepareTimingPanel` oculta únicamente el GameObject del texto de checkpoints; mantiene su referencia y usa un panel de tiempo de 82 px de alto. Se retira la actualización del texto invisible. No se alteran gates, managers, timer, ranking, física, tuning ni escenas de circuitos.

## Verificación reproducible

Unity idle, fuera de Play: `Tools/RallySelectionUxCheck.cs`, entrada `RallySelectionUxCheck.Run` mediante Unity Pipeline. Usa dos layouts virtuales `DualSenseGamepadHID` y teclado virtual. Comprueba confirmación separada del foco, amarillo persistente renderizado, arriba/abajo con D-Pad/stick/flechas, filas horizontales, propiedad de cada panel 2P, mapeo de nombres a escenas, limpieza y créditos conservados. Espera a que los previews terminen de cargar antes de capturar selección.

Luego recorre los tres circuitos en 1P/2P: texto de checkpoints inactivo, tiempo visible, conteo de salida y reloj avanzando. Esta parte prueba el despacho del manager con un collider de auto existente y congela los vehículos en la copia de Play; no es una vuelta conducida ni una prueba física de todos los gates. Restaura preferencias, dispositivos/InputSettings, escenas y estado de ejecución. No guarda escenas.

Informe: `Logs/selection-ux-check.txt`; exigir `COMPLETE: PASS` y `RESTORED` actuales. Capturas: `Logs/SelectionUX_1P.png`, `_2P.png`, `SelectionUX_HUD_Circuit_0X_1P.png` / `_2P.png`. No equivale a mandos físicos ni a un ejecutable Windows actualizado.

Validación final 2026-10-17: **COMPLETE: PASS**, **RESTORED**; selección 1P/2P con dos layouts DualSense virtuales, teclado y pointer, modelos/nombres visibles después de cargar y seis casos de HUD. Las capturas de selección y HUD se revisaron. Comprobación posterior: Unity fuera de Play, sin compilación pendiente y con InputSettings válido. El fixture espera a que termine la salida de Play antes de restaurar y conserva JSON de los ajustes transitorios de input para recuperarlos si Unity los retira. No se modificaron escenas ni físicas; el ejecutable anterior no se regeneró.
