# Zonas de turbo

Implementación adicional al controlador existente, compartida por jugadores y bots. No cambia masa, damping, fricciones, suspensión, motor, downforce ni asistencia de estabilidad.

## Parámetros

- Tres zonas por circuito bajo `Rally Turbo Zones`, con hijos `Turbo_01/02/03 - centre lane 4.4m` (actualización de posición/hitbox del 2026-10-21).
- Señal visual actual (2026-10-17): solo cuatro chevrones amarillos, sin franja de fondo. Ocupan 2.4 × 8.3 m, frente a flechas anteriores de 1.28 × 7.2 m. Material URP Unlit transparente, RGBA `(1, 0.88, 0.30, 0.55)`, mezcla alpha y ZWrite desactivado. Malla adaptada por raycast al camino, sin sombras ni colisión sólida. El mayor tamaño es únicamente visual: no ensancha la activación.
- La raíz del Rigidbody debe entrar en un carril central de **4.4 m**, dentro del BoxCollider de **5.2 × 3 × 10 m** y avanzando en el sentido de las flechas. `Activation Half Width = 2.2`; el margen exterior del collider no activa por mero roce de la carrocería. Velocidad mínima: 12 km/h; alineación mínima del movimiento: coseno 0.5. El visual conserva sus 2.4 × 8.3 m; ensanchar la activación no escala las flechas.
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
| Circuit_01 | 440 / 725 / 1223 m | 0 / 0 / 0 m |
| Circuit_02 | 398 / 233 / 1502 m | 0 / 0 / 0 m |
| Circuit_03 | 218 / 1568 / 1664 m | 0 / 0 / 0 m |

Las distancias se miden sobre los waypoints actuales, incluido el puente; no se regeneraron trazados. Materiales/mallas originales de señalización en `Assets/Art/RallyTurbo/`, sin descargas nuevas. No son oclusores: los bakes existentes se conservan.

La tabla vigente sigue el orden de nombres Turbo_01/02/03, no necesariamente el orden de paso. La revisión 2026-10-21 reubicó las zonas a rectas/salidas con 82–413 m hasta la próxima entrada de curva relevante. Las notas de grado 5 representan quiebres suaves, no se consideran horquillas; también se comprobó el cambio real de rumbo en los siguientes 55 m (máximo 25°), apoyo de cada vértice de las flechas sobre el camino y ausencia de charcos superpuestos. No se movieron las notas de frenada.

Autoría explícita: `Tools/RallyTurboRoadReadability.cs` (`Plan`/`Apply`); no ejecutarlo para cambiar un solo parámetro manual porque vuelve a planificar las nueve ubicaciones y restablece los valores de esta revisión. Informe de aplicación: `Logs/turbo-road-readability.txt`, respaldo `Logs/SceneBackups/TurboReadability_20261021_182357/`. `Tools/VerifyTurboRoadReadability.ps1 -Baseline <respaldo>` compara todos los bloques YAML: únicamente poses/nombres/anchos/colliders de las tres zonas y el material permitido del camino cambian. El impulso, las mallas/materiales de flechas, autos, terreno, notas, checkpoints y waypoints permanecen iguales.

## Actualización visual sin cambios de gameplay

`Tools/RallyTurboVisualUpgrade.cs`, entrada `Run`, actualiza los nueve Mesh assets manteniendo GUID/referencias y el material `Turbo yellow.mat`. La submalla 0 queda vacía (cero triángulos), por compatibilidad con el slot histórico; no hay geometría de fondo oscuro. La submalla 1 contiene 32 vértices/16 triángulos en cuatro chevrones. El material oscuro histórico se conserva como recurso, pero no dibuja un fondo.

Validación: `Logs/turbo-visual-upgrade.txt` **COMPLETE: PASS**, los tres archivos de escena byte-idénticos; no cambiaron Transform, BoxCollider 2.2×3×8 m, activación central de 1.4 m ni scripts de impulso. Respaldo de mallas/material: `Logs/SceneBackups/TurboVisual_20261017_113732/`. El instalador histórico `RallyTurboSetup` conserva el aspecto nuevo si se usa para una instalación futura; no ejecutarlo sobre las pistas existentes.

`RallySelectionUxCheck` comprueba material transparente real, tamaño y ausencia de fondo en las tres pistas 1P/2P. Para probar cruces físicos nuevamente usar `RallyTurboRuntimeCheck`; exigir el informe final actual, no solo el histórico. Las capturas `Logs/Turbo_Circuit_0X_0.png` muestran las flechas sobre el piso real.

Rerun final del 2026-10-17 después del cambio visual: ambos informes **COMPLETE: PASS / RESTORED**. Los nueve cruces centrales activaron y los nueve cruces de borde no; se conservaron duración/no acumulación y efecto independiente por vehículo, incluidos bots y J1/J2. Se revisaron las capturas nuevas de las tres pistas. Unity quedó fuera de Play y con InputSettings válido; no se regeneró el ejecutable Windows.
