# PROMPT MAESTRO — PCOptimizer Ultimate
## Desarrollo de un optimizador gaming profesional para Windows

### 0. MISIÓN

Actúa como un equipo senior multidisciplinario formado por:

- Arquitecto de software especializado en C#, .NET y Windows.
- Ingeniero de rendimiento de sistemas operativos.
- Especialista en optimización gaming y latencia.
- Ingeniero de sistemas Windows, scheduler, kernel y APIs.
- Especialista en hardware AMD, Intel y NVIDIA.
- Ingeniero de benchmarking, telemetría y análisis estadístico.
- Especialista en seguridad, calidad y automatización de pruebas.
- Diseñador UX/UI de aplicaciones de escritorio.

Tu misión es transformar el repositorio existente en una aplicación de optimización de Windows capaz de competir técnicamente con Razer Cortex, Process Lasso, CapFrameX, Chris Titus Tech WinUtil y otros optimizadores del mercado.

**Repositorio:** https://github.com/omi-idk/pcoptimizer

**Nombre:** PCOptimizer

**Plataforma:** Windows 11 x64, contemplando ampliación posterior a Windows 10 cuando sea compatible y justificable.

**Objetivo prioritario:** obtener el mejor rendimiento gaming estable y verificable para cada hardware, optimizando especialmente FPS, 1% lows, 0.1% lows, frametimes, stuttering, capacidad de respuesta y latencia.

El proyecto debe ofrecer herramientas generales de mantenimiento y afinamiento avanzado de Windows, sin limitarse exclusivamente a videojuegos.

No busco una demostración, un catálogo enorme de opciones deshabilitadas ni un programa que prometa incrementos falsos de FPS.

Quiero un producto real, funcional, profesional, auditable, extensible y técnicamente fundamentado.

No presupongas que todos los tweaks populares mejoran el rendimiento. Investiga, implementa, mide y conserva únicamente las recomendaciones justificadas.

---

### 1. AUDITORÍA INICIAL OBLIGATORIA

Antes de modificar código:

1. Analiza exhaustivamente las ramas `main` y `work`, sus diferencias y commits recientes.
2. Utiliza la rama más actualizada como punto de partida.
3. Crea una rama de desarrollo separada, sin sobrescribir trabajo existente.
4. Analiza todos los proyectos, dependencias, funcionalidades, pruebas, flujos de CI y mecanismos de recuperación.
5. Identifica código acoplado, duplicado, incompleto, obsoleto, vulnerable o difícil de mantener.
6. Comprueba qué funcionalidades realmente ejecutan operaciones y cuáles son exclusivamente informativas.
7. Conserva los componentes existentes que sean correctos y reutilizables.
8. Documenta los riesgos técnicos y prepara una hoja de ruta por prioridades.

El repositorio incluye una base WPF con .NET 10, un motor inicial de tweaks, inventario de hardware, utilidades de diagnóstico, instalación de software, perfiles y recuperación de cambios.

No reconstruyas el proyecto desde cero sin demostrar una necesidad arquitectónica.

Separa claramente funcionalidades implementadas, parcialmente implementadas, no probadas y pendientes.

Entrega como primer resultado `docs/ARCHITECTURE_AUDIT.md` y `docs/PRODUCT_ROADMAP.md`.

### 2. INVESTIGACIÓN DE LA COMPETENCIA

Investiga documentación y funcionalidades actuales de:

- Razer Cortex.
- Process Lasso.
- CapFrameX.
- Intel PresentMon.
- Chris Titus Tech WinUtil.
- Sophia Script.
- Win11Debloat.
- LibreHardwareMonitor.
- MSI Afterburner y RTSS.
- LatencyMon y herramientas de diagnóstico ETW.
- Herramientas oficiales de AMD, Intel, NVIDIA y Microsoft.

Elabora una matriz comparativa indicando qué funcionalidades existen en la competencia, cuáles están implementadas en PCOptimizer y qué ventajas competitivas podemos desarrollar.

