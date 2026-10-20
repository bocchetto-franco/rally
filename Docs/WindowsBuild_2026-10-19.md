# Build Windows actualizado — 2026-10-19

- Ejecutable: `Build/Windows_2026-10-19/Rally1.exe`, Windows x64, Mono Release, LZ4HC.
- **Succeeded**, cero errores, tres advertencias; 5 min 28 s, 637,619,025 bytes reportados.
- Rally1, versión 0.1.0, 1280×720 Windowed, Active Input Handling Both.
- Escenas: MainMenu, VehicleCircuitSelection, Circuit_01, Circuit_02, Circuit_03, RaceResults.
- Incluye la corrección de polvo proporcional al giro del 19 de octubre, los tres Grupo B y el steering vigente. Detalles/pruebas de partículas en `Docs/DustSteering_2026-10-19.md`.
- Verificados PE x64 (0x8664), campos `fullTurnEmissionBonus`/`currentSteerAngle` y setter de crecimiento en Assembly-CSharp del build; CREDITS.md distribuido idéntico al canónico.
- SHA-256 de `Rally1_Data/Managed/Assembly-CSharp.dll`: `D52A95371863EF1FC88153E5BE63488757D71F0CA80DF548C2C07C9FAC215793`.
- SHA-256 del EXE lanzador: `A4C71B1BFC3E42D02F2F129FE04ADEF3EBF2343A662884E4B99598E9DFC90E7D`. El lanzador puede ser idéntico al anterior; los cambios de juego están en Data. Mantener toda la carpeta, no copiar solo el EXE.

## Advertencias

1. La herramienta de automatización Pipeline no tiene RuntimePipelineConfig y se desactiva en el Player. No es el pipeline gráfico URP.
2. TextMeshPro/Mobile/Distance Field usa `enable_d3d11_debug_symbols`, directiva obsoleta.
3. Seis mallas ProBuilder tienen pre-baked collision desactivado; esta versión de Unity las prehornea al construir. Afectadas: `pb_Mesh29034:1280`, `pb_Mesh43710:768`, `pb_Mesh27416:768`, `pb_Mesh27425:768`, `pb_Mesh27726:768`, `pb_Mesh27737:768`.

ILPP también avisa de referencias duplicadas de System.Runtime.InteropServices.WindowsRuntime y System.ComponentModel.Composition, resueltas automáticamente usando las referencias de Unity. No se modifican estos recursos ajenos al ajuste de polvo.

Informe `Logs/windows-build-2026-10-19.txt` copiado como `BuildReport.txt` junto al ejecutable. Cachés/PlayerSettings/escenas restaurados; respaldo `Logs/SceneBackups/WindowsBuild_20261019_230450/`. Los builds anteriores se conservan. Esta validación comprueba compilación y contenido del artefacto; no es prueba de hardware físico, vuelta completa ni benchmark del ejecutable nuevo.
