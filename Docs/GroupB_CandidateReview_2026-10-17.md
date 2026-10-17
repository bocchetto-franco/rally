# Grupo B: evaluación previa al reemplazo

Estado: **reemplazo pendiente; ningún candidato aceptado ni conectado al juego**.
El usuario pidió detener la integración y avisar si los modelos no mantienen
proporciones similares al Audi, aproximadamente 4 m de largo y 1.7–1.8 m de ancho.
No se cambiaron física, escenas, selección, previsualizaciones, bots ni créditos
distribuidos. Tampoco se retiraron Porsche/BMW antes de contar con sustitutos válidos.

## Fuentes descargadas para inspección

- [Lancia Delta S4 — ericdzlpz](https://sketchfab.com/3d-models/lancia-delta-s4-cb7e2285b95543f4b3e1b562473fbd4d).
  La ficha y la API oficial indican CC-BY 4.0. Descarga original FBX, 332886 bytes,
  SHA-256 `1DC43378EAC0FCE7B4C916C3F6B80968F2B32AB2CD5E3194CE521E5D853DF6F0`.
  Cuatro ruedas separadas, pero con nombres genéricos pTorus y distinta escala entre ejes.
- [1965–2002 Rally Cars — DisneyCars / supercarmodels](https://sketchfab.com/3d-models/1965-2002-rally-cars-2146acc1e3de409baf46d03a070d0526).
  Publicado como CC-BY 4.0. Archivo original FBX, 8571667 bytes,
  SHA-256 `E6E9E5F64634E496C6F8626B4402568DB91427B21328A620789B152D6B96EAB8`.
  Contiene Lancia S4 y Peugeot 205 T16 Evo 2. Ambos tienen carrocería y cuatro
  ruedas separadas; sus nombres de ruedas necesitan normalización FL/FR/BL/BR.
  La página tiene un comentario que cuestiona similitud con Rally Legends;
  no demuestra extracción, pero no constituye prueba independiente de autoría.
  La licencia aquí consignada es la declarada por quien lo publicó.
- Se descartó el Peugeot 205 Pikes Peak de Tyler_Dave por ser la variante de 1987
  para subida de montaña, no el WRC 1985–86 solicitado.
- Otros Delta S4 encontrados (FreakyCat, Massola Racing) no habilitan descarga.

Los originales descargados están en Downloads; su extracción y los resultados
se conservan fuera de Assets, en `Logs/GroupBCandidates/`.

## Bounds medidos en Unity 6000.6.0f1

Bounds completos de MeshRenderers, incluyendo ruedas/espejos/alerones; metros.
Instancias aisladas en PreviewScene, con ejes Y vertical y Z longitudinal.
No se guardaron escenas ni assets existentes.

| Modelo | Largo | Ancho | Alto |
|---|---:|---:|---:|
| Audi actual, sin cambios | 4.250 | 1.706 | 1.693 |
| Delta S4 ericdzlpz, importación original | 0.09975 | 0.05482 | 0.04043 |
| Delta S4 pack, importación original | 3.956 | 1.962 | 1.495 |
| Peugeot 205 T16 pack, importación original | 3.868 | 1.852 | 1.526 |

Al llevar uniformemente el Delta de ericdzlpz a 4 m de largo queda en
2.198 m de ancho; al limitarlo a 1.8 m de ancho queda en 3.276 m de largo.
No es un simple error de unidades: su relación ancho/largo difiere del Audi.

El Delta del pack tiene mejores proporciones, pero a 4 m de largo queda en
1.984 m de ancho (16.3% más que el Audi). A 1.8 m de ancho queda en
3.628 m de largo y 1.372 m de alto, sensiblemente menor que el Audi.
El Peugeot puede quedar en 3.760 × 1.800 × 1.484 m con escala uniforme 0.97214.
Estos son candidatos de evaluación, **no dimensiones finales de autos integrados**.

Herramientas: `Tools/inspect_group_b_candidates.py` (inspección Blender, sin
autoexec) y `Tools/GroupBCandidateReview.cs` (Unity, entrada `Run`). Informe:
`Logs/GroupBCandidates/unity-bounds.txt`. Unity importa los FBX temporalmente en
una carpeta Editor y la envía a la papelera al terminar; los originales permanecen
disponibles. El Audi se mide sin modificar su prefab ni sus materiales.

Decisión posterior del usuario: «esta bien asi, no pasa nada», seguida de
«continua la tarea». Se aceptó el ancho de ~1.96 m del Delta del pack y la
escala uniforme propuesta para el Peugeot. No se estrechó solo X ni se estiró
solo Z. La implementación final y medidas verificadas están en
`Docs/GroupB_Integration_2026-10-17.md`; esta tabla conserva la evaluación original.