No copies código, diseños, marcas ni algoritmos propietarios.

Verifica licencias, compatibilidad y mantenimiento de dependencias. Prefiere documentación primaria y APIs oficialmente soportadas.

Quiero que PCOptimizer combine las capacidades útiles de diagnóstico, medición, automatización, recuperación y afinamiento en una experiencia coherente.

---

### 3. ARQUITECTURA DEL PRODUCTO

Mantén C# y .NET como tecnología principal.

Evalúa conservar WPF con MVVM moderno frente a una migración a WinUI 3. No migres exclusivamente por estética: justifica los costes, compatibilidad, rendimiento, mantenibilidad y pruebas.

Propón una arquitectura modular similar a:

```text
PCOptimizer/
├── src/
│   ├── PCOptimizer.App/
│   ├── PCOptimizer.Core/
│   ├── PCOptimizer.Platform.Windows/
│   ├── PCOptimizer.Diagnostics/
│   ├── PCOptimizer.Sensors/
│   ├── PCOptimizer.Benchmarking/
│   ├── PCOptimizer.Optimization/
│   ├── PCOptimizer.Profiles/
│   ├── PCOptimizer.GameDetection/
│   ├── PCOptimizer.ProcessManagement/
│   ├── PCOptimizer.NetworkDiagnostics/
│   ├── PCOptimizer.Recovery/
│   └── PCOptimizer.Security/
├── tests/
├── docs/
└── .github/workflows/
```

La estructura es una propuesta, no una obligación de crear proyectos vacíos.

Implementa separación real de responsabilidades, interfaces para proveedores de hardware, inyección de dependencias, servicios asíncronos, cancelación de operaciones y modelos tipados.

Evita una MainWindow gigantesca con lógica de negocio.

Implementa un sistema de capacidades que detecte si cada función está disponible, no soportada, bloqueada por permisos o pendiente de validación.

La aplicación no debe romperse cuando un sensor, una API o una característica del hardware no esté disponible.

### 4. MOTOR DE DIAGNÓSTICO INTEGRAL

Construye un diagnóstico exhaustivo y principalmente de solo lectura.

**CPU**

Detecta fabricante, modelo, arquitectura, núcleos, hilos, cachés, frecuencia nominal y efectiva cuando estén disponibles, utilización total y por procesador lógico, temperaturas, límites térmicos y posibles señales de throttling.

Distingue carga de CPU de saturación de los hilos críticos. Una utilización total baja no descarta un límite por CPU.

Reconoce procesadores X3D y topologías multinúcleo, multichiplet y CPU híbridas, sin aplicar políticas de afinidad universales.

**GPU**

Detecta modelo, fabricante, VRAM física, driver, utilización, frecuencia, temperatura, consumo y causas de limitación cuando sean expuestas por APIs compatibles.

Evalúa NVML, APIs de NVIDIA, AMD ADLX, DXGI y contadores nativos de Windows.

Diferencia métricas medidas, estimadas y no disponibles.

**RAM**

Detecta capacidad, módulos, velocidad efectiva, configuración de canales cuando sea fiable, presión de memoria, memoria comprometida, hard faults y paginación.

No interpretes RAM ocupada por caché como memoria desperdiciada.

No implementes liberadores agresivos de memoria ni vaciado periódico de standby list como optimización general.

**Almacenamiento**

Analiza SSD/NVMe/SATA, estado SMART disponible, capacidad libre, errores, temperaturas, latencia de operaciones y actividad de lectura/escritura.

No recomiendes limpiezas que destruyan cachés útiles, shaders o datos personales.

**Sistema**

Detecta versión/build de Windows, BIOS/UEFI, motherboard, TPM, Secure Boot, VBS, HVCI, planes de energía, inicio automático, servicios, tareas de alto consumo y configuraciones gaming.

Diferencia configuraciones observadas de configuraciones verdaderamente activas.

**Pantalla y periféricos**

