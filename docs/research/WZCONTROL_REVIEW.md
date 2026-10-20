# WZControl 1.13.21 · revisión estática y decisiones de PCOptimizer

Entrada: ZIP proporcionado por el propietario, SHA-256 `fb5e72b5710093513f2840f7b07856b1bb5b1e5e1c343057959dc0e1ece09636`, 1640843 bytes. Extracción temporal con rechazo de rutas absolutas, `..`, separadores ambiguos y enlaces simbólicos. Se revisó como datos: **no se ejecutaron** instalador, Python, scripts, auxiliares, drivers ni llamadas a sus servidores.

El paquete contiene fuente Python/HTML, por lo que se hizo ingeniería inversa mediante análisis estático directo: inventario AST de **45 archivos Python / 644 funciones**, lectura de arquitectura, manuales y módulos críticos. No se encontró un archivo LICENSE/LICENCE/COPYING ni una autorización explícita de reutilización. Que se distribuya gratuitamente no autoriza incorporar su implementación o activos a un producto comercial. El ZIP y su código/logos no se incorporan al repositorio ni al portable. El código C# de esta entrega es propio; se trasladan necesidades y decisiones de producto, no implementaciones. Esta revisión no es una auditoría legal completa ni un pentest.

## Qué hace y cómo se organiza

| Área | Evidencia en el paquete | Aprendizaje / decisión propia |
| --- | --- | --- |
| Presentación/coordinación | `app.py`, `ui/index.html`, `wz/backend.py`: servidor HTTP en loopback y bandeja/ventana | Conservar WPF/MVVM y servicios inyectables. Los POST verifican X-Token; no se afirma que carezca de autenticación. No añadimos un servidor local ni trasladamos sus endpoints. |
| Diagnóstico | `checks.py`, `home.py`, `balance.py`, `verdict.py` | Priorizar hallazgos accionables y su evidencia. Separar puntuaciones editoriales de rendimiento medido. |
| FPS/frametimes | `frames.py`, `recorder.py`: PresentMon y capturas | Preservar nuestras fórmulas/procedencia y repeticiones A/B. Sus lows usan inverso de percentiles nearest-rank; nuestros lows usan media de la cola lenta y percentiles tipo 7. No son intercambiables ni uno universalmente superior. |
| Procesos | `interfere.py`, `gpuprocs.py`, `cpusampler.py` | Implementar nuestra medición CPU/E/S por identidad y disponibilidad, con streaming/voz conservados. No inferir causa por correlación o por nombre de ejecutable. GPU por proceso/hilos/ETW sigue pendiente. |
| Objetivos | `gamewrite.py`, `optimize.py`, `streamadvice.py` | Incorporar objetivos con revisión y contexto. Streaming no debe perder grabadores; laptop debe respetar OEM/batería. No aplicar todas las opciones al mismo tiempo para luego atribuir ganancias. |
| Configuración COD | `gameconfig.py`, `gamewrite.py`: formatos de usuario `.txt0/.txt1`, lectura/opciones y cambios con backup/juego cerrado | Es una buena capacidad futura. Requiere parser propio, esquema/versiones, conflictos, encoding, escritura atómica y pruebas con configuraciones reales autorizadas. No se incorpora un editor genérico ni se manipulan archivos protegidos. |
| Drivers/gráficos | `nvdrs.py`, `amdsettings.py`, `nvgpu.py` | APIs y settings por driver requieren versión/evidencia/documentación; no trasladar IDs u overrides de forma ciega. NVML existente continúa de solo lectura. |
| CPU/placa | `cputopo.py`, `biosguide.py`, `biosver.py`, `pcislots.py` | Topología X3D/híbrida y PCIe deben evaluarse con proveedores adecuados. No convertir recomendaciones de BIOS en escrituras automáticas ni forzar workers/afinidad por una tabla. |
| Red | `nettest.py`, `netgame.py`, `netetw.py` | Separar carga voluntaria, ICMP, ETW, captura de paquetes y tráfico real. No asumir que ping al último salto sea ping del juego. Bufferbloat y red por juego siguen pendientes con consentimiento y validación. |
| Control/overlay | `padtest.py`, `overlay.py`, `tray.py` | Polling observado no es latencia extremo a extremo. Evaluar coste y compatibilidad legítima antes de añadir overlay/atajos/HID. No se incorporan hooks ni macros. |
| Mantenimiento | `winslim.py`, `fixes.py`, `autostart.py` | Conservar nuestra lista cerrada de ajustes, revisión exacta y recuperación con conflictos; evitar reemplazarla por ejecuciones masivas y residencia elevada. |

