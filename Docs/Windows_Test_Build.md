# Build de prueba Windows

Preparado el 2026-10-10 con Unity 6000.6.0f1. La copia final incluye la pantalla de Créditos y sus avisos de StreamingAssets integrados por la tarea paralela. Se reconstruyó tras detectar y corregir un fallo real de URP al ejecutar fuera del editor.

## Configuración

- Plataforma: Windows x64; backend Mono existente. Build normal, sin Development Build ni Script Debugging; compresión LZ4HC.
- Producto: Rally1; compañía: DefaultCompany; versión: 0.1.0. Se conservaron estos identificadores para no cambiar el almacenamiento de PlayerPrefs.
- Ventana predeterminada: 1280×720, redimensionable, sin forzar resolución nativa. Preferencias de pantalla de una ejecución previa pueden prevalecer sobre estos valores.
- Active Input Handling: Both, sin cambios. Teclado y Unity Input System/gamepads incluidos.
- Escenas habilitadas en orden: MainMenu, VehicleCircuitSelection, Circuit_01, Circuit_02, Circuit_03, RaceResults. MainMenu tiene índice 0; el inicio de una build no depende del playModeStartScene del editor.
- Se conservan URP, las optimizaciones existentes, física, selección, bots y modo local.

## Dependencias del editor

- Se revisaron los scripts runtime y búsquedas de rutas absolutas/Assets/Editor/AssetDatabase. Los recursos de menú, vehículos y audio se cargan desde Resources; las pistas se cargan por nombre incluido en el build.
- Corrección: `Assets/TextMesh Pro/Examples & Extras/Scripts/TMP_TextInfoDebugTool.cs` tenía `using UnityEditor` fuera de `#if UNITY_EDITOR`, aunque sus métodos de debug ya estaban protegidos. Se protegió también ese import, sin cambios funcionales.
- El diagnóstico del benchmark en `RallyRenderPerformance` y el código de salida del asset de vehículo ya tienen protección UNITY_EDITOR. Las herramientas externas de Tools y las de Assets/Editor no se incluyen en el ejecutable.
- Corrección de Player: `Assets/Settings/PC_Renderer.asset` conservaba SSAO desactivado en su lista de features. URP 17.6 ejecuta `Create()` también sobre features inactivos, pero elimina sus recursos al construir. Esto causaba NullReferenceException en `ScreenSpaceAmbientOcclusion.TryPrepareResources`, errores secundarios de Blitter y ventana negra. Se retiró la referencia de la lista (se conserva el subasset inactivo como respaldo); SSAO sigue apagado, sin cambio de calidad o gameplay. No se editó el código de los paquetes.

## Salida y reconstrucción

Ejecutable: `C:\Users\franc\Downloads\rally\Build\Windows_Test_2026-10-10\Rally1.exe`.

Para copiar la prueba, conservar **toda la carpeta** de salida, no solo el exe: requiere Rally1_Data, UnityPlayer.dll, MonoBleedingEdge y demás archivos generados. La carpeta `Rally1_BackUpThisFolder_ButDontShipItWithYourGame` contiene símbolos/respaldo de depuración y no necesita distribuirse. Build está excluida de Git; no se publicó un binario en GitHub. Esta es una prueba local, no una aprobación de publicación comercial: Créditos documenta permisos de assets todavía pendientes, incluido el Porsche.

Herramienta editorial externa: `Tools/RallyWindowsTestBuild.cs`, entradas Prepare/Start mediante Unity Pipeline, con Unity fuera de Play y escenas guardadas. Start rechaza sobrescribir un ejecutable existente; modificar la ruta para un build posterior o conservar la copia antes de reconstruir. No ejecutar builds mientras otra tarea modifica Assets/ProjectSettings.

Registro completo: `Logs/windows-test-build.txt`, **COMPLETE: PASS**. Player.log está en `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Rally1\Player.log`; copia de la prueba final en `Logs/windows-player-smoke.log`.

## Resultado final y alcance de prueba