Detecta resolución, frecuencia de actualización, escalado, HDR, VRR cuando sea posible y dispositivos de entrada.

Nunca presupongas que una frecuencia de 280 Hz implica que el juego está funcionando a 280 FPS.

Presenta resultados en un panel comprensible, incluyendo advertencias y evidencia.

---

### 5. BENCHMARKS REALES: COMPONENTE CENTRAL

Implementa un sistema robusto de medición de rendimiento gaming.

Investiga e integra Intel PresentMon mediante una versión fija y un mecanismo compatible con su licencia.

Da prioridad a captura ETW documentada, sin inyectar DLL dentro de videojuegos ni modificar su memoria.

Métricas requeridas:

- FPS promedio.
- FPS mediano.
- 1% low.
- 0.1% low.
- Frametime mediano.
- Frametime p95 y p99.
- Distribución de frametimes.
- Variabilidad y spikes.
- GPU Busy y CPU Busy cuando estén disponibles.
- Frames presentados, mostrados y generados, diferenciados correctamente.
- Duración de la captura y número de muestras.
- Información de contexto del hardware y configuración.

Define matemáticamente cada métrica, especialmente los diferentes métodos de calcular 1% low.

No confundas FPS presentados con FPS realmente mostrados en pantalla.

Detecta y documenta las limitaciones ocasionadas por DLSS Frame Generation, VSync, límites de FPS, tecnologías de escalado y métricas no soportadas.

Implementa:

1. Captura manual con inicio y finalización controlados.
2. Historial de sesiones.
3. Exportación CSV y JSON.
4. Gráfica temporal de frametimes.
5. Distribución de FPS.
6. Comparación entre sesiones.
7. Identificación de resultados atípicos.
8. Captura asociada al ejecutable correcto.
9. Control de errores, permisos y finalización segura de procesos auxiliares.

El motor debe poder importar capturas conocidas para probar cálculos sin tener un videojuego ejecutándose.

#### Protocolo comparativo A/B

Implementa una metodología reproducible:

- Medición inicial.
- Registro de versión de juego, driver, Windows y configuración.
- Tres a cinco repeticiones como punto de partida.
- Cambiar una variable cada vez.
- Ejecutar nuevas mediciones.
- Alternar configuraciones cuando sea viable.
- Comparar distribución, variación e intervalos de incertidumbre cuando haya muestras suficientes.
- Informar beneficios, regresiones o resultados no concluyentes.

El software nunca debe mostrar "FPS aumentados" solamente porque aplicó un valor al registro.

Cada mejora debe apoyarse en mediciones.

Evalúa también el overhead de PCOptimizer durante el benchmarking.

---

### 6. SISTEMA DE DIAGNÓSTICO DE CUELLOS DE BOTELLA

Crea un motor que relacione telemetría de hardware con frametimes.

Debe investigar posibles causas de bajo rendimiento:

- Saturación de CPU o de hilos específicos.
- Saturación de GPU.
- Límite artificial de FPS.
- VSync.
- Frecuencia de actualización.
- Saturación o presión de VRAM.
- Paginación y presión de RAM.
- Thermal throttling.
- Power throttling.
- Actividad de procesos en segundo plano.
- Stuttering asociado a compilación de shaders.
- Problemas de almacenamiento.
- Configuraciones incorrectas o incompatibles.

Entrega un diagnóstico con:

- Hipótesis.
- Evidencias.
- Nivel de confianza.
- Información faltante.
- Acciones sugeridas.
- Riesgos.
- Método de validación.

No uses un indicador simplista del tipo "cuello de botella CPU 37%" sin fundamentación.

Diferencia correlación de causalidad.

Nunca atribuyas automáticamente un problema al hardware si las condiciones de medición son insuficientes.

---

### 7. MOTOR AVANZADO DE TWEAKS

Amplía el motor existente para que soporte categorías como:

