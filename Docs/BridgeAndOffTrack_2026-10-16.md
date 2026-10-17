# Revisión de puente y fuera de pista — 2026-10-16

## Circuit_03

Se auditó el collider físico `Circuit 03 - Timber River Crossing/Continuous smooth deck collider` y las paredes próximas usando la escena guardada. Las muestras del centro y extremos transitables no detectaron penetración de una pared en el corredor. Esto no descarta un enganche durante una maniobra concreta fuera de esa trayectoria.

Se encontró un labio de 0.015 m en la unión: Transform Y=-0.16, alto 0.35, centro original 0; techo físico = +0.015 respecto de la entrada. Se cambió exclusivamente `BoxCollider.center.y` a -0.015. El techo queda a 0 y el raycast posterior confirma `dy=0.000` en la unión (antes 0.015). No se quitó ningún límite ni se modificó el puente visible. El constructor histórico conserva la corrección si se reconstruye explícitamente en el futuro.

Respaldo: `Logs/SceneBackups/BridgeLip_20261016/Circuit_03.unity`. Auditoría reproducible: `Tools/BridgeObstructionAudit.cs`, entrada `Run`; resultados `Logs/bridge-obstruction-before.txt` y `bridge-obstruction-audit.txt`. `Tools/VerifyBridgeLip.ps1` compara toda la escena con el respaldo y permite únicamente el cambio de centro del collider. Los colliders no son oclusores; no se requiere regenerar el bake visual por esta corrección.

Validación geométrica y preservación: PASS. Tras resolver la interferencia de compilaciones, `Tools/BridgeAndSurfacePhysicsCheck.cs` probó Rigidbody/WheelColliders reales en una PhysicsScene aislada con el circuito guardado: paso completo por acceso/tablero/salida en centro y carriles a ±2.5 m, velocidades mínimas 66.51/65.01/67.59 km/h sin trabarse. Informe `Logs/bridge-isolated-physics.txt`, `COMPLETE: PASS`. No es una vuelta completa ni una garantía para toda maniobra fuera del corredor. El verificador histórico de Play ahora espera la preparación de carrera antes de mover el auto.

## Penalidad fuera de pista: regla nueva autorizada

No se encontró código de detección de superficie fuera de pista ni un multiplicador de velocidad 0.5 en la copia abierta `C:/Users/franc/Downloads/rally`, main `bbe3d33`. Se revisaron scripts propios/asset JRS, historial de todas las ramas locales, rama `fix/menu-joystick-navigation`, origen actualizado con fetch, y las copias de rally en el workspace y `rally-basin-staging`.

Sí existen autofrenado por ángulo/exceso de velocidad y resistencia de charcos. Ninguno detecta fuera de pista ni aplica una reducción del 50%. La comparación física sin la regla nueva dio 88.19 km/h en camino y 88.16 en banquina después de 2 s desde 50 km/h con acelerador sostenido. No hay evidencia de un multiplicador 50% en esta versión.

El usuario confirmó expresamente crear la regla del 75% después de explicar esa ausencia. `Assets/Scripts/RallyOffTrackSpeedPenalty.cs` se instala en cada jugador desde `RallyVehicleDynamics.Start`, incluidos los modelos seleccionados y el jugador local 2. Excluye bots y escenas sin superficies reconocidas. No cambia tuning del vehículo ni gameplay de agua.

Semántica explícita: al confirmar una salida durante 0.12 s, conserva **75% de la velocidad horizontal que llevaba al salir**, una única vez por salida (100 → 75 km/h). No es un límite permanente de velocidad máxima, ni multiplica 0.75 cada FixedUpdate. Se puede seguir acelerando fuera de pista. Velocidad vertical y angular intactas; no actúa con control/dinámica deshabilitados o Rigidbody cinemático. El tiempo de confirmación evita penalizar un contacto transitorio; requiere al menos dos ruedas apoyadas y mayoría fuera (el empate en el borde no cuenta). Se rearma después de 0.35 s con mayoría de ruedas en camino. El vuelo no cuenta como fuera de pista.

Superficies reconocidas, cacheadas una vez: `Rally_Road_Start_to_Finish`, `Circuit02_Road`, `Circuit03_Road`, `Continuous smooth deck collider`. El puente es pista. Banquinas y Terrain son fuera de pista. Al renombrar/reconstruir un circuito hay que actualizar esta lista o migrarla a marcadores explícitos; no añadir búsquedas globales por frame.

Variables en Inspector: `Retained Speed` 0.75, `Off Track Confirmation Seconds` 0.12 y `Road Rearm Seconds` 0.35. Prueba reproducible `Tools/OffTrackPenaltyPhysicsCheck.cs`, entrada `Run`, informe `Logs/off-track-penalty-physics.txt`; exigir `COMPLETE: PASS` antes de dar la regla por validada.

Validación final: `off-track-penalty-physics.txt` terminó en **COMPLETE: PASS** para Porsche/BMW/Audi, instalación desde Start, retención exacta 0.75, velocidad vertical/angular intactas, una aplicación por salida, rearmado, vuelo y bots excluidos y superficies de los tres circuitos reconocidas. `BridgeAndSurfacePhysicsCheck.RunIntegrated` e informe `Logs/bridge-penalty-integration.txt` terminaron en **COMPLETE: PASS**: tres cruces físicos con la regla activa y cero penalidades sobre el puente. Son pruebas físicas aisladas con el código real, no una sesión de juego humana. Las escenas abiertas se conservan; no se generó una nueva build de Windows.
