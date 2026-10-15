# Criterios técnicos y fuentes

Revisión inicial: 15 de octubre de 2026. Este documento distingue las capacidades descritas por los fabricantes de las decisiones del proyecto. Leer una documentación no acredita una mejora de FPS ni certifica compatibilidad con un anticheat.

## Decisiones basadas en la revisión

| Área | Evidencia consultada | Decisión para PCOptimizer | Validación pendiente |
| --- | --- | --- | --- |
| Energía | Microsoft documenta enumeración, activación y consulta de planes con `powercfg` [1]. Los modos de rendimiento utilizan políticas superpuestas y no equivalen simplemente a otro plan [2]. | Mostrar planes reales y verificar el activo después del cambio. Guardar el anterior antes de modificarlo. No crear un plan «gaming» ni prometer FPS adicionales. | Windows real: permisos, idiomas, portátiles/OEM y Modern Standby. Detectar modos de energía por separado en otra versión. |
| Medición | PresentMon recoge eventos ETW y documenta métricas de CPU, GPU y presentación. También documenta errores y limitaciones con HWS, OpenGL/Vulkan y permisos [3][4]. | Evaluar captura externa sin overlay ni inyección como primera integración. Identificar versión, API gráfica, tipo de cuadro y calidad de la captura. | Integración, coste de captura y compatibilidad por juego. PresentMon todavía no está incluido. |
| Anticheat | Riot describe Vanguard como cliente y controlador de kernel y reconoce posibles interacciones [5]. BattlEye explica que puede bloquear software de sensores con drivers vulnerables y que las políticas pueden depender del desarrollador del juego [12]. | Usar interfaces documentadas del sistema. No tocar memoria, archivos ni protecciones del juego. No cargar drivers propios ni apagar el anticheat. Auditar también los drivers de bibliotecas de sensores de terceros. No anunciar certificación. | Verificación independiente por título, versión de anticheat, versión de Windows y versión de la aplicación. No se ha probado ningún juego. |
| Seguridad de Windows | Microsoft explica el aislamiento de VBS y la protección de integridad del código de HVCI [6]. | Conservar estas protecciones. No ofrecer desactivar antivirus, Secure Boot, TPM o integridad de memoria como optimización automática. | Requisitos actualizados de cada juego. |
| Red | La guía de Microsoft explica el intercambio entre interrupciones, consumo de CPU y latencia. También distingue microsegundos de procesamiento local de milisegundos de tránsito [7]. | Diagnosticar antes de ajustar. No desactivar offloads/RSS ni aplicar valores TCP universales. No prometer eliminar el ping del proveedor o del servidor. | La fuente trata Windows Server; sus parámetros no se trasladarán a Windows cliente sin documentación del adaptador y mediciones. |
| Audio | Microsoft describe Loudness EQ como ajuste dinámico de ganancia para reducir diferencias de sonoridad [8]. Los fabricantes pueden incluir o sustituir APOs [9]. | Detectar soporte por dispositivo antes de ofrecer la función. Explicar el efecto sobre la dinámica. No editar claves ocultas para forzarla ni prometer soporte universal. | Detección del efecto, pruebas con USB/Bluetooth/HDMI y controladores OEM, latencia y evaluación auditiva por juego. No hay evidencia revisada aquí de una ventaja competitiva general. |
| Almacenamiento | `Get-StorageReliabilityCounter` expone temperatura, errores, desgaste y horas cuando el dispositivo los proporciona; la propia documentación muestra campos vacíos [10]. | Lectura de salud en una fase posterior. Ausente no significa cero. Mostrar origen y disponibilidad de cada dato. No convertir capacidad o antigüedad en «porcentaje de salud». | Soporte real de NVMe/SATA, RAID y puentes USB; interpretación del fabricante. Sin predicción exacta de fecha de fallo. |
| Límites térmicos y eléctricos | Intel indica que el límite térmico depende del modelo y del diseño OEM; alcanzar temperaturas altas no basta para diagnosticar un problema [13]. NVIDIA expone umbrales de reducción térmica y apagado, con diferencias de API por generación [14]. | No implementar overclock, undervolt, cambios de BIOS ni aumentos de potencia. No inventar una temperatura segura universal ni recomendar funcionar al límite. Correlacionar temperaturas, frecuencias, límites y rendimiento antes de recomendar refrigeración. | Consultar especificaciones de cada modelo, sensores soportados y comportamiento OEM. Las protecciones de Intel no son garantía de ausencia de daños en todo el equipo ni de estabilidad. |
| Tecnologías de baja latencia | NVIDIA describe Reflex para productos compatibles [11]. AMD documenta Anti-Lag 2 como una integración en la lógica del juego, con requisitos de hardware/driver [15]. | Recomendar opciones nativas solo donde el juego y el fabricante las soporten. No intentar añadir Reflex o Anti-Lag 2 a juegos arbitrarios ni modificar sus archivos. | Evaluar compatibilidad y latencia por juego. No confundir distintas generaciones de Anti-Lag ni equiparar latencia de PC con ping de red. |

## Cómo demostrar una mejora

Protocolo propuesto; todavía no ejecutado sobre juegos:

1. Registrar hardware, Windows, controlador, versión del juego, resolución, calidad, límite de FPS, VSync/VRR, generación de cuadros y modo de energía. Separar batería de alimentación externa.
2. Elegir una escena o benchmark reproducible, calentar el sistema y excluir carga inicial/compilación de shaders cuando no formen parte del problema analizado.
3. Capturar varias repeticiones de la referencia; como punto de partida, entre tres y cinco. Cambiar una sola opción y repetir las mismas condiciones. Alternar referencia y ajuste cuando sea viable para controlar calentamiento y variación temporal.
4. Comparar tiempo de cuadro mediano y p95/p99, FPS y dispersión entre repeticiones; registrar duración y número de cuadros. No confundir FPS renderizados, presentados y cuadros generados.
5. Mostrar resultados absolutos y relativos, incluidos empeoramientos. Si el efecto no supera la variación de las mediciones, informar «sin mejora concluyente». No fabricar intervalos de confianza a partir de una captura única.
6. Medir también el coste de PCOptimizer frente al mismo escenario sin captura. Separar diagnósticos intensivos y pruebas de carga de las partidas.

