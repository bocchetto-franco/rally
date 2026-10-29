# Windows Release — 2026-10-22

- Ejecutable: `Build/Windows/Rally.exe`, Windows x64 Release, Unity 6000.6.0f1, versión 0.1.0.
- Fuente: estado guardado del repositorio, commit `192eec5`, sin modificaciones de gameplay realizadas para este build.
- Inicio: 17:54:54; final: 18:00:43 (Argentina, UTC−03).
- Resultado: **Succeeded, 0 errores, 3 advertencias**, duración **5 min 49 s**, 642,578,625 bytes según BuildReport.
- Log completo: `Build/Windows/BuildLog.txt`; identificación: `Build/Windows/BuildInfo.txt`.
- Build anterior conservado íntegro en `Build/Windows_Previous_20261022_175453/`.
- Acceso directo `Rally.lnk` actualizado en el Escritorio del usuario para apuntar a la salida nueva, no al respaldo.

## Contenido

Escenas habilitadas, en este orden: MainMenu, VehicleCircuitSelection, Circuit_01, Circuit_02, Circuit_03 y RaceResults. Input Handling permanece en **Both**.

Incluye el estado actual guardado: flota Grupo B y previews, audio/créditos, pantalla dividida, clima/escape, menú compartido y dificultad Fácil, dirección 46/66, polvo por giro independiente, correcciones de puente/contención y frenado temporal de bots al trabar al jugador; además, turbos reubicados con activación de 4.4 m y materiales privados más contrastados en C01/C02.

Los hashes de las tres escenas son idénticos antes/después del build: no se guardaron cambios de trazado, checkpoints, waypoints ni tuning durante la generación. El assembly del juego cambió respecto al build anterior y contiene las clases nuevas `RallyBotCollisionYield` y `RallyTrackBoundary`. Unity retiró automáticamente sus cuatro archivos temporales PerformanceTestRun al finalizar.

## Advertencias

1. **Pipeline:** falta RuntimePipelineConfig; la automatización de Unity Pipeline se desactiva en Player. No se activó una conexión de automatización dentro del juego.
2. **TextMeshPro/Mobile/Distance Field:** `#pragma enable_d3d11_debug_symbols` está obsoleto; Unity recomienda `enable_debug_symbols`.
3. **Colisión precalculada:** seis mallas ProBuilder tienen triangle collision desactivado: `pb_Mesh43710:768`, `pb_Mesh29034:1280`, `pb_Mesh27416:768`, `pb_Mesh27425:768`, `pb_Mesh27726:768` y `pb_Mesh27737:768`. Unity todavía realiza el precálculo automáticamente en esta versión; requiere revisión para compatibilidad con versiones futuras. No se alteraron esas mallas ni sus colliders en esta tarea.

Son los tres avisos preexistentes documentados en el build anterior. El aviso de depreciación del Input Manager visto al abrir el Editor es independiente; Both se conserva por requisito del proyecto.

## SHA-256

| Archivo | SHA-256 |
| --- | --- |
| Rally.exe | `A4C71B1BFC3E42D02F2F129FE04ADEF3EBF2343A662884E4B99598E9DFC90E7D` |
| Rally_Data/Managed/Assembly-CSharp.dll | `E5B927A0792A4922CF19E86DE5BF4B4CCC3CE7EFC05D33578FF0C955C06BAC2E` |
| Rally_Data/data.unity3d | `80AF1A445DBAA58D130C3DDEA537F42170E7B81B9C4695E73367FC84D3A2815B` |

El EXE es el lanzador de Unity; el contenido nuevo se distribuye principalmente en Rally_Data. Conservar **toda la carpeta Build/Windows**, con sus DLL y subcarpetas, no copiar solamente el EXE.

Verificación de generación y archivos, no una prueba conducida del ejecutable. La prueba integral de turbos en Play de la tarea anterior sigue documentada como incompleta; generar este build no cambia ese resultado.
