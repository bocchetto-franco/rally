# Créditos integrados y verificados — 2026-10-10

## Estado final (reemplaza las notas provisionales siguientes)

Pantalla integrada mediante RallyMenuCredits/RallyCreditsScroll y botón Créditos en MainMenu. Copias Resources/Credits/Credits.txt y StreamingAssets/CREDITS.md idénticas al documento raíz; 48 documentos de avisos/inventarios en StreamingAssets/Licenses. RallyCreditsSetup sincroniza al cargar el editor o Tools > Rally > Sync Credits and License Notices; prebuild rechaza texto desactualizado sin importar Assets durante BuildPipeline.

Logs/credits-play-test.txt COMPLETE: PASS: intro real, D-Pad + Cruz abre Créditos, scroll con D-Pad mantenido/PageDown/Home/End/rueda, vuelta con Cruz/Círculo/Escape/pointer y autores/advertencias presentes. Capturas Menu_WithCredits/Menu_Credits_Top/Menu_Credits_Assets revisadas, sin superposición. Gamepad virtual, no hardware real. Runtime/editor 0 errores/0 warnings; Unity recompilación terminada y consola actual groundTruth 0/0, editor ready/fuera de Play.

Assets liberado para segunda build Windows tras esta prueba; primera build no incluía Créditos. No implica prueba del ejecutable nuevo; ver Docs/Windows_Test_Build.md. No se modificaron escenas/física; cambios de renderer/TMP/build paralela preservados. AGENTS.md documenta arquitectura y advertencias. Tools/RallyCreditsUI.patch temporal se eliminó una vez aplicada; implementación definitiva está en Assets.

Porsche sigue sin licencia verificable (usuario confirmó no disponer de fuente), fuente Electronic Highway Sign y avisos internos de Graph Authoring/Burst shim también pendientes. No asumir permiso para distribuir esos recursos ni borrar/reemplazar sin pedido.

## Completed

- Canonical root CREDITS.md: 44 entries, authors, source URLs, exact license terms and adaptation notices.
- Tools/AuditCredits.ps1: 1,419 assets inventoried, no unmapped media/native assets; 12 unclear files (Porsche and Electronic Highway Sign font/example atlas).
- Tools/AuditPackageLicenses.ps1: 63 installed packages inventoried; unresolved internal package notices explicitly flagged.
- Docs/AssetLicenseInventory.csv, PackageLicenseInventory.csv and CreditsEvidence.json.
- Full Apache 2.0 and JRS MIT notices under Docs/Licenses.
- User confirmed they do NOT have the original Porsche source/license. Do not assume permission; do not delete/replace the vehicle in this task.

## Histórico de preparación (ya aplicado/probado; no repetir)

- Tools/RallyCreditsUI.patch contains the complete proposed menu, scrolling and editor synchronization patch. Apply with apply_patch only after the concurrent build releases Assets. Root CREDITS.md is canonical; Resources/Credits/Credits.txt and StreamingAssets/CREDITS.md are synchronized copies, plus full notices in StreamingAssets/Licenses.
- Tools/RallyCreditsPlay.cs is an external Pipeline test covering virtual gamepad/keyboard/pointer navigation, scroll, attribution text and source parity. No PASS report exists yet.
- After applying: refresh Unity, compile both assemblies, run RallyCreditsSetup.Sync outside Play/build, run external test, visually review Logs/Menu_WithCredits.png and Menu_Credits_*.png, check console and preserve scene/physics files. Update AGENTS.md only with verified results.

## Histórico de coordinación (bloqueo ya levantado)

Another authorized thread (01a095fe-2956-7693-b337-acfd37f8745b) is running BuildPipeline on this same project. It explicitly requested no Assets/ProjectSettings edits until completion. At last check the Windows build was still compiling Terrain shaders (95/384 variants); do not interfere or infer completion from an exe appearing. Check Logs/windows-test-build.txt for completed result and coordinate editor ownership before applying.

Existing dirty Unity fonts, URP/ProjectSettings and TMP example fixes belong to the build thread/editor; preserve them. No gameplay, physics or scenes were changed by this credits audit.