El diagnóstico de cuello de botella debe formular una hipótesis por carga, con evidencia y confianza. Antes de señalar CPU/GPU hay que descartar límites de FPS, VSync, menús, carga de shaders, presión de memoria y throttling. La utilización total de CPU no demuestra que ningún hilo esté saturado. Un 99 % de GPU por sí solo no implica un problema ni justifica comprar hardware. Contrastar cambios de resolución/calidad ayuda a investigar, pero no sustituye la telemetría.

En red se separarán pruebas al router, a un destino externo y al servidor del juego cuando sea accesible. ICMP no representa necesariamente el tráfico del juego: un equipo puede filtrar o relegar respuestas. Se comparará conexión en reposo y bajo carga controlada, sin lanzar tráfico intensivo durante partidas. Las recomendaciones de DNS, router y gestión de colas requieren investigación específica adicional.

## Condiciones para publicar funciones

- Mostrar si una función es disponible, incompatible, no evaluada o pendiente; no usar datos de demostración como si fueran mediciones.
- Para cada ajuste: documentar fuente, requisitos, alcance, efecto esperado, riesgos, necesidad de reinicio y restauración. Guardar los valores originales y comprobar el resultado.
- Mantener elevación limitada a acciones concretas. Distribuir por un canal verificable; evaluar firma de binarios antes de distribución pública.
- Mantener una matriz de pruebas por juego/anticheat. Un ensayo sin incidencias no garantiza ausencia futura de bloqueos o sanciones. No buscar formas de ocultar la aplicación al anticheat.
- Obtener diagnósticos locales sin números de serie, nombres de usuario, IP pública ni identificadores de cuentas por defecto. Revisar y redactar datos antes de implementar exportaciones.

## Fuentes consultadas

1. Microsoft — [Powercfg command-line options](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options).
2. Microsoft — [Customize the Windows performance power slider](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-power-slider). Describe políticas/modos; no prueba beneficios en juegos.
3. Intel / GameTechDev — [PresentMon: arquitectura, permisos y limitaciones](https://github.com/GameTechDev/PresentMon/blob/main/README.md).
4. Intel / GameTechDev — [PresentMon Console Application: opciones y métricas](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md). Las ramas `main` cambian; una integración deberá fijar una versión y revisar su licencia.
5. Riot Games — [What is Vanguard?](https://support.riotgames.com/en-us/riot/performance/what-is-vanguard).
6. Microsoft — [Virtualization-based Security (VBS)](https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/oem-vbs).
7. Microsoft — [Performance tuning for network adapters](https://learn.microsoft.com/en-us/windows-server/networking/technologies/network-subsystem/net-sub-performance-tuning-nics). Alcance original: Windows Server.
8. Microsoft — [Loudness Equalization DSP](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/loudness-equalization-dsp). Describe el DSP; no garantiza su exposición en todos los endpoints.
9. Microsoft — [Audio Processing Object Architecture](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/audio-processing-object-architecture).
10. Microsoft — [Get-StorageReliabilityCounter](https://learn.microsoft.com/en-us/powershell/module/storage/get-storagereliabilitycounter?view=windowsserver2025-ps). Documentación del módulo Storage; verificar disponibilidad en cada cliente.
11. NVIDIA — [Reflex](https://www.nvidia.com/en-us/geforce/technologies/reflex/). Distingue reducción de latencia y medición con productos compatibles; sus afirmaciones comerciales no constituyen una prueba independiente. No está integrado.
12. BattlEye — [FAQ](https://www.battleye.com/support/faq/). Contiene incidencias históricas y recomendaciones de soporte de varias épocas. No se interpretan como requisitos vigentes para todos los juegos. En particular, no copiamos sus antiguos procedimientos de desactivación de protecciones de Windows al optimizador.
13. Intel — [Information about Temperature for Intel Processors](https://www.intel.com/content/www/us/en/support/articles/000005597/processors.html). Deben consultarse las especificaciones del procesador concreto; los rangos citados en el artículo no son límites para AMD o GPU.
14. NVIDIA — [NVML: definiciones y contratos del encabezado oficial](https://github.com/NVIDIA/go-nvml/blob/main/pkg/nvml/nvml.h). Ver `nvmlTemperatureThresholds_enum`, `nvmlDeviceGetTemperatureThreshold` y las notas de soporte por arquitectura. No implica que cualquier GeForce exponga todas las métricas.
15. AMD GPUOpen — [AMD Radeon Anti-Lag 2 SDK](https://gpuopen.com/anti-lag-2/). La integración corresponde al desarrollador del juego. No está incluido en PCOptimizer.

La página principal de [Easy Anti-Cheat](https://www.easy.ac/) pudo consultarse, pero su índice de soporte devolvió HTTP 403. Por tanto, no se atribuyen a EAC aprobaciones o garantías sobre aplicaciones de terceros. Sigue pendiente la revisión de sus políticas específicas, de las especificaciones térmicas de cada AMD, de políticas de audio por juego y de SQM de routers. No se aplicarán ajustes basados en esas áreas hasta cerrar la investigación pertinente. Se descartan como referencia los enlaces que no pudieron recuperarse, en lugar de atribuirles conclusiones.
