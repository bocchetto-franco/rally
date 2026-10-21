# Windows Release verificado — 2026-10-20

Se encontraron todos los cambios pedidos, guardados como archivos del proyecto:

| Cambio | Archivo o referencia principal |
| --- | --- |
| Escape de jugadores/bots y los tres modelos | `Assets/Scripts/RallyVehicleExhaust.cs`; boquillas en `Assets/Resources/Vehicles/` |
| Lluvia, niebla y camino húmedo | `Assets/Scenes/Circuit_03.unity`, `Assets/Art/Atmosphere/Circuit03 - Rain.asset` |
| Atardecer separado y desactivable | `Assets/Scenes/Circuit_01.unity`, `Assets/Art/Atmosphere/Circuit01 - Sunset.asset` |
| Previsualización de autos | `Assets/Scripts/RallyMenuVehiclePreview.cs`, prefabs en `Assets/Resources/MenuVehicles/` |
| Créditos | `CREDITS.md`, `Assets/Resources/Credits/Credits.txt`, `Assets/StreamingAssets/CREDITS.md`, verificadas idénticas |
| Pantalla dividida | `Assets/Scripts/RallySplitScreen.cs`, `RallyLocalPlayerInput.cs`, `RallyLocalDevices.cs` |
| Selección, navegación, HUD y pausa | `RallyMenuController.cs`, `RallyMenuNavigation.cs`, `RallyRaceHud.cs`, `RallyPauseMenu.cs` y escenas de frontend |

La verificación abrió los tres circuitos desde disco y confirmó que estaban guardados.
No se reescribieron escenas, trazados, checkpoints, waypoints ni física. Las boquillas
son metadata visual; los prefabs no cambiaron Transform, colliders ni manejo. El escape
espera al montaje de ambos jugadores para elegir correctamente el modelo y no clonar
emisores duplicados.

Build Settings conserva seis escenas habilitadas, en este orden:

1. `Assets/Scenes/MainMenu.unity`
2. `Assets/Scenes/VehicleCircuitSelection.unity`
3. `Assets/Scenes/Circuit_01.unity`
4. `Assets/Scenes/Circuit_02.unity`
5. `Assets/Scenes/Circuit_03.unity`
6. `Assets/Scenes/RaceResults.unity`

Unity 6000.6.0f1; producto Rally1; versión 0.1.0; resolución 1280×720;
Active Input Handling **Both**. El build no cambió estos valores.

## Generación real

Se ejecutó el nuevo `BuildWindows.BuildRelease`, la misma entrada de
**Build → Windows Release**, sin errores de compilación del Editor ni del Player.

- **Succeeded, 0 errores, 3 avisos**. Windows x64 Release, sin Development, LZ4HC.
- Inicio 2026-10-20 22:51:19 −03:00; fin 22:57:09 −03:00; duración Unity 5 min 47 s.
- Salida `Build/Windows/Rally.exe`; tamaño de build informado 642,574,828 bytes.
- SHA-256 del EXE: `A4C71B1BFC3E42D02F2F129FE04ADEF3EBF2343A662884E4B99598E9DFC90E7D`.
- Log completo `Build/Windows/BuildLog.txt`; identificación `BuildInfo.txt`.

Avisos no bloqueantes, ya presentes en el proyecto:

1. **Pipeline / RuntimePipelineConfig ausente**: desactiva la conexión de automatización
   Pipeline dentro del Player. No es URP ni significa que falte el renderer del juego.
2. **TextMeshPro/Mobile/Distance Field**: pragma de símbolos de depuración D3D11 obsoleto
   en el shader incluido. No se modificó el paquete de TMP.
3. **Pre-baked collision** deshabilitado en seis mallas ProBuilder; Unity todavía cocina
   sus colisiones al generar el build. Mallas: `pb_Mesh29034:1280`, `pb_Mesh43710:768`,
   `pb_Mesh27416:768`, `pb_Mesh27425:768`, `pb_Mesh27726:768`, `pb_Mesh27737:768`.
   Se conservaron sus colliders y geometría por el alcance pedido.

La validación de Play de efectos terminó **COMPLETE: PASS** en C01/C02/C03 con 1P
y C01/C03 con 2P. Se comprobaron escape por modelo, input visual independiente,
lluvia acotada, restauración de perfiles y ausencia de clima nuevo en C02. No fue una
vuelta completa del ejecutable ni una prueba con gamepads físicos. Informes locales:
`Logs/graphics-atmosphere-play.txt`, `Logs/windows-preparation-verification.txt`.

## Acceso directo

`CrearAccesoDirecto.ps1` se ejecutó dos veces con `-NoPause`: creación y reemplazo
correctos, ambos sin errores. Se verificó el destino con WScript.Shell.

- En esta máquina: `C:\Users\franc\OneDrive\Desktop\Rally.lnk`.
- Destino `C:\Users\franc\Downloads\rally\Build\Windows\Rally.exe`.
- Ícono del ejecutable, índice 0; directorio de trabajo `Build\Windows` del repo.
- Usa el Escritorio real del usuario y `$PSScriptRoot`, sin una ruta de clonación fija.

Para reproducir después de Pull, seguir `BUILD_WINDOWS.md`. Binarios y logs quedan
fuera de Git; cada equipo genera su carpeta Windows. No falta ningún cambio de la
lista. El audio de lluvia opcional no se agregó; no se descargaron assets nuevos.
