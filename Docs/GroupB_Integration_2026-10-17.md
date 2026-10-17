# Flota Grupo B — 17 de octubre de 2026

Se reemplazaron Porsche/BMW por Lancia Delta S4/Peugeot 205 T16 Evo 2. Audi Quattro S1 conserva modelo, materiales, escala y preview originales. La diferencia de proporciones fue aceptada explícitamente por el usuario después de revisar los candidatos.

## Dimensiones verificadas

Bounds completos de MeshRenderers, medidos en Unity 6000.6.0f1, metros:

| Modelo | Largo | Ancho | Alto |
|---|---:|---:|---:|
| Lancia Delta S4 | 3.95553 | 1.96228 | 1.49530 |
| Peugeot 205 T16 | 3.76026 | 1.80000 | 1.48384 |
| Audi Quattro S1 | 4.25000 | 1.70626 | 1.69345 |

Lancia conserva escala original; Peugeot tiene escala uniforme 0.9721383795 horneada en FBX. Ninguno se deformó por eje. Los modelos nuevos tienen carrocería y cuatro ruedas separadas, pivotes de controlador y material URP Lit con atlas original comprimido/mipmaps; no se generó geometría de autos ni texturas nuevas.

## Fuente y licencia

Los dos provienen de [1965–2002 Rally Cars](https://sketchfab.com/3d-models/1965-2002-rally-cars-2146acc1e3de409baf46d03a070d0526), publicado por DisneyCars (supercarmodels) bajo CC-BY 4.0. Se conservan atribución, vínculo de licencia, hash del ZIP y modificaciones en cada `SOURCE.md`, CREDITS.md, créditos en juego y copias distribuidas. La licencia es la declaración del publicador, no una verificación independiente de procedencia ni de marcas/logotipos.

## Integración

- `Assets/Art/Vehicles/LanciaS4/` y `Peugeot205T16/`: FBX, textura y material.
- `Assets/Resources/Vehicles/`: prefabs visuales para carrera, sin física adicional.
- `Assets/Resources/MenuVehicles/`: previews combinados por material, sin scripts ni física; el menú carga únicamente la elección visible de cada jugador.
- Selección, migración de preferencias guardadas, 2P, bots aleatorios y precarga de carrera usan la nueva flota. Los tres bots siguen escogiendo sin repetición.
- Se conservan Rigidbody, colliders de carrocería y tuning del rig de jugador/bot: masa, inercia, damping, fricciones, suspensión, dirección, motor, frenos, downforce y ayudas. Solo posiciones/radios de WheelColliders se adaptan a las ruedas medidas.
- Las escenas guardan el Lancia predeterminado. Los identificadores internos históricos Porsche del rig/dinámica permanecen para no romper referencias; no significan que quede un modelo Porsche oculto.
- Se actualizaron las tres pistas, escena de pruebas, escena AMR, copia de recuperación y Bot_Car. Se conservaron los transforms históricos enlazados y se retiraron sus Mesh/materiales anteriores. No se modificó la física del asset ni el auto de policía original.
- Modelos Porsche/BMW y previews retirados de Assets tras comprobar cero dependencias sobrevivientes. Copia recuperable en `Logs/SceneBackups/GroupB_20261017/`, además de papelera/historial Git.

## Verificación

- `Logs/group-b-static.txt`: PASS. Bounds, cuatro ruedas ordenadas, materiales válidos, intercambio repetido de los tres modelos en C01/C02/C03 y Bot_Car sin cambios de tuning, ruedas alineadas, sin scripts faltantes.
- `Logs/GroupBCandidates/rig-migration.txt`: PASS. Comparación de tuning antes/después en los siete rigs migrados.
- `Logs/GroupBCandidates/retired-assets.txt`: PASS. Retiro condicionado a cero dependencias de assets sobrevivientes.
- Comparación de bloques YAML contra el respaldo de las tres pistas: ningún bloque original eliminado. Cambios limitados a 2 MonoBehaviours del rig, 4 WheelColliders, 5 Transforms y MeshFilter/MeshRenderer de la presentación anterior; terreno, recorrido, cámaras, luces, checkpoints y waypoints originales sin cambios.
- `Tools/AuditCredits.ps1`: 1382 archivos inventariados, cero sin documentar; quedan dos entradas históricas de licencia incierta ajenas a estos autos. No se declaran autorizadas ni se borran sus avisos.
- Capturas reales de menú `Logs/GroupB_Menu_0/1/2.png` y `Logs/SelectionUX_2P.png`: modelos texturados y nombres actuales, preview independiente por jugador, navegación con mandos virtuales y selección persistente.
- `Logs/group-b-races.txt`: **COMPLETE: PASS / RESTORED** en las seis cargas C01/C02/C03 × 1P/2P. Modelos seleccionados, cuatro ruedas enlazadas/radios correctos, contacto con grilla, orientación estable, aceleración por gamepads asignados; tres bots con su masa separada en 1P y cero en 2P. Capturas `Logs/GroupB_Race_{0,1,2}_{1,2}.png`. Se restauraron preferencias, dispositivos/InputSettings, escenas, pausa y timeScale.
- El primer ensayo de menú/carrera (`group-b-play.txt`) completó menús/créditos/previews y se detuvo en una aserción del verificador: buscaba el controlador del bot en el padre en vez del hijo. Se corrigió a `GetComponentInChildren` y se repitió toda la matriz de seis carreras con éxito; no era un fallo del juego.
- Recompilación final de runtime/editor tras corregir las referencias de precarga: completada sin errores. Herramientas externas de comprobación compilaron sin diagnósticos. El Audi no presenta cambios en sus archivos de modelo/materiales/prefab/preview.

Las pruebas son del Editor. No se generó ni publicó un build Windows nuevo; el ejecutable anterior no contiene estos cambios. No equivalen a mandos físicos ni a una vuelta competitiva completa.
