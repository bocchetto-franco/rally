# Escape, lluvia y atardecer — 2026-10-20

## Alcance

Cambios de presentación independientes. No se cambian circuitos, TerrainData,
colliders, checkpoints, waypoints ni parámetros/algoritmos de física.
Circuit_02 conserva su ambiente. No se descargaron assets ni se escribieron shaders.

## 1. Escape (tres modelos, jugadores y bots)

`Assets/Scripts/RallyVehicleExhaust.cs` se instala al iniciar cada controlador.
Las dos propiedades de solo lectura de `JrsVehicleController` exponen aceleración
y marcha usando el input del dueño del auto (incluido bot o segundo jugador).
El efecto no escribe torque, fricción, Rigidbody ni controles.

- Humo gris azulado: 12 partículas/s a fondo, 30/s durante 0.45 s al levantar el
  acelerador o cambiar de marcha bajo carga; ráfaga de 7/5 partículas respectivamente.
- El gatillo arma la descarga por encima de 65% y la dispara al bajar de 20%,
  incluso si la liberación analógica tarda varios frames. Cooldown de 0.35 s.
- Petardeo opcional: dos partículas anaranjadas de 0.06–0.12 s al soltar desde
  60 km/h. No se agregan luces, audio ni explosiones físicas.
- Desactivado por debajo de 4 km/h, volcado, pausa, carga de pista, controlador
  deshabilitado o fuera de vista/rango (80 m). Un teleport limpia el humo anterior.
- Máximo por auto: 40 partículas de humo + 6 de destello. Se crean antes de la
  precarga de carrera, reutilizando textura/shader del humo existente.
- Boquilla medida en los vértices del escape de cada modelo (metros locales):
  Lancia (−0.45, 0.20, −1.855), Peugeot (0, 0.275, −1.82), Audi (0.25, 0.275, −1.98).
  La presentación guardada antigua copia esa metadata del prefab al iniciar;
  `nozzleOffset` permite afinar la posición visual sin mover ruedas ni colliders.

## 2. Bosque: lluvia y camino mojado

Escena `Assets/Scenes/Circuit_03.unity`, objeto **Circuit 03 - Rain Profile**.
Perfil: `Assets/Art/Atmosphere/Circuit03 - Rain.asset`.

- `RallyCameraRain` crea una caja de lluvia 26 × 28 m delante/encima de cada cámara.
  Partículas en espacio mundial, vida 0.85 s, sin colisión, luces ni sombras.
- Máximo 480 gotas para una vista o 300 por vista en 2P (600 total). El emisor sigue
  la cámara; un salto de cámara limpia las partículas. Los callbacks de URP hacen
  que cada vista renderice únicamente su lluvia, sin mezclar emisores de jugadores.
- Fog lineal de 55 a 290 m, gris azulado. No afecta a Canvas/HUD ni avisos de curva.
- Material `Assets/Art/Atmosphere/Forest road - rainfall.mat`, copia independiente
  del original: color base (0.60, 0.67, 0.61), smoothness 0.52, metallic 0.
  Conserva albedo, normales y AO. Una película de humedad uniforme reemplaza solo
  la rugosidad seca; reflejos ambientales existentes, sin SSR/cámaras de reflejo.
- La asignación de materiales ocurre una sola vez al entrar en carrera y se restaura
  al desactivar el perfil. No se modifica el material original compartido.
- Se conserva el cielo nublado ya importado. Sonido de lluvia opcional no añadido.

## 3. Desierto: perfil de atardecer

Escena `Assets/Scenes/Circuit_01.unity`, objeto **Circuit 01 - Sunset Profile**.
Perfil: `Assets/Art/Atmosphere/Circuit01 - Sunset.asset`.

- Sol a 16° de elevación, orientación −38°, intensidad 1.25, color (1, 0.72, 0.46),
  fuerza de sombras 0.85. No aumenta resolución/distancia de sombras del presupuesto
  de rendimiento existente; la segunda vista conserva sus restricciones actuales.
- Cielo procedural existente clonado: `Sunset - warm horizon.mat`, tonos cálidos,
  tinte rosado, atmósfera 1.4, exposición 1.1.
- Fog cálido de 140 a 440 m. Luz ambiental Trilight cálida para terreno y vegetación,
  sin teñir destructivamente las texturas ni oscurecer el camino cercano.
- Para apagar el atardecer, desactivar **RallyCircuitAtmosphere** en el objeto del
  perfil. En Play restaura inmediatamente sol, cielo, ambiente y niebla anteriores.
  También puede guardarse deshabilitado para iniciar futuras carreras sin el efecto.
  El perfil se aplica en Play, no modifica la iluminación de la Scene View en edición.

## Compilación y rendimiento

`RallyRaceLoading` precalienta los sistemas/materiales junto con el resto de la
carrera. No hay búsquedas de escena, creación de materiales ni instanciación por
frame. El escape revisa visibilidad a 4 Hz; la lluvia solo mueve emisores y filtra
el render por cámara. Ningún volumen pesado nuevo.

Graphics Settings conserva explícitamente las variantes de fog, incluida Linear,
porque los perfiles se activan en runtime: el stripping automático podría quitarlas
al examinar escenas guardadas con Exp2. Ver la
[referencia oficial de Graphics Settings](https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/editor-settings-reference/comp-manager-group/class-graphics-settings).

Herramientas: `Tools/RallyGraphicsSetup.cs` (instalación/comparación de componentes),
`Tools/RallySelectionUxCheck.cs → RunAtmosphere` (Play, perfiles, escape y 1P/2P).
Informes en `Logs/graphics-atmosphere-setup.txt` y `Logs/graphics-atmosphere-play.txt`.
Las pruebas restauran escena, preferencias, input y estado de Play del usuario.
Verificación final de Play: **COMPLETE: PASS** para C01/C02/C03 en 1P y C01/C03 en 2P.
Se comprobó input visual independiente por auto con muestras controladas, colocación
de boquilla, ausencia de emisores duplicados, humo/cambio de marcha/petardeo, apagado
detenido/volcado, lluvia acotada, material húmedo realmente asignado, restauración al
apagar perfiles y ausencia de cambios de clima en C02. No equivale a probar mandos físicos.
El build reproducible se prepara con `Build → Windows Release`; ver `BUILD_WINDOWS.md`.
No se realizó un benchmark de FPS.
