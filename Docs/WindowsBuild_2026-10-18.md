# Build Windows actualizado — 2026-10-18

- Ejecutable: `Build/Windows_2026-10-18/Rally1.exe` (Windows x64, Mono, Release, compresión LZ4HC).
- Resultado: **Succeeded**, 0 errores, 3 avisos de Unity; duración 4 min 39 s; tamaño reportado 637,618,518 bytes.
- Producto Rally1, versión 0.1.0, ventana inicial 1280×720, Active Input Handling Both.
- Orden de escenas: MainMenu, VehicleCircuitSelection, Circuit_01, Circuit_02, Circuit_03, RaceResults.
- Incluye Lancia Delta S4, Peugeot 205 T16 y Audi Quattro S1, selección/previews/bots actualizados, estela de conducción del 18 de octubre y demás cambios actuales del proyecto.
- Créditos distribuidos idénticos a CREDITS.md; comprobado el formato PE x64 y la presencia de los campos/métodos nuevos de la estela en Assembly-CSharp.dll del build.
- SHA-256 del EXE: `A4C71B1BFC3E42D02F2F129FE04ADEF3EBF2343A662884E4B99598E9DFC90E7D`. El juego completo incluye las carpetas y DLL adjuntas; el EXE por sí solo no es una distribución completa.

## Avisos

1. `Pipeline: No RuntimePipelineConfig asset found`: la herramienta Unity Pipeline de automatización se desactiva en el Player. No es el render pipeline URP.
2. Shader TextMeshPro/Mobile/Distance Field: directiva `enable_d3d11_debug_symbols` obsoleta; Unity recomienda `enable_debug_symbols`.
3. Seis mallas ProBuilder usadas por MeshColliders tienen pre-baked collision desactivado; Unity realiza el prehorneado para este build. Nombres: `pb_Mesh43710:768`, `pb_Mesh29034:1280`, `pb_Mesh27425:768`, `pb_Mesh27416:768`, `pb_Mesh27737:768`, `pb_Mesh27726:768`.

Además, el proceso ILPP emitió dos avisos de rutas de referencia duplicadas: `System.Runtime.InteropServices.WindowsRuntime` y `System.ComponentModel.Composition`. Resolvió ambos eligiendo las referencias de Unity. No se contabilizan como avisos adicionales en BuildReport.summary.

Informe de Unity: `Logs/windows-build-2026-10-18.txt`, copiado al directorio de salida como `BuildReport.txt`. Copias de cachés generadas: `Logs/SceneBackups/WindowsBuild_20261018_213146/`. Se conservan los builds anteriores. Esta comprobación del artefacto no incluye una vuelta conducida ni una medición de FPS del ejecutable nuevo.

Constructor externo: `Tools/RallyCurrentWindowsBuild.cs`, entrada `BuildNow`. Rechaza sobreescribir un ejecutable existente. No reconstruye pistas ni altera tuning.