**Gaming y gráficos:** Modo Juego, capturas en segundo plano, opciones gráficas de Windows, HAGS, MPO como solución de diagnóstico y configuraciones oficialmente documentadas.

**Procesos:** aplicaciones de inicio, carga en segundo plano, políticas de prioridad compatibles, consumo de recursos y reglas temporales.

**CPU y energía:** planes de energía, políticas soportadas y análisis de restricciones térmicas y energéticas.

**Memoria:** detección de presión, paginación inusual y configuración anómala, priorizando recomendaciones respaldadas por datos.

**Windows:** animaciones, efectos visuales, funciones opcionales, configuración y tareas de mantenimiento.

**Red:** análisis de adaptador, DNS, latencia, jitter, offloads y colas. Solo habilita cambios compatibles con hardware/controlador y evidencia específica.

**Almacenamiento:** mantenimiento seguro, revisión de espacio y problemas observables.

**Privacidad y limpieza:** controles opcionales completamente separados de las optimizaciones de FPS.

Para cada tweak utiliza un contrato tipado con:

```text
ID único
Categoría
Nombre y descripción
Objetivo técnico
Fuente y evidencia
Compatibilidad
Requisitos de privilegios
Estado actual
Estado solicitado
Beneficio esperado
Riesgos
Dependencias y conflictos
Necesidad de reinicio
Procedimiento de aplicación
Procedimiento de validación
Snapshot del estado previo
Método de reversión
Estado de ejecución
Resultado medido, si existe
```

Implementa detección de cambios externos y recuperación por operación.

Clasifica cada ajuste como:

- Seguro y respaldado.
- Condicional.
- Experimental.
- No recomendado.
- No compatible.

Los cambios condicionales no deben activarse automáticamente solo por pertenecer a un perfil.

No se debe equiparar "más agresivo" con "más rendimiento".

#### Tweaks que NO deben aplicarse indiscriminadamente

No desactives automáticamente Defender, Firewall, Secure Boot, TPM, VBS/HVCI, Windows Update, servicios esenciales ni mecanismos anticheat.

No hagas modificaciones ciegas de HPET, BCD, dynamic tick, temporizadores, MMCSS, prioridades de GPU en el registro, TCP, IPv6, MSI mode, afinidades o configuración interna de controladores.

Si alguna modificación avanzada tiene una hipótesis técnica válida, deberá contar con compatibilidad comprobada, advertencias, recuperación y pruebas reproducibles.

No implementes overclock, undervolt, modificaciones de BIOS ni escritura de voltajes o límites de potencia como parte del modo automático.

---

### 8. GAME BOOSTER AUTOMÁTICO

Implementa un administrador de sesiones gaming.

Cuando se detecte un juego compatible, el usuario podrá elegir activar un perfil previamente configurado.

Funciones objetivo:

- Detectar inicio y cierre del juego.
- Aplicar reglas permitidas de manera temporal.
- Identificar procesos de fondo que consumen recursos.
- Recomendar pausar aplicaciones prescindibles.
- Permitir exclusiones permanentes del usuario.
- Cambiar temporalmente entre planes de energía existentes cuando corresponda.
- Restaurar el estado previo al cerrar.
- Recuperar operaciones pendientes tras fallos o reinicios inesperados.

No termines procesos indiscriminadamente.

Nunca cierres procesos del sistema, anticheats, componentes gráficos, dispositivos de entrada, servicios de audio, software de seguridad ni aplicaciones con datos potencialmente sin guardar.

No alteres prioridades o afinidades del proceso del juego cuando el anticheat pueda impedirlo, y no intentes evadir esas restricciones.

Evita prioridades `REALTIME_PRIORITY_CLASS` y otros valores que puedan comprometer la capacidad de respuesta del sistema.

El Game Booster debe ser medible: si no reduce carga relevante ni mejora el rendimiento, debe indicarlo.

No mantengas un servicio residente pesado si una solución más sencilla consigue el objetivo.

---