## Aspectos concretos a refinar

1. **Expectativas de FPS:** `optimize.expected_fps` usa búsqueda en tablas y una estimación con banda porcentual. Eso no demuestra rendimiento de una escena/versión concreta de Warzone. PCOptimizer conserva mediciones y el resultado no concluyente cuando corresponda.
2. **Latencia:** `frames.input_lag` distingue métricas instrumentadas y una alternativa estimada basada en latencia más medio frametime. Esa estimación no es medición física clic-a-fotón/control-a-pantalla. No la incorporamos bajo un nombre que prometa latencia medida.
3. **Privilegios/residencia:** el instalador solicita elevación y `autostart.enable` registra una tarea con `RunLevel Highest`. Son decisiones funcionales del autor, no prueba de intención maliciosa. Para nuestra app mantenemos GUI sin elevar, operaciones UAC acotadas y ninguna tarea elevada automática.
4. **Descargas:** el script de herramientas consulta releases y descarga auxiliares; no se observó verificación SHA-256 fijada en ese flujo. Se mantiene nuestro PresentMon fijado/verificado y no se instala PawnIO u otro driver para esta mejora.
5. **Recuperación:** `gamewrite.apply` crea backup y verifica después, pero escribe directamente el archivo y procesa hermanos secuencialmente. `fixes.revert` restaura copias/valores sin comparar en ese bucle si hubo ediciones externas posteriores. Nuestro futuro editor deberá resolver fallos parciales y conflictos antes de escribir/revertir; el motor de registro actual ya conserva esas comprobaciones.
6. **Privacidad/red:** `netgame.Geo` puede consultar servicios de geolocalización y contiene una alternativa HTTP. No añadimos ese flujo; cualquier diagnóstico futuro que envíe IPs o genere tráfico debe explicar destino, alcance y consentimiento. No se probaron sus endpoints.
7. **Indicadores orientativos:** un semáforo de preparación puede ayudar a ordenar tareas, pero no es una nota de calidad del PC, FPS medidos ni certificación anticheat. Evitamos un puntaje ficticio global.

## Entrega propia funcional

- **Gaming → Procesos y objetivos gaming:** medición optativa CPU/E/S mediante GetProcessTimes/GetProcessIoCounters y un mismo handle `QUERY_LIMITED_INFORMATION` por observación; PID+creación para impedir mezclar identidades reutilizadas. Se informa ausencia, reinicios e instantáneas no comparables.
- Selección voluntaria del juego, filas de evidencia, escalas CPU diferenciadas, conservación de streaming/voz, JSON reabrible e informe. Sin terminar procesos ni leer memoria privada/módulos ni activar privilegios de depuración.
- Objetivos **Equilibrado, Competitivo, Gaming + streaming y Laptop gaming**, conectados a la selección real del motor existente. Proponen Modo Juego; solo Competitivo sin clips declarados añade desactivar captura Game Bar. Revisión, respaldo, punto Windows y aplicación/reversión siguen en el flujo ya implementado. Que varios objetivos compartan un ajuste no implica igual rendimiento: no se inventan cambios para diferenciarlos visualmente.
- Lectura de ruta de proceso con buffer inicial pequeño y crecimiento acotado, reutilizando el mismo handle de contadores. Evita asignar 32768 caracteres por proceso habitual y evita mezclar ruta/contadores de un PID reutilizado.

No se implementa toda la suite del referente en una entrega. Próximas capacidades priorizadas: configuración de juego compatible con reversión por conflictos, contexto/objetivos asociados a experimentos A/B, mejor atribución temporal de carga y proveedores de pantalla/CPU. Requieren implementación propia y validación, no botones de funciones ficticias.
