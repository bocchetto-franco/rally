# Generar Rally para Windows

1. En GitHub Desktop, abrir el repositorio **rally** y pulsar **Fetch origin → Pull origin**.
2. Abrir este proyecto en **Unity 6000.6.0f1**. Esperar a que termine la importación y compilación.
3. Fuera de Play, guardar las escenas modificadas y ejecutar **Build → Windows Release**.
   Esperar el mensaje de éxito. El menú cambia a Windows x64 si hace falta y toma
   las escenas habilitadas en Build Settings / Build Profiles, comenzando por MainMenu.
4. En la raíz del repo, clic derecho sobre **CrearAccesoDirecto.ps1 → Ejecutar con PowerShell**.
   En Windows 11 puede aparecer dentro de **Mostrar más opciones**.
5. Abrir **Rally** desde el Escritorio.

El ejecutable siempre queda en **`Build/Windows/Rally.exe`**. El script de PowerShell
encuentra el proyecto desde su propia ubicación, incluso si se clonó en otra carpeta,
y crea/reemplaza **Rally.lnk** en el Escritorio real del usuario (también si está en OneDrive).
Usa el ícono del ejecutable y su carpeta como directorio de trabajo.

El log de cada generación queda en **`Build/Windows/BuildLog.txt`**, con fecha y hora,
versión de Unity, escenas, resultado, duración y cantidad de avisos/errores.
**`BuildInfo.txt`** también registra la fecha del ejecutable generado.
Los builds posteriores actualizan la misma carpeta. Si el juego está abierto, cerrarlo
antes de reconstruirlo para que Windows permita reemplazar sus archivos.

Si hay errores de compilación, falta una escena, hay una escena sin guardar, Input Handling
no está en Both o falta Windows Build Support, el script detiene el proceso y explica
el motivo. Si el build falla, no usar un ejecutable anterior o parcialmente escrito:
revisar el resultado del log y generar nuevamente hasta obtener el mensaje de éxito.

Para compartir el juego, copiar **toda la carpeta `Build/Windows`**, incluyendo
`Rally_Data`, `UnityPlayer.dll` y las demás carpetas/DLL, no solo Rally.exe.
Los binarios generados y los logs están excluidos de Git; Pull trae el proyecto y los
scripts, y el build se genera localmente.

Para automatizar el acceso directo sin esperar Enter:

```powershell
.\CrearAccesoDirecto.ps1 -NoPause
```

Los scripts no modifican la física, el trazado, checkpoints ni waypoints.
