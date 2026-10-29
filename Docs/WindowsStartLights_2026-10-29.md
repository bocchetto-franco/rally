# Build Windows con semáforo — 29/10/2026

- Ruta: `Build/Windows_StartLights_20261029/Rally.exe`.
- Resultado: **Succeeded**, 0 errores, 1 advertencia, 4 min 18,6 s, 650.570.759 bytes reportados por Unity.
- Unity 6000.6.0f1, Release x64, versión 0.1.0, producto interno Rally1. Escenas: MainMenu, VehicleCircuitSelection, Circuit_01, Circuit_02, Circuit_03, RaceResults. Active Input Handling Both.
- Incluye las fuentes actuales del semáforo con protección contra frames lentos, guía de mandos, aviso celeste de agua, selección simplificada y dirección 52°/82. Sin cambios nuevos de gameplay al generar el build.
- Advertencia conocida: Pipeline no encuentra RuntimePipelineConfig y deshabilita la automatización en Player. No afecta URP ni requiere activar un servidor en el juego.
- Se verificaron EXE, UnityPlayer.dll y Assembly-CSharp.dll con los tipos runtime de semáforo, guía de mandos, asistencia HUD y split. Hashes en `Logs/WindowsStartLights/artifact-hashes.json`.
- Acceso directo Rally actualizado a este EXE; copia del enlace anterior en `Logs/WindowsStartLights/Rally.previous.lnk`. Se conservó la sesión vieja del juego. Para ver los cambios, cerrar esa sesión y volver a abrir Rally.
- `RallyStartLightsBuild.Run` genera este snapshot sin sobrescribir `Build/Windows`, mientras el menú estándar Build/Windows Release conserva su ruta fija habitual.
- Evidencia funcional previa: seis pruebas Play del semáforo aprobadas en los tres circuitos y ambos modos, `Logs/start-lights-check.txt`. No se repitieron vueltas completas ni prueba de hardware en el ejecutable nuevo. La validación de artefactos no equivale a esas pruebas.
