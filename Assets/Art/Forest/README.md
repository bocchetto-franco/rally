# Circuit_03: bosque de montaña

Escena: `Assets/Scenes/Circuit_03.unity`.

- Recorrido cerrado ProBuilder, dos horquillas de 180° (radios 28 y 22 m), chicana, sucesión de curvas y una sola recta larga de 130 m.
- Tres elevaciones suaves de +20, +28 y -10 m respecto a la salida, con transiciones continuas.
- Cuatro charcos de 6 × 10 m: depresiones máximas de 0.28 m. Posiciones, distancias y ancho del camino en `Circuit03/Circuit03Layout.json`.
- Público Quaternius: cuatro grupos de 20, combinados por material. Deben permanecer completamente fuera de las barreras invisibles; el generador verifica margen del cuerpo completo.
- Fardos visuales ProBuilder en las curvas. Sin colliders ni Rigidbody.
- Sin checkpoints, timer, lógica de agua ni resultados. El Porsche conserva sus ajustes originales de Circuit_01 y queda en la largada para pruebas.

## Abrir y probar

Abrir la escena para editar. Para conducir sin pasar por el frontend, usar **Tools → Rally → Play Circuit 03 - Free Drive**. Al salir de Play se restaura la escena de inicio anterior; el menú existente no se modifica ni se agrega todavía la pista a su selección.

## Assets

Consultar `ASSET_SOURCES.md` y `DOWNLOAD_MANIFEST.json`. Suelos y cielo de Poly Haven; coníferas low-poly y plantas de Kenney Nature Kit. Todos gratuitos/CC0. Ninguna textura árida de Circuit_01/02 se usa en el nuevo terreno.

Materiales URP: `Materials/`; capas: `Terrain/`; modelos descargados: `KenneyNature/`; prefabs preparados: `Prefabs/`. Texturas importadas comprimidas, máximo 2048. Vegetación con mallas/materiales compartidos, GPU instancing habilitado y LOD de descarte a distancia; no se ha medido un benchmark de FPS.

## Edición y reconstrucción

`Assets/Editor/Circuit03Builder.cs` construye la pista de forma explícita y se niega a sobreescribir una escena existente. `Circuit03ForestAssets.cs` prepara solo los assets nuevos. No ejecutar herramientas de regeneración de Circuit_01 sobre esta pista ni reemplazar su TerrainData por el compartido del desierto.

La prueba automática comprueba cierre, apoyo del camino/collider, depresiones, distancia del público, ausencia de sistemas de carrera y conservación de la física del Porsche. Las vistas renderizadas de revisión quedan en `Logs/Circuit03_Preview_*.png`.