- BuildReport: **Succeeded**, **0 errores**, **3 advertencias**; 658737925 bytes (~628 MiB), 4 min 58.8 s. Windows x64 verificado (cabecera PE: Machine 0x8664). No hay DLL UnityEditor en la carpeta Managed.
- SHA-256 de Rally1.exe: `A4C71B1BFC3E42D02F2F129FE04ADEF3EBF2343A662884E4B99598E9DFC90E7D`.
- Arranque real fuera del editor: Input System y PhysX inicializados, URP renderiza, menú MainMenu visible con fondo 3D y texto «Presioná cualquier botón para continuar». Sin NullReferenceException/errores Blitter ni excepciones en el log de esta prueba. La ventana quedó minimizada; no se forzaron entradas cuando el control de aplicaciones informó intervención del usuario.
- Las seis escenas y el mapeo de las tres opciones de pista fueron comprobados en configuración/código y compilados. **No se probó la carga jugable de las tres pistas, una vuelta completa ni gamepads físicos en este ejecutable**. Comprobar manualmente cada opción en selección, pausa con Escape y una carrera 2P antes de dar por validado el juego end-to-end.
- Se conservaron física, escenas, circuitos, waypoints y calidad. Los cambios fuente de esta tarea son Player Settings de ventana, el import UnityEditor protegido en TMP, la desconexión de SSAO inactivo, el constructor externo y esta documentación. Los cambios de Créditos pertenecen a la tarea paralela.
- Fuentes y cachés derivados de Unity se restauraron al estado previo al segundo build (incluidos cambios de la otra tarea), con respaldos en `Logs/SceneBackups/WindowsBuildSecondBaseline_20261010/` y `WindowsBuildSecondGenerated_20261010/`. UnityConnect permanece desactivado; preload temporal de InputActions retirado del archivo fuente tras la construcción. No se borró ningún asset de gameplay.

## Avisos detectados durante la construcción

- Unity Pipeline: no se encontró RuntimePipelineConfig; el paquete de automatización se desactiva en Player. No se habilitó automatización remota dentro del juego. Esto no desactiva URP.
- Shader TextMeshPro/Mobile/Distance Field: pragma `enable_d3d11_debug_symbols` obsoleto en un shader de TMP incluido; recomienda `enable_debug_symbols`. No es error de compilación del shader.
- Seis mallas ProBuilder referenciadas por MeshCollider carecen de pre-baked collision (triangle collision disabled). Unity aún hornea su colisión al construir; advierte que habrá que habilitarlo explícitamente en versiones futuras. No se alteraron colliders ni geometría.
- ILPP/.NET: ruta duplicada de `System.Runtime.InteropServices.WindowsRuntime.dll`; Unity elige la copia de sus ReferenceAssemblies en lugar de la de NetStandard/Extensions.
- ILPP/.NET: ruta duplicada de `System.ComponentModel.Composition.dll`; Unity elige la copia de sus ReferenceAssemblies en lugar del shim NetStandard.

Los dos últimos son mensajes `warn:` del proceso interno de postprocesado de ensamblados, no necesariamente incluidos en el contador de warnings del BuildReport. Se preservan como advertencias de esta ejecución; no se editó la instalación de Unity para silenciarlos.

Mallas afectadas por el tercer warning del BuildReport: `pb_Mesh43710:768`, `pb_Mesh29034:1280`, `pb_Mesh27425:768`, `pb_Mesh27416:768`, `pb_Mesh27737:768` y `pb_Mesh27726:768`.

Avisos adicionales al iniciar el Player: shaders `GaussianDepthOfField` (dos mensajes), `BokehDepthOfField` y `PaniniProjection` no soportados o retirados del build; esos pases no se ejecutan. Son efectos ya desactivados en el presupuesto de rendimiento, no fallos del menú/URP ni errores de compilación. No se reactivaron efectos costosos ni se deshabilitó stripping global para silenciar el diagnóstico. También se informa que XInput1_3.dll no está disponible y se usa XInput9_1_0.dll; la inicialización de input completa correctamente, pero no equivale a una prueba con hardware real.

Primer intento: `Succeeded`, 3 warnings, 0 errores de build; 657937511 bytes, 59 min 22.7 s (caché fría de shaders). **No apto para jugar** por el fallo de inicialización de SSAO detectado en el smoke test. Se conserva en `Logs/SceneBackups/WindowsBuildFailed_20261010/`, con informes `Logs/windows-test-build-first-attempt.txt` y `Logs/windows-player-first-attempt.log`; no es la carpeta final a distribuir.

Referencia del método de construcción: [BuildPipeline.BuildPlayer y BuildReport de Unity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPipeline.BuildPlayer.html).
