# Polvo proporcional al volante — 2026-10-19

## Causa y corrección

La lectura del volante sí funcionaba. El antiguo multiplicador de giro ×2.6 multiplicaba una base demasiado dependiente de la velocidad: la frenada automática al doblar reducía esa base. En Play, una recta a 80 km/h emitía unas 13 partículas/s por rueda trasera, pero una curva a 35 km/h solo unas 9. Cambiar `main.startSize` tampoco alteraba las partículas ya vivas.

`Assets/Scripts/RallyVehicleDust.cs` lee en `LateUpdate` el promedio de los ángulos reales de los WheelColliders delanteros del propio auto. Normaliza su valor absoluto por `maxSteerAngle`, sin input global compartido. Ahora suma hasta 36 partículas/s por rueda proporcionalmente al giro, con entrada progresiva entre 8 y 35 km/h, además de la base por velocidad de 0.25–16. El techo es 50 por rueda. Actualiza explícitamente `emission.enabled`, `rateOverTime` y `sizeOverLifetime.sizeMultiplier` (1–1.65) en runtime. Este último afecta también las partículas existentes; `main.startSize` conserva la variación por velocidad para las nuevas.

Se reutilizan los cuatro emisores existentes y el material URP Particles/Unlit del asset. No se crearon shaders ni sistemas adicionales. Presupuesto máximo: 64 partículas por trasera y 20 por delantera, 168 por jugador. Se conservan los filtros de contacto real con camino/puente, parada, vuelco, vuelo, pausa, carga, finalización y visibilidad/distancia. No cambia física, asistencia, humo de derrape ni asignación de mandos.

## Verificación

- `Tools/RallyDustTrailCheck.cs`: `Logs/dust-trail-check.txt`, **COMPLETE: PASS**. Contactos y simulación aislados, velocidad/giro, expiración y gates; no equivale a una vuelta conducida.
- `Tools/RallySelectionUxCheck.cs`, entrada `RunDustSteering`: `Logs/dust-steering-play.txt`, **COMPLETE: PASS / RESTORED**. Play real de Circuit_01, Audi en 1P y Lancia/Peugeot en 2P, dos DualSense virtuales, cámaras y superficies reales. La prueba fija velocidades y restringe posición lateral/yaw temporalmente para comparar el efecto; no mide manejo, FPS ni hardware físico.
- Recta a 80 km/h: aproximadamente 13 partículas/s por trasera. A unos 35 km/h, recto: 3.6; giro parcial de 12°: 13.7, crecimiento ×1.18; giro fuerte de 42.5°: 38.3, crecimiento ×1.63. Al alternar el giro entre J1 y J2, solo cambian la emisión y el crecimiento del auto correspondiente.
- Capturas revisadas: `Logs/Dust_Steering_1P_After.png`, `Logs/Dust_Steering_2P_After.png`, `Logs/Dust_Steering_2P_J2_After.png`. El polvo conserva transparencia y tono tierra; no pretende convertirse en una nube opaca.

Las pruebas restauran preferencias, dispositivos/InputSettings, escenas abiertas, pausa y timeScale. Los cambios de steering 44°/respuesta 30 de otra tarea se preservan, no forman parte de este ajuste de partículas.
