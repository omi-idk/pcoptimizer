# Investigación técnica y matriz comparativa

Consulta de fuentes primarias el 15 de octubre de 2026 (México). Describe capacidades documentadas, no un ensayo comparativo de rendimiento ni una auditoría completa de terceros. No se ejecutaron scripts de optimización ni se copió código propietario. Las páginas comerciales no prueban mejoras en nuestro hardware.

| Referencia y fuente | Capacidad documentada útil | Estado en PCOptimizer de esta rama | Oportunidad / límites de licencia |
| --- | --- | --- | --- |
| [Razer Cortex](https://www.razer.com/cortex/boost) | Gestión de sesiones y reducción de actividad de fondo según su oferta | Sin Game Booster automático | La consulta automatizada devolvió 403; no se dan por verificadas sus recomendaciones actuales. Software propietario: no copiar implementación ni terminar procesos indiscriminadamente. |
| [Process Lasso](https://bitsum.com/) | Reglas de procesos, respuesta bajo carga y planes de energía | Cambio/restauración manual de planes existentes; no ProBalance ni reglas de procesos | Producto propietario. Desarrollar sesiones temporales consentidas con conflictos y recuperación; no imitar algoritmos propietarios ni afinidades universales. |
| [CapFrameX](https://github.com/CXWorld/CapFrameX) | Captura/análisis de frames, comparación, sensores y presentación | Nuevo módulo de importación, estadísticas, capturas PresentMon, gráficas y repeticiones A/B | Su LICENSE contiene MIT y avisos/licencias adicionales de componentes (incluido Open Hardware Monitor MPL); la API de GitHub devuelve NOASSERTION para el conjunto. No asumir MIT para todos sus binarios. No se incorporó código. |
| [Intel PresentMon 2.6.0](https://github.com/GameTechDev/PresentMon/tree/v2.6.0) | ETW de presentación, busy y tipos de frame cuando hay instrumentación compatible | Integración del console x64 por PID y sesión única, con hash fijo; importador de MsBetweenPresents | Console/repo MIT; revisar avisos de componentes si se redistribuye. No se incluye exe ni servicio residente. ETW necesita permisos; soporte por juego/driver aún por validar. |
| [WinUtil](revision-de-optimizadores.md) | Instalación, selección por perfiles y ajustes Windows | 236 aplicaciones, 41 ajustes ejecutables, perfiles revisables, 18 Appx opcionales | MIT conservada en distribución. Se preservan snapshots reales por operación en vez de asumir valores por defecto. No atribuir FPS a privacidad/personalización. |
| [Sophia Script](revision-de-optimizadores.md) | Preferencias y ajustes específicos por Windows | Fuentes y evaluación por ajuste; subconjunto implementado con motor propio | MIT del script; no extrapolar a wrappers cerrados. Diagnóstico no cambia servicios. |
| [Win11Debloat](revision-de-optimizadores.md) | Eliminación selectiva, snapshot y recuperación | Eliminación por usuario y respaldo tipado de registro | MIT. No ejecutar limpieza de todos los usuarios ni confundir reinstalación con restauración de datos. |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | Proveedores de sensores CPU/GPU/placa/almacenamiento | No integrado; CIM y contadores nativos parciales | MPL-2.0, obligaciones por archivo y avisos adicionales; acceso bajo nivel/controladores necesita evaluar firma y compatibilidad. No introducir un driver kernel solo para mostrar temperaturas. |
| [MSI Afterburner / RTSS](https://www.msi.com/Landing/afterburner/graphics-cards) | Monitorización, curvas/OC y overlay según componentes | Sin OC/voltajes ni overlay inyectado; gráficas internas | Herramientas propietarias y licencias separadas. No redistribuir sin permiso. Medir fuera del proceso del juego. |
| [LatencyMon](https://www.resplendence.com/latencymon) | Diagnóstico de ISR/DPC, hard pagefaults y aptitud para audio en tiempo real | No implementado | Producto propietario. DPC no equivale a latencia de entrada ni ping del juego. |
| [Microsoft ETW](https://learn.microsoft.com/windows/win32/etw/about-event-tracing) / WPR/WPA | Trazas de sistema y análisis de CPU/I/O/eventos | PresentMon usa ETW; sin analizador ETW general | Usar APIs documentadas; controlar permisos, volumen, pérdida de eventos y overhead. |
| [NVIDIA NVML](https://docs.nvidia.com/deploy/nvml-api/index.html), [AMD ADLX](https://gpuopen.com/adlx/), [Intel PresentMon](https://github.com/GameTechDev/PresentMon) | Telemetría específica por fabricante | Inventario y enlaces oficiales; proveedores nativos pendientes | Revisar SDK/EULA y redistribución por separado, detección dinámica y métricas N/D. No asumir NVML completo en todas las GeForce. |

## Decisión de medición

Se descargaron documentación y licencia del tag `v2.6.0`, se consultó la publicación oficial y se verificó localmente el SHA-256 del console descargado (980320 bytes):

`b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af`

Coincide con el digest de la publicación. El pin vive en `PresentMonVersion`; actualizar requiere revisar argumentos, formato, licencia, hash y fixtures, no consultar «latest» durante la ejecución.

Se usa console en lugar del servicio/overlay: evita instalar un servicio para la primera captura y separa el proveedor del cálculo. No se pretende reimplementar ETW ni obtener todos los sensores con el ejecutable console. El diagnóstico de cuello de botella requiere telemetría sincronizada que aún falta.

La ventaja buscada es vincular medición reproducible, explicación del ajuste, snapshot y reversión en una sola sesión. Esta ventaja es un objetivo del producto; no se afirma superar a los competidores sin mediciones comparativas.
