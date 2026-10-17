# Zonas de turbo

Implementación adicional al controlador existente, compartida por jugadores y bots. No cambia masa, damping, fricciones, suspensión, motor, downforce ni asistencia de estabilidad.

## Parámetros

- Tres zonas por circuito bajo `Rally Turbo Zones`, con hijos `Turbo_01/02/03 - centre lane 1.4m`.
- Señal visual de 2.2 × 8 m: cuatro flechas amarillas sobre franja oscura, malla adaptada a la altura real del camino, materiales URP Unlit compartidos, sin sombras ni colisión sólida.
- La raíz del Rigidbody debe entrar en un carril central de 1.4 m, dentro del volumen y avanzando en el sentido de las flechas. Rozar con la carrocería/ruedas no alcanza. Velocidad mínima: 12 km/h; alineación mínima del movimiento: coseno 0.5.
- Impulso: 4.5 m/s² durante 2.25 s mediante `ForceMode.Acceleration`, igual para cualquier masa. Solo empuja con al menos dos ruedas apoyadas y el auto enderezado; sin fuerza vertical añadida. El impulso deja de aportar aceleración a 180 km/h y nunca reduce una velocidad superior existente.
- No se acumula mientras ya está activo. Cada zona tiene un rearme de 8 s por vehículo; el cooldown no es compartido entre corredores y usa tiempo de juego, por lo que la pausa lo congela.
- Los bots reciben temporalmente +35 km/h en el objetivo de su conducción para no frenar inmediatamente el impulso. El perfil/dificultad base permanecen intactos; el margen vuelve a cero al expirar/deshabilitarse el efecto.
- No activa con controles deshabilitados, durante preparación ni en pausa. Cancelación al finalizar, deshabilitar el auto o detectar un teletransporte de más de 12 m entre pasos físicos.

`RallyTurboZone` contiene los valores serializados por zona. `RallyVehicleTurbo` es el receptor por auto; las zonas lo agregan durante Start/preparación usando el roster cacheado, evitando crear efectos en el primer contacto. El impulso no requiere partículas, audio ni recursos externos.

## Autoría y verificación

`Tools/RallyTurboSetup.cs` planifica posiciones desde los waypoints y raycasts contra el camino actual, evitando salida/meta, charcos, curvas cerradas y zonas demasiado próximas. Antes de escribir valida las nueve posiciones; respalda escenas y compara todos los componentes originales. Rechaza duplicar una instalación existente. No reconstruye terreno, carretera, checkpoints ni waypoints.

`Tools/RallyTurboRuntimeCheck.cs` prueba cruces físicos por centro/borde, fuerza medida, expiración, no acumulación, bloqueo en pausa/reversa/controles deshabilitados y jugadores/bots en los tres circuitos. Solo dar la ejecución por válida si `Logs/turbo-play-check.txt` termina en `COMPLETE: PASS` y `RESTORED`.

Instalación completada: `Logs/turbo-build.txt`, **COMPLETE: PASS**, tres zonas por escena y componentes originales preservados. Respaldo: `Logs/SceneBackups/Turbo_20261016_181541/`.

Prueba real de Play: `Logs/turbo-play-check.txt`, **COMPLETE: PASS** y **RESTORED**. Atravesó físicamente las nueve zonas por centro y borde; las pasadas centrales fueron más rápidas que el mismo carril con la zona deshabilitada. Validó impulso/expiración en un bot por circuito, sin cambiar su velocidad base; bloqueo en pausa/reversa/controles deshabilitados; ausencia de acumulación; dinámica serializada/masa/damping del jugador intactos. C03 2P comprobó ambos receptores precargados, uso de la misma zona por J1/J2 y ausencia de efecto/cooldown compartido. La prueba neutraliza inputs/IA en la copia de Play para medir el impulso aislado: no equivale a una carrera completa de bots ni a evaluar dificultad.

`Tools/VerifyTurboPreservation.ps1` verificó por separado 5257/9307/14885 bloques serializados originales de C01/02/03 idénticos a los respaldos; únicamente una raíz y tres zonas nuevas por escena. Las tres vistas `Logs/Turbo_Circuit_0X_0.png` fueron revisadas: flechas amarillas legibles sobre el camino, sin colliders sólidos ni superficie flotante. Hay capturas adicionales para las nueve zonas.

| Circuito | Estaciones aproximadas desde salida | Desplazamiento lateral |
| --- | --- | --- |
| Circuit_01 | 266 / 726 / 1122 m | +1.2 / -1.2 / +1.2 m |
| Circuit_02 | 290 / 814 / 1258 m | +1.2 / -1.2 / +1.2 m |
| Circuit_03 | 278 / 830 / 1306 m | +1.2 / -1.2 / +1.2 m |

Las distancias se miden sobre los waypoints actuales, incluido el puente; no se regeneraron trazados. Materiales/mallas originales de señalización en `Assets/Art/RallyTurbo/`, sin descargas nuevas. No son oclusores: los bakes existentes se conservan.
