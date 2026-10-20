# Experiencia de referencia y alcance actual

WZControl aportado es referencia de funciones y recorrido de uso. PCOptimizer conserva marca, WPF/C#, implementación y recursos propios. No se incorpora el código, instalador ni imágenes del ZIP sin una licencia de reutilización. La revisión estática está en [el informe técnico](research/WZCONTROL_REVIEW.md).

El **PR #10 apunta directamente a main** e incluye 0.6 y los incrementos posteriores; no necesita fusionar primero el #9. La rama `master` no existe en este repositorio. Ningún PR se fusiona automáticamente.

| Recorrido/capacidad del referente | Qué funciona en PCOptimizer | Qué falta para una experiencia comparable |
| --- | --- | --- |
| Inicio gaming con hardware y próximos pasos | Hardware real, objetivos desde Inicio, analizar PC, procesos, monitor, benchmarks y recuperación accesibles | Priorización derivada de diagnóstico/experimentos; no hay puntaje inventado de FPS/preparación |
| Objetivos competitivo/equilibrado/streaming | Cuatro objetivos incluyendo laptop; selección real revisable con captura/voz conservadas según contexto | Recomendaciones gráficas por juego/pantalla y evidencia local persistida |
| Ajustar y revertir Windows | 41 ajustes compilados, revisión de valores, snapshots, journal, punto Windows y restauración con conflictos | Más ajustes solo cuando exista soporte/evidencia; integración de fallos reales UAC/VM |
| En vivo | CPU global, memoria, sensores NVIDIA disponibles, selección por dispositivo/métrica, gráfica QPC, parar/cerrar y exportar | FPS en vivo continuo, GPU AMD/Intel, sensores CPU térmicos, memoria/GPU por proceso y overhead físico |
| Capturar y analizar FPS | PresentMon manual fijado, CSV, estadísticas, sesiones, comparación por repeticiones, sensores correlacionados e informes | Flujo de experimentos/intervenciones más integrado y calibración real de Warzone |
| Procesos que consumen recursos | CPU/E/S por PID+creación, disponibilidad, contexto streaming/voz, JSON reabrible e informe | Vinculación temporal con frametimes y mejor atribución; no se termina ningún proceso |
| Configuración gráfica COD | Perfil experimental de hardware y checklist; no se editan archivos del juego | Parser propio por versión, revisión, respaldos y reversión conflictiva de `.txt0/.txt1` con validación real |
| Drivers y perfiles NVIDIA/AMD | Inventario, fuentes oficiales, ofertas WUA y NVML de solo lectura | Recomendación verificable por juego/versión y APIs soportadas de perfiles |
| Red del juego/bufferbloat | ICMP, pérdidas observadas, adaptadores y DNS voluntario | Carga controlada y diagnóstico por juego con consentimiento; no se capturan paquetes ni geolocalizan IPs automáticamente |
| Control, overlay y latencia de entrada | Inventario/audio y contexto GameSir; sin macros ni inyección | Proveedores documentados, medición válida, accesibilidad y compatibilidad legítima de overlay/anticheats |
| Mantenimiento e instalación | WinGet exacto, 236 apps, limpieza selectiva, salud de almacenamiento cuando Windows la expone | Más cobertura de hardware y validación real; no se predice vida exacta del SSD ni EQ universal |

## Usar el monitor

En Inicio o Uso de recursos pulsa **Recursos en vivo**, luego **Iniciar monitor**. Elige una serie; cada adaptador conserva índice/nombre NVML y fuente, sin asignarlo automáticamente al juego. CPU es global y memoria es uso físico, no presión/paginación demostrada. Los valores ausentes son N/D; las consultas mayores de 250 ms no se muestran como instantáneas utilizables. Una lectura de más de cinco segundos se marca antigua mientras el monitor está activo. La ventana informa timestamp UTC y agregación conocida/desconocida.

El monitor reutiliza el proveedor de la captura de benchmarks, con observaciones QPC y espera solicitada de un segundo después de cada ciclo. Las consultas lentas reducen la cadencia; no existe garantía de muestreo exacto. NVML mantiene globalmente una sola llamada nativa pendiente en el proceso: cancelar no puede abortarla, pero no crea otras simultáneas al reiniciar ventanas. No se instalan componentes ni drivers.

**Parar** congela la traza; cerrar durante el sondeo solicita parar y espera al cierre cooperativo antes de cerrar la ventana. Hay parada automática tras diez minutos. La aplicación bloquea cambios simultáneos del sistema mientras mide. **Parar y medir FPS en Benchmarks** termina el monitor y abre el módulo real de PresentMon. No se muestran FPS estimados en el monitor de sensores. La gráfica conserva las últimas 120 lecturas de la serie; el JSON contiene la traza completa hasta 64 MiB y no es una sesión de FPS reabrible en Benchmarks. Los huecos no se interpolan.

El tiempo CPU del proceso colector se registra como observación, no como overhead causal. Falta medir coste con/sin captura en el hardware del propietario y en otros equipos; tampoco se certifica RICOCHET/Vanguard/EAC/BattlEye. Sigue el protocolo físico de [Performance Intelligence](PERFORMANCE_INTELLIGENCE.md).

## Validación del incremento

CI Windows prueba CPU/memoria real con GPU ausente simulada, inicio/parada voluntarios, bloqueo de operaciones, fallo del proveedor, estabilidad de selección, rechazo de callbacks tardíos y exportación JSON. El renderizador usa datos ilustrativos identificados y captura Inicio/monitor en tamaños normal y compacto. Esto verifica el flujo, no una mejora de Warzone. La matriz indica honestamente qué sigue parcial; el alcance completo del referente no se declara terminado.
