# Polvo y salpicaduras más visibles

## Ajustes visuales

- `Assets/Scripts/RallyVehicleDust.cs`: base máxima de polvo 16 → 24 partículas/s por trasera; aporte de giro 36 → 48; techo 50 → 70. Tamaño por velocidad 0.18–0.68 → 0.22–0.85 m, alpha 0.14–0.23 → 0.24–0.38. Conserva crecimiento por giro ×1–1.65, filtros y vida máxima de 1.5 s. Pool 96 por trasera y 24 por delantera: 240 por jugador, sin emisores adicionales.
- `Assets/Scripts/RallyPuddleSlowZone.cs`: multiplicador visual `splashDensityMultiplier=2.2`; entrada aproximadamente 27–48 gotas, continuo 17.6–79.2 gotas/s según velocidad. Umbral 20 km/h; migra solo el antiguo valor exacto de 35, preservando otros valores personalizados. Vida 0.4–0.8 s, tamaño 0.11–0.31 m, salida 3.4–7.4 m/s y máximo 192 partículas por charco. Se reutiliza material/shader URP existente.
- El agua se emite a 8 cm sobre la superficie visible del charco (bounds de su Renderer, cacheado en Awake), no debajo del centro de masa bajo del auto. Esto evita que el camino oculte gotas recién emitidas.
- `Assets/Scripts/RallyPuddleMudVisual.cs`: multiplicador visual `sprayDensityMultiplier=2`; conserva el valor serializado de 12 partículas/s por rueda, ahora hasta 24 efectivas. Gotas de 0.06–0.18 m, vida 0.4–0.8 s y dispersión lateral/vertical mayor. Pool 160 por charco; continúa exigiendo contacto real de cada rueda con la zona húmeda. No emite con carrocería volcada ni Rigidbody cinemático.

Los cambios son compartidos por los charcos existentes de los tres circuitos. No se modificaron escenas, tamaños/posiciones/triggers, fricción, dirección, masa, downforce, frenado ni aquaplaning. No hay texturas/shaders nuevos, instanciaciones por cruce ni módulos de colisión en partículas. El aumento de densidad tiene más coste visual que antes, acotado por los pools; no se midió FPS comparativo.

## Pruebas

- `Tools/RallyDustTrailCheck.cs`, `Run`, `Logs/dust-trail-check.txt`: **COMPLETE: PASS**. Física/contactos aislados: polvo a 80 km/h recto 19.37 partículas/s por trasera; girando al máximo 67.37. Agua a 25 km/h 28 gotas de entrada; a 90 km/h 44, más 32 continuas en 0.5 s. Cuatro ruedas apoyadas emiten 43 gotas de barro en 0.5 s a 90 km/h. Gates de pausa/vuelco/aire/fuera de camino/cámaras y pools pasan; resistencia del agua sigue en 7.5 m/s² a 110 km/h.
- `Tools/RallySelectionUxCheck.cs`, `RunDustAndWater`, `Logs/dust-water-play.txt`: **COMPLETE: PASS / RESTORED**. Play real de Circuit_01 con Audi 1P y Lancia/Peugeot 2P; gamepads virtuales, velocidades y poses controladas. Se verificó el giro independiente, emisión/tamaño y el cruce real de triggers/contactos para cada jugador: picos de 42–43 gotas de agua y 16–17 de barro por cruce a unos 40 km/h. No es vuelta completa, hardware físico ni benchmark.
- Capturas `Logs/Wet_Effects_1P_J1.png`, `Wet_Effects_2P_J1.png`, `Wet_Effects_2P_J2.png`; polvo en `Logs/Dust_Steering_*_After.png`. Las pruebas restauran escenas, preferencias, mandos, pausa y timeScale sin guardar poses temporales.

El build `Windows_2026-10-19` anterior a esta ampliación conserva el polvo de la primera corrección. No confundirlo con la nueva salida `Windows_2026-10-19_Effects`.

## Ejecutable actualizado

- `Build/Windows_2026-10-19_Effects/Rally1.exe`: **Succeeded**, cero errores/cuatro advertencias, 9 min 1 s y 637,619,551 bytes reportados. Windows x64 Release Mono, versión 0.1.0, 1280×720, input Both, seis escenas desde MainMenu.
- Advertencias: herramienta de automatización Pipeline sin RuntimePipelineConfig (no es URP); directiva obsoleta de TextMeshPro; seis mallas ProBuilder sin pre-baked collision (Unity prehornea en este build); desconexión de DualSense registrada durante la construcción. ILPP también resolvió automáticamente los dos avisos conocidos de referencias duplicadas.
- Verificados PE x64 y campos nuevos de agua/barro/polvo en Assembly-CSharp del build; créditos distribuidos idénticos al canónico. SHA-256 de `Rally1_Data/Managed/Assembly-CSharp.dll`: `3C16565683E026BD6371AC8723923C665B375C07BDDE3A65A1A1E6D0BB3B9304`.
- Informe completo `Logs/windows-build-2026-10-19-effects.txt`, también `BuildReport.txt` junto al EXE. PlayerSettings y cachés generadas restaurados, respaldo `Logs/SceneBackups/WindowsBuild_20261019_002707/`. No se cerró ni sobrescribió el juego anterior abierto por el usuario. Mantener Data y DLL junto al ejecutable. No se ejecutó una vuelta completa en este artefacto nuevo.
