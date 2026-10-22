# Cinco ajustes independientes — 2026-10-21

## Cambios

1. **Fácil solamente.** Un único botón de dificultad; preferencias antiguas Medio/Difícil migran a 0. `RallyBotController` conserva el antiguo Fácil mediante multiplicador de velocidad 0.7225, frenado `Clamp01(base + 0.05) * 0.85`, todos serializados. Mantiene variación 110/118/126 de base entre bots (79.475/85.255/91.035 km/h efectivos). No modifica selección de auto/pista.
2. **Fila 2P compartida.** Un solo propietario `sharedCircuitCard`, fuera de los dos paneles de autos; encabezado `PISTA COMPARTIDA` en lugar de repetir un nombre de pista sobre la fila. Tres botones reales, compartidos por los dos navegadores; confirmar cambia la pista global y no los autos.
3. **Información del inicio.** Tarjeta con `CORRÉ A TU MANERA` y `Rally arcade · 3 circuitos · Grupo B / Competí solo o con otro jugador`. Se conserva el título `RALLY ARCADE`, posición, colores y fuentes. No se modifica el título de la pantalla de selección, que es un bloque diferente.
4. **Polvo y dirección.** `RallyVehicleDust.Update` lee cada frame los ángulos reales del auto dueño. A igual velocidad, giro aumenta emisión y tamaño inicial hasta ×1.20, además del crecimiento existente hasta ×1.65. Escribe `rateOverTime`, `startSize` y `sizeOverLifetime` en runtime; no agrega emisores ni shaders. Mantiene límites, contacto con camino y apagado por distancia/pausa/aire/vuelco. Máximo de dirección del jugador 44→46°, respuesta 60→66, serializados en las tres escenas y defaults del rig compartido por los tres modelos. Masa, fricción, downforce, asistencia y física de bot intactos.
5. **Contención/bots.** Solo se desactiva BoxCollider fileID 1496176210, `Boundary_1_0848`, antiguo tramo superpuesto al acceso nuevo del puente C03. Puente y otras barreras sin cambios. El material existente conserva rebote 0.15/fricción 0.05. La clasificación de fuera de pista ignora paredes, normales laterales y otros Rigidbodies: se mantiene la reducción puntual al 75% solo al salir realmente. `RallyBotCollisionYield` se instala en el Rigidbody del bot para recibir contactos; tras contacto sostenido (0.12 s) con jugador trabado a ≤6 km/h, el controlador frena 0.75 s, sin aceleración/reversa, y retoma; cooldown 1 s. Duración serializada 0.5–1 s. Ningún perfil físico cambia.

## Evidencia

- Compilación de los scripts nuevos confirmada tras reimportación/reinicio del Editor. Unity había omitido los archivos nuevos en su lista de fuentes; no fue un error de sintaxis. `git diff --check` limpio.
- `Logs/five-blocks-menu-graph.txt`: **COMPLETE: PASS + RESTORED**, menú real en Play; tres ciclos 1P↔2P sin duplicar fila, ambos navegadores usan los mismos tres botones y sus confirmaciones se aplican a la pista compartida. Es prueba del grafo de botones/submission, no de hardware DualSense ni de todas las animaciones renderizadas.
- `Logs/dust-trail-check.txt`: **COMPLETE: PASS**, contactos reales WheelCollider/partículas en física aislada y dos rigs con giros opuestos independientes. No equivale a benchmark ni a sesión completa de pantalla dividida.
- `Logs/bridge-penalty-integration.txt`: **COMPLETE: PASS**, cruce real con WheelColliders, desde aproximación hasta salida, carriles 0 y ±2.5 m, mínimos de velocidad ~63–66 km/h; cero penalidades fuera de pista. No se condujo una vuelta completa.
- `Logs/five-blocks-boundary-physics.txt`: **COMPLETE: PASS**, rebote físico (velocidad lateral de retorno ~−0.475 m/s), ninguna penalidad ni límite persistente; avanzó ~21.56 m después.
- `Logs/off-track-penalty-physics.txt`: **COMPLETE: PASS**, tres modelos, pérdida horizontal exacta de 25% una vez por salida, rearme, exclusión en aire y superficies de las tres pistas/puente.
- `Logs/five-blocks-bot-state.txt`: **COMPLETE: PASS**, nueve bots de las tres escenas, configuración Fácil, receptor en Rigidbody, pausa/velocidad del jugador, 0.75 s y retorno al control de waypoints. Se fuerza la expiración del plazo en esta prueba aislada: no prueba callbacks de contacto real ni tiempo transcurrido en Play.
- `Logs/five-blocks-bot-Circuit_01.txt`, `...Circuit_02.txt`, `...Circuit_03.txt`: **COMPLETE: PASS + RESTORED** en las pruebas finales individuales. Contacto físico real mediante colliders temporales sobre los Rigidbody existentes, freno de 0.5–1 s (C02 ~0.75 s, C03 ~0.74 s), aceleración reanudada y perfil Fácil de todos los bots de cada escena. Se aísla el contacto sobre el escenario para evitar interferencia de otros coches; no equivale a una vuelta competitiva completa. El informe secuencial anterior `five-blocks-bot-contacts.txt` conserva el intento interrumpido, pero no representa el resultado final.
- `Logs/five-blocks-preservation.txt`: **PASS**, comparación contra HEAD del alcance de estos cinco bloques y verificación separada 13/13/12 de turbos/materiales posteriormente aplicados por otra tarea. No revertir esos cambios ajenos.

## Recuperación y cierre

El usuario confirmó que no estaba operando Unity durante las interrupciones. Se encontró en la ventana del Editor un diálogo modal **Opening file failed / Recursos insuficientes en el sistema**, al abrir `Assets/Art/Forest/Circuit03/RiverBridge/RiverSurface.asset`. Ese diálogo explica el bloqueo de la API; no se atribuye al usuario ni se afirma que explique por sí solo cada interrupción previa de Play. Se pulsó Cancelar (no Force Quit), se limpiaron verificadores interrumpidos, se confirmó que no había escenas sucias y se abrió MainMenu antes de liberar assets no usados. No se borraron archivos, ni se cambiaron assets/configuración de Windows. La memoria asignada del Editor bajó aproximadamente de 977 a 907 MB en esa limpieza. Registro: `Logs/five-blocks-editor-recovery.txt`.

Después se probaron las tres pistas por separado desde la escena liviana: **las tres pruebas de contacto terminaron PASS y restauraron preferencias, escenas, InputSettings y timeScale**. `Logs/five-blocks-final-status.txt` comprueba compilación válida, Editor fuera de Play y ninguna escena sin guardar. No quedaron verificaciones funcionales pendientes de estos cinco bloques. El build Windows no se regeneró.

## Repetir las pruebas

Con Editor estable, fuera de Play y escenas guardadas, ejecutar mediante Unity Pipeline:

```text
unity command run_script --file Tools/RallySelectionUxCheck.cs --entry RallySelectionUxCheck.RunBotCircuit01
unity command run_script --file Tools/RallySelectionUxCheck.cs --entry RallySelectionUxCheck.RunBotCircuit02
unity command run_script --file Tools/RallySelectionUxCheck.cs --entry RallySelectionUxCheck.RunBotCircuit03
```

Esperar **COMPLETE: PASS y RESTORED** en el informe de cada prueba antes de iniciar otra. Cada prueba restaura preferencias, escenas, InputSettings, pausa y timeScale y no guarda escenas de Play. Si se interrumpe, no contabilizarla como aprobada. El build Windows no se regeneró en este pedido.