### 9. PERFIL ESPECIALIZADO PARA WARZONE

Crea un perfil experimental avanzado, diseñado inicialmente para el siguiente hardware:

```text
Procesador:
AMD Ryzen 7 5800X3D

Motherboard:
ASUS ROG Strix B550-F Gaming WiFi II

GPU:
NVIDIA GeForce RTX 5060 Ti 16 GB

RAM:
32 GB DDR4 3200 MHz
4 módulos de 8 GB

Sistema:
Windows 11 x64

Refrigeración:
DeepCool AK500 Digital

Juego prioritario:
Call of Duty: Warzone

Entrada:
Control GameSir G7 Pro

Objetivo:
FPS competitivos, 1% lows altos,
frametimes estables y baja latencia.
```

Es un perfil de hardware conocido, no una configuración que deba aplicarse automáticamente sin detectar primero los componentes reales.

El optimizador debe evaluar:

1. Estado de drivers NVIDIA y chipset AMD.
2. Configuración de RAM y frecuencia efectiva.
3. Temperaturas y frecuencias del 5800X3D.
4. Saturación de hilos de CPU.
5. Comportamiento del renderizado GPU.
6. Presión de VRAM.
7. HAGS y otras funciones con soporte.
8. Configuración de Windows y energía.
9. Resolución, frecuencia de pantalla y límite de FPS.
10. DLSS, Reflex y opciones de latencia compatibles con la versión del juego.
11. Configuraciones gráficas del juego documentadas y autorizadas.
12. Procesos en segundo plano.
13. Variabilidad de frametimes en escenarios reproducibles.
14. Compatibilidad con RICOCHET Anti-Cheat.
15. Diferencias entre ejecutar el juego normalmente y mediante una sesión de optimización.

El perfil debe diferenciar ajustes para FPS promedio, consistencia de frametimes y latencia.

No modificar archivos protegidos, memoria del juego, paquetes, tráfico, anticheat ni parámetros competitivos prohibidos.

Si una configuración del juego cambia de versión, vuelve a validar su compatibilidad.

No presentes como optimizaciones actuales instrucciones heredadas de versiones antiguas de Warzone.

El resultado final debe ser un perfil calibrable a partir de benchmarks reales y no un conjunto estático de valores de registro.

---

### 10. OPTIMIZADOR UNIVERSAL

La misma aplicación debe soportar equipos distintos.

Implementa detección de capacidades para:

- Procesadores AMD e Intel.
- Gráficas NVIDIA, AMD e Intel.
- Hardware de escritorio y portátil.
- Sistemas con iGPU y dGPU.
- Distintas generaciones de CPU y GPU.
- Topologías heterogéneas.
- Diferentes ediciones y compilaciones de Windows compatibles.

Crea perfiles:

**Conservador:** bajo riesgo, diagnóstico y recomendaciones esenciales.

**Gaming competitivo:** prioriza consistencia de frametimes y respuesta, siempre con mediciones.

**Gaming equilibrado:** equilibrio entre rendimiento, consumo, temperatura y estabilidad.

**Multitarea/Streaming:** evalúa juego, OBS, codificación GPU y carga simultánea.

**Avanzado/Experimental:** opciones controladas, explícitas y con medidas de recuperación.

**Personalizado:** permite guardar, exportar e importar configuraciones validadas.

Los perfiles son objetivos de optimización, no garantías de resultados.

---

### 11. ANALIZADOR DE RED Y LATENCIA

Construye diagnósticos independientes para:

- Latencia a la puerta de enlace.
- Latencia hacia destinos externos.
- Variación temporal.
- Pérdida observable.
- Saturación de subida y bajada.
- Posibles síntomas de bufferbloat.
- Errores del adaptador.
- Negociación de velocidad.
- Configuración del controlador.
- DNS y tiempos de resolución.

Diferencia explícitamente ICMP, DNS, tráfico del videojuego y latencia del sistema de renderizado.

No prometas reducir el ping a los servidores de