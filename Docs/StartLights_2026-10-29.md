# Semáforo compartido de salida

Actualización posterior: el build final sí fue generado en `Build/Windows_StartLights_20261029/Rally.exe`, con 0 errores/1 aviso, e incluye la protección contra frames lentos. Detalles en `Docs/WindowsStartLights_2026-10-29.md`. Las referencias al build pendiente más abajo corresponden al cierre inicial de implementación.

`RallyRaceFlow.Awake` instala una sola instancia de `RallyRaceStartLights` por carrera. Los tres circuitos ya contienen Race Flow; no es necesario reconstruir ni guardar sus escenas para instalarlo.

Cinco luces rojas, encendido secuencial cada 0,8 s; todas permanecen rojas 0,7 s y cambian juntas a verde. El verde permanece 3 s de juego y luego se destruye el Canvas. Los parámetros están serializados en el componente. La primera luz enciende después de 0,8 s, por lo que la largada ocurre a los 4,7 s.

La precarga normal entrega el control al semáforo de forma sincrónica, antes del próximo paso físico. Durante el rojo se conserva `Time.timeScale = 0`, se bloquean inputs mediante la puerta existente y se pausa audio; también se impide registrar checkpoints. La pausa de carrera no puede liberar accidentalmente esa puerta. El cambio a verde restaura audio y pone `Time.timeScale = 1`. La lógica existente del cronómetro sigue iniciándolo al cruzar la salida.

Canvas overlay compartido, una sola fila de cinco discos, arriba en el centro de la pantalla completa también en 2P. No se modifican los HUD por jugador, ni grilla, tuning, trazado, waypoints o definición de checkpoints. Los discos se crean una vez y se liberan al abandonar la escena; no requieren assets descargados.

Verificador opt-in `Assets/Editor/RallyStartLightsCheck.cs`, entrada `RallyStartLightsCheck.Run`, argumento `-rally-start-case N` (0–5: C01/1P, C01/2P, C02/1P, C02/2P, C03/1P, C03/2P). Cada caso se ejecuta en un proceso limpio y carga el circuito desde MainMenu con `SceneManager.LoadScene`, reproduciendo el evento de inicialización de split. Los informes son `Logs/start-lights-case-N.txt`. Usa `-rally-no-load-prep`: verifica la fase de largada en Play sin depender del renderizado de shaders en batch; no valida el calentamiento de cámaras ni hardware físico. El fixture envía aceleración mediante dos gamepads virtuales y comprueba roster, poses congeladas, bloqueo de checkpoints, secuencia, Canvas único, verde, desbloqueo y movimiento posterior. Los primeros intentos en `start-lights-attempt*.txt` son historial fallido del fixture y no validaciones aprobadas. Consultar los seis informes actuales antes de afirmar aprobación completa.

El build de otra tarea es independiente de esta prueba; no asumir que un ejecutable incluye el semáforo sin verificar su ensamblado o fecha de compilación.

## Resultado de cierre

Los seis casos individuales terminan **COMPLETE: PASS**, con preferencias y opciones de Play restauradas. Consolidado: `Logs/start-lights-check.txt`. Se verificaron cinco rojos secuenciales, inmovilidad de todos los corredores contra aceleración virtual, checkpoints bloqueados, un solo Canvas, cinco verdes simultáneos, liberación de física/input, ocultación tras 3 s y movimiento posterior en C01/C02/C03 en 1P/2P. La prueba no incluye conducción humana, hardware real ni evaluación visual de GPU. Unity compiló los scripts y salió de los seis procesos con código 0. El build UX anterior no se regeneró después de la protección final de avance de luces.
