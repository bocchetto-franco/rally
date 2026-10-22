# Turbos y legibilidad del camino — 2026-10-21

## Turbos

Tres zonas existentes por pista, sin duplicar ni alterar el efecto. Se movieron desde entradas de curvas a rectas o salidas; se conservaron meshes, materiales, dimensiones visuales y escalas. Los waypoints se leen en orden de hermanos, no orden alfabético (Waypoint_100 no debe preceder a Waypoint_11).

| Pista | Estación Turbo_01 / 02 / 03 (m) | Distancia a próxima curva relevante (m) |
| --- | --- | --- |
| Circuit_01 | 440 / 725 / 1223 | 85 / 413 / 146 |
| Circuit_02 | 398 / 233 / 1502 | 83 / 83 / 279 |
| Circuit_03 | 218 / 1568 / 1664 | 82 / 274 / 178 |

Se comprueban geometría futura (≤25° de cambio de rumbo en 55 m), apoyo del visual sobre el camino, anchura disponible y ausencia de charcos. Las notas suaves grado 5 no definen una entrada cerrada. Los volúmenes completos de notas se superponen ampliamente en C02/C03: no equivalen al inicio físico de las curvas. El mínimo de 80 m se mide hasta `CurveEntry` de notas grado 1–4, no hasta su trigger de aviso.

### Ajustes manuales serializados

- Hierarchy: `Rally Turbo Zones/Turbo_0X - centre lane 4.4m`.
- `Transform`: posición y orientación editables; mantener las flechas apoyadas en el camino.
- `RallyTurboZone > Activation Half Width`: **2.2 m** (antes 0.7).
- `BoxCollider > Size`: **(5.2, 3, 10)** (antes 2.2, 3, 8); `Center` sigue **(0, 1.5, 0)**; `Is Trigger` activo.
- Conservados: 12 km/h mínimo, alineación 0.5, impulso 4.5 m/s² durante 2.25 s, techo 180 km/h, bonus bot 35 km/h, rearme 8 s.

## Camino C01/C02

Materiales privados para no oscurecer Terrain, banquinas ni C03:

- `Assets/Art/RoadSurfaces/ReadableRoads/Circuit_01 - readable gravel.mat`: Base Color **RGBA (0.62, 0.53, 0.40, 1)**, marrón más oscuro.
- `Assets/Art/RoadSurfaces/ReadableRoads/Circuit_02 - readable gravel.mat`: Base Color **RGBA (0.60, 0.58, 0.50, 1)**, tierra/roca más neutra.

Asignados solo a `Rally_Road_Start_to_Finish` y `Circuit02_Road`, respectivamente. Editar **Surface Inputs > Base Map > Color** en el Inspector. Conservan las mismas texturas PBR, normal, máscara/AO, resolución y tiling del material compartido `Assets/Art/Environment/Materials/Road - dry gravel.mat`, que no fue modificado. No se añadieron bordes, objetos, shaders ni texturas nuevas; la mezcla suave existente con el terreno se conserva.

## Verificación

- Aplicación en Unity: `Logs/turbo-road-readability.txt`, **COMPLETE: PASS**; pruebas aisladas de la función real `TryActivate` para las nueve zonas: acepta x=1.65 m y z=4.6 m, rechaza x=2.7 m/z=5.3 m, reversa, detenido y pausa. No equivalen a una vuelta conducida.
- Comparación de los 5319/9366/14947 bloques YAML contra respaldo: **PASS**. Solo 13/13/12 bloques autorizados modificados. Conserva, entre otros, los ajustes previos de dirección 46/66 y el collider de puente deshabilitado por la otra tarea.
- Capturas `Logs/Circuit_0{1,2}_RoadContrast_{Before,After}.png` revisadas: mayor contraste sin rehacer la transición de banquina.
- Prueba de cruces físicos en Play: `Tools/RallyTurboRuntimeCheck.cs`, entrada `Run`; exigir informe actual `Logs/turbo-play-check.txt` con **COMPLETE: PASS** y **RESTORED** antes de afirmar aprobación completa. Incluye margen ampliado, impulso, bots y dos jugadores.
- Resultado de Play de esta revisión: **incompleto**, `FAIL: Play stopped externally before checks completed`, con preferencias/start scene/pausa restaurados y ninguna escena de Play guardada. No afirmar validación integral de bots/2P. La primera pasada observada en C01 sí activó a x=1.65 m (78.5 → 93.2 km/h, frente a 79.2 km/h de control); el rerun se interrumpió antes de repetir todos los casos. Los nueve chequeos directos de lógica y la preservación de escenas sí están completos. Tras reiniciar Unity se incluyeron dos scripts nuevos pendientes de otra tarea, sin editar sus fuentes; estado final de consola: compilación válida, 0 errores/0 advertencias actuales.

Respaldo previo: `Logs/SceneBackups/TurboReadability_20261021_182357/`. La copia headless se usó para diagnóstico sin exportar archivos al original; los cambios finales se aplicaron en Unity original tras desbloquear Windows. No se generó un build Windows en esta tarea.
