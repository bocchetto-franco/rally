# Audio de Rally — CC0

Los cinco audios estan publicados por sus autores con **CC0 1.0 Universal**:
https://creativecommons.org/publicdomain/zero/1.0/

No se requiere atribucion; se conserva la procedencia para auditoria y futuras
sustituciones. Licencias verificadas en las paginas originales al importar.
URLs de descarga y SHA-256 en `DOWNLOAD_MANIFEST.json`.

| Uso | Archivo | Obra / autor | Fuente |
| --- | --- | --- | --- |
| Motor | EngineLoop.wav | Sedan engine loop — Dmitry_mansurev64 | https://freesound.org/people/Dmitry_mansurev64/sounds/748027/ |
| Derrape | TyreSkid.wav | screeching tyres / tires — johnnydekk | https://freesound.org/people/johnnydekk/sounds/614627/ |
| Impacto | Impact.wav | Collision — qubodup | https://freesound.org/people/qubodup/sounds/332058/ |
| Menu y seleccion | MenuMusic.ogg | Racing Game Title — MintoDog | https://opengameart.org/content/racing-game-title |
| Carrera | RaceMusic.ogg | Hot Roadway — MintoDog | https://opengameart.org/content/hot-roadway |

## Preparacion

Freesound ofrece los originales con inicio de sesion y versiones publicas de
escucha HQ MP3. Se usaron esas versiones HQ publicas bajo la misma licencia CC0;
no se accedio a descargas restringidas. Se convirtieron a WAV mono de 44.1 kHz,
16 bits, se recorto silencio y se ajusto el nivel. Motor y derrape tienen un
fundido circular de 80 ms para disimular la union del loop. Derrape usa un
fragmento de 2 segundos de mayor energia de la grabacion; es foley de goma,
no una grabacion de un auto derrapando. El impacto es un golpe mecanico generico.
La musica instrumental Ogg se conserva sin modificar.

Reproduccion reproducible: `Tools/prepare_rally_audio.py` descarga y prepara los
clips con Python e `imageio-ffmpeg`. Cache de originales en `Temp/RallyAudio`
(no necesaria para compilar ni ejecutar el juego).

## Integracion y ajustes

- Mixer: `Assets/Resources/Audio/RallyAudio.mixer`.
- Canales: **Motor** (-3 dB), **Efectos** (-4 dB), **Musica** (-12 dB).
- Volumenes expuestos: `MotorVolume`, `EfectosVolume`, `MusicaVolume` (dB).
- Biblioteca/ajustes: `Assets/Resources/Audio/RallyAudioLibrary.asset`.
- `RallyVehicleAudio` reutiliza el AudioSource de motor y agrega dos fuentes
  para derrape/impactos solamente en el jugador. El controlador mantiene su
  pitch de 0.65 a 1.55 entre 0 y 140 km/h, con interpolacion de 5/s.
- El derrape lee el estado real del humo trasero en `RallyVehicleDynamics`:
  comienza con slip >= 0.18, termina bajo 0.12, requiere contacto y 25 km/h.
- Los impactos usan `OnCollisionEnter`, velocidad normal minima de 2 m/s,
  volumen pleno a 14 m/s y enfriamiento de 0.2 s. Los apoyos de suelo y los
  triggers no disparan golpes.
- `RallyAudioSystem` persiste entre escenas y cambia de tema con fundido de
  0.8 s. Pausa/resultados detienen el audio del auto y bajan la musica de carrera.
- Musica: streaming Vorbis estereo; efectos cortos: PCM mono precargado para
  evitar latencia y artefactos al repetirlos. No se modifica ninguna fisica.
- `Tools > Rally > Install and Verify Audio` reinstala importadores y referencias
  conservando los ajustes existentes de biblioteca/mixer.

## Verificacion

Prueba automatizada en Play completada el 2026-10-01 en Unity 6000.6.0f1:
motor/musica y routing en Circuit_01, Circuit_02 y Circuit_03; pitch por
velocidad; derrape con slip real de WheelCollider; impacto fisico;
pausa/reanudacion; persistencia y limpieza entre escenas. Resultado:
`Logs/audio-play-test.txt` termina en `COMPLETE: PASS`.
Esto verifica la reproduccion programatica, no sustituye la escucha humana
para afinar el balance final de volumenes.
