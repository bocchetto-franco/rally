# Correcciones de selección, aceleración y vibración — 29/10/2026

- Eliminados los tres bloques de texto rodeados en rojo en selección local: explicación, guía general de teclado y etiquetas de dispositivo de cada jugador. La detección de dispositivos, selección individual y confirmación siguen funcionando.
- Eliminado el aviso visual de agua y su búsqueda de proximidad. Los triggers y efectos físicos/visuales de los charcos se conservan. El HUD de turbo permanece.
- Dirección compartida: 55 grados y respuesta 96 (antes 52/82); bots conservan su dirección propia. Valores serializados en las tres escenas y en RallyVehicleDynamics.
- El autofreno anterior empezaba al 15% de giro, usaba una curva raíz cuadrada y podía reducir el torque motor hasta un 95%. Ahora no interviene hasta el 60% del giro, la transición es SmoothStep, el objetivo a giro máximo sube de 60 a 85 km/h y el máximo de autofreno baja de 0,55 a 0,25. Nunca recorta el torque de aceleración. El freno manual mantiene su fuerza.
- JrsVehicleController sobrescribía dirección/torque/frenos en Update además de FixedUpdate; los rigs con RallyVehicleDynamics ahora escriben fuerzas únicamente en física. El vehículo original sin ese componente mantiene su ruta de código anterior. Las ruedas visuales se colocan una vez en LateUpdate y se ajustan a la pose interpolada del chasis.
- JrsFollowCamera sigue la pose interpolada en LateUpdate en vez de saltar al ritmo de FixedUpdate; integración con subpasos limitados. RallyCameraRoadShake ejecuta después de la cámara, no interpreta las oscilaciones normales de suspensión como golpes y reduce sus valores serializados de 0,09m/0,4 grados a 0,025m/0,12 grados, conservando efecto sutil en agua/banquina.
- No se modificó masa, fricción, downforce, terreno, checkpoints, waypoints, semáforo ni perfil de bots. Respaldos en Logs/HandlingFix_20261029/Before.
- Por instrucción posterior del usuario, NO generar ni actualizar el build. Estos cambios son del proyecto; el ejecutable anterior sigue siendo anterior a esta revisión.

Validación: consultar Logs/HandlingFix_20261029/check.txt y Editor.log. Solo declarar aprobada si termina COMPLETE: PASS. Las comprobaciones automáticas no equivalen a una vuelta manual ni certifican la percepción de vibración en hardware real.

Resultado: Unity 6000.6.0f1 terminÃ³ con salida 0; check.txt COMPLETE: PASS para los tres circuitos, HUD 1P/2P, ausencia de autofreno en giro <=60%, torque motor preservado y configuraciÃ³n de interpolaciÃ³n/direcciÃ³n. preservation.txt confirma datos ajenos al alcance conservados. No se ejecutÃ³ BuildPipeline ni se modificaron ejecutables. La percepciÃ³n visual de vibraciÃ³n y el feeling requieren prueba de conducciÃ³n.
