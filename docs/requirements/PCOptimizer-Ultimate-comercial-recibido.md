# PROMPT MAESTRO — PCOptimizer Ultimate
## Desarrollo de un optimizador profesional y comercial para Windows

### 1. Tu misión

Actúa como un equipo multidisciplinario de ingeniería especializado en:

- Arquitectura de software Windows con C# y .NET.
- Rendimiento de sistemas operativos, scheduler y administración de recursos.
- Diagnóstico de CPU, GPU, RAM, almacenamiento y redes.
- Optimización gaming competitiva y reducción de stuttering.
- Telemetría, benchmarking y análisis estadístico.
- Seguridad de software, privilegios y recuperación.
- Experiencia de usuario para aplicaciones de escritorio.
- Calidad de software, automatización, distribución y productos comerciales.

**Repositorio:** https://github.com/omi-idk/pcoptimizer

**Producto:** PCOptimizer Ultimate.

**Plataforma inicial:** Windows 11 x64.

**Visión:** desarrollar una aplicación comercial, profesional y técnicamente rigurosa para diagnosticar, optimizar y afinar computadoras Windows de distintos fabricantes y configuraciones.

No se trata solamente de mejorar una computadora o un videojuego.

Se busca crear un producto escalable que eventualmente pueda distribuirse y venderse al público, con calidad suficiente para competir contra herramientas consolidadas del mercado.

La primera plataforma de validación física será una PC específica del propietario. Sin embargo, **ninguna decisión arquitectónica debe limitar el producto a ese hardware**.

El objetivo fundamental del programa será:

**Analizar → Diagnosticar → Recomendar → Aplicar → Medir → Validar → Conservar o Revertir.**

No busco simplemente cientos de modificaciones del registro, un limpiador visualmente atractivo o una aplicación que anuncie aumentos de FPS sin demostrarlos.

Quiero ingeniería de rendimiento real.

---

### 2. Estado actual del repositorio

Antes de comenzar, inspecciona nuevamente todo el repositorio remoto, sin asumir que conserva exactamente el estado descrito aquí.

Durante la revisión anterior se identificó Preview 0.5, que ya incluía:

- Aplicación WPF con .NET 10.
- Motor de aproximadamente 41 tweaks ejecutables.
- Catálogo de software y limpieza selectiva.
- Recuperación y reversión de cambios.
- Diagnóstico inicial de Windows y hardware.
- Benchmarking mediante PresentMon.
- FPS medios, 1% lows, 0.1% lows, frametimes y percentiles.
- Comparaciones estadísticas A/B.
- Inventario ampliado de hardware.
- Telemetría NVIDIA mediante NVML.
- Análisis preliminar de presión de CPU y memoria.
- Sesiones temporales de planes energéticos.
- Perfil experimental específico de Warzone.
- Pruebas automatizadas y GitHub Actions.

Todo ello debe preservarse cuando esté correctamente implementado.

**No reconstruyas el proyecto desde cero sin una justificación técnica demostrable.**

Analiza las ramas, archivos, commits, dependencias y pipelines actuales.

Detecta funcionalidades duplicadas, incompletas, frágiles, inseguras o insuficientemente probadas.

Trabaja desde una rama nueva basada en la versión más reciente y coherente.

No sobrescribas trabajo ajeno, no modifiques directamente `main` y no realices merges destructivos.

Actualiza la auditoría técnica y la hoja de ruta.

---

### 3. Principios obligatorios del producto

PCOptimizer debe diferenciar entre:

1. Preferencias de Windows.
2. Ajustes de privacidad.
3. Mantenimiento.
4. Diagnóstico de problemas.
5. Optimizaciones con fundamento técnico.
6. Optimizaciones experimentales.
7. Cambios que han demostrado beneficio mediante benchmarks.

No etiquetes una preferencia estética como una mejora de FPS.

No asumas que un ajuste funciona porque aparece en un tutorial, en una herramienta popular o en una clave del registro.

Cada intervención debe disponer de:

- Descripción técnica.
- Justificación y evidencia.
- Hardware y versiones compatibles.
- Estado actual y estado propuesto.
- Efectos secundarios conocidos.
- Privilegios necesarios.
- Reinicio requerido, cuando corresponda.
- Método de verificación.
- Método de recuperación.
- Resultado observado, cuando exista.
- Nivel de confianza de la recomendación.

No prometas aumentos universales de rendimiento.

El mejor ajuste puede ser no modificar nada.

Prioriza estabilidad, reproducibilidad, seguridad y compatibilidad.

---

### 4. Arquitectura comercial y modular

Conserva C# y .NET como tecnologías principales.

Mantén inicialmente WPF, modernizando su arquitectura y presentación. Evalúa WinUI 3 solo si existe una ventaja técnica demostrable que justifique migrar.

Implementa una separación clara de responsabilidades:

**Presentation Layer**
- Dashboard.
- Diagnósticos.
- Benchmarks.
- Optimización.
- Perfiles.
- Historial.
- Configuración.
- Recuperación.

**Application Layer**
- Casos de uso.
- Coordinación de operaciones.
- Gestión de sesiones.
- Políticas de autorización.
- Estados tipados.
- Cancelación y progreso.

**Domain/Core**
- Modelos de hardware.
- Reglas de diagnóstico.
- Motor de recomendaciones.
- Contratos de tweaks.
- Estadísticas.
- Compatibilidad.
- Políticas de seguridad.
- Historial de experimentos.

**Infrastructure**
- Windows API.
- ETW.
- PresentMon.
- WMI/CIM.
- NVIDIA NVML.
- Proveedores AMD/Intel cuando sean apropiados.
- Windows Update.
- Registro de Windows.
- Sensores.
- Persistencia.

Utiliza MVVM, interfaces, inyección de dependencias y operaciones asíncronas donde aporten separación y capacidad de prueba.

Evita que `MainWindow` concentre la lógica de negocio.

No crees proyectos o servicios vacíos simplemente para aparentar modularidad.

La arquitectura debe ser extensible, mantenible y adecuada para un producto comercial de larga duración.

---

### 5. Motor universal de diagnóstico de hardware

Desarrolla un sistema de reconocimiento y evaluación profunda del hardware.

Debe funcionar con equipos de distintos fabricantes y configuraciones, incluyendo desktops, laptops, GPUs integradas, sistemas híbridos y múltiples GPUs.

**CPU**

Investiga e implementa, mediante proveedores compatibles:

- Fabricante y modelo.
- Arquitectura y generación.
- Núcleos físicos y procesadores lógicos.
- Topología de CPU.
- Cachés.
- Frecuencias efectivas cuando sean accesibles.
- Utilización por núcleo y por proceso.
- Temperaturas y límites térmicos disponibles.
- Información de potencia y throttling cuando el hardware la exponga.
- Detección de saturación y comportamiento anómalo.
- Limitaciones del sensor y precisión de los datos.

Considera explícitamente procesadores AMD X3D e Intel de arquitectura híbrida.

No apliques reglas universales de afinidad o frecuencia que puedan perjudicar su funcionamiento.

**GPU**

Detecta:

- Fabricante, modelo y arquitectura.
- VRAM dedicada y utilizada.
- Utilización gráfica.
- Temperaturas.
- Frecuencias.
- Consumo.
- Límites térmicos o eléctricos reportados.
- Versión del controlador.
- Características compatibles.
- Disponibilidad de funciones gráficas relevantes.

Utiliza APIs oficiales y proveedores seguros. No instales drivers kernel desconocidos para obtener sensores.

**RAM**

Analiza:

- Capacidad instalada.
- Módulos físicos.
- Velocidad configurada y declarada.
- Uso y memoria disponible.
- Memoria comprometida.
- Actividad de paginación.
- Presión real de memoria.
- Posibles inconsistencias entre módulos.
- Información disponible sobre canales y configuración.

No presupongas que la velocidad reportada por SMBIOS equivale a la configuración efectiva ni que una utilización elevada requiere vaciar memoria.

**Pantalla y almacenamiento**

Detecta pantallas, resolución, frecuencia, HDR, VRR y características compatibles mediante APIs adecuadas.

Evalúa salud reportada de SSD/HDD, espacio libre, errores y actividad de I/O cuando los datos sean accesibles.

No inventes métricas cuando un controlador no las exponga.

Todas las lecturas deben registrar fuente, unidad, timestamp y estado de disponibilidad.

---

### 6. Sistema profesional de benchmarking

Amplía el módulo existente de PresentMon.

Conserva su captura, importación CSV, estadísticas, historial y comparación A/B.

Añade progresivamente:

- Captura sincronizada de frametimes y sensores.
- FPS presentados y métricas de visualización cuando estén disponibles.
- Clasificación fiable de cuadros generados frente a renderizados.
- Frametime p50, p95 y p99.
- 1% y 0.1% lows con definiciones documentadas.
- Detección de spikes y anomalías.
- Comparaciones entre múltiples sesiones.
- Gráficas temporales y distribuciones.
- Contexto de juego, hardware, driver y configuración.
- Exportación de informes reproducibles.
- Persistencia completa del experimento.

Prioriza un sistema de tiempo común entre eventos ETW y telemetría de sensores.

**Objetivo técnico:** identificar correlaciones entre anomalías de frametime y cambios observados en carga de CPU, GPU, frecuencias, memoria, almacenamiento y procesos.

Distingue correlación de causalidad.

Si faltan sensores, indica la limitación y reduce la confianza del diagnóstico.

Las comparaciones A/B deben utilizar repeticiones independientes, idealmente de tres a cinco por configuración como mínimo inicial, con un escenario controlado.

Evalúa variabilidad, tamaño del efecto y condiciones de la medición.

Cuando la evidencia no sea suficiente, muestra "Mejora no concluyente".

También mide el overhead introducido por PCOptimizer y sus proveedores de captura.

La aplicación nunca debe perjudicar significativamente el rendimiento por mantener telemetría innecesaria activa.

---

### 7. Motor inteligente de diagnóstico

Implementa un motor capaz de transformar mediciones en hipótesis y recomendaciones.

Ejemplos:

**Escenario A — Limitación de GPU**

Se observan frametimes elevados, uso sostenido de GPU y evidencia consistente de que el tiempo de GPU limita la presentación.

El programa explica los datos, identifica alternativas de configuración gráfica y propone un experimento.

**Escenario B — Posible limitación de CPU**

Se identifican patrones consistentes en los tiempos de CPU, actividad de hilos y respuesta del rendimiento a cambios gráficos.

El programa propone una hipótesis, señala las mediciones faltantes y recomienda una prueba.

**Escenario C — Stuttering**

Los spikes de frametime coinciden temporalmente con actividad anómala de recursos.

Se explica qué se observó y qué intervención es razonable probar.

**Escenario D — Sin cuello de botella concluyente**

La evidencia no permite atribuir la causa.

No inventes un resultado para llenar el dashboard.

El diagnóstico debe producir:

- Hallazgo.
- Evidencia cuantitativa.
- Hipótesis.
- Confianza.
- Información faltante.
- Recomendación.
- Riesgos.
- Experimento para comprobarla.

Inicialmente implementa reglas deterministas, auditables y probadas.

No agregues modelos de IA externos como dependencia obligatoria. La inteligencia principal debe funcionar localmente y ser explicable.

---

### 8. Motor avanzado de optimizaciones

Amplía el motor de ajustes actual hacia una arquitectura profesional y reutilizable.

Debe administrar cambios de forma declarativa, comprobable y reversible.

Investiga oportunidades en:

**Windows y procesos**

- Modo Juego.
- Capturas en segundo plano.
- Servicios y procesos consumidores.
- Aplicaciones de inicio.
- Tareas en segundo plano.
- Planes y modos de energía.
- Configuración de gráficos de Windows.
- Configuración de pantalla.
- Preferencias de rendimiento del sistema.
- Actividad de actualizaciones durante partidas.
- Configuración de controladores, cuando exista una API soportada.

**CPU y scheduler**

- Diagnóstico de prioridades.
- Saturación por proceso/hilo.
- Políticas documentadas de Windows.
- Evaluación de afinidad por juego y topología.
- Efectos de planes de energía.
- Reglas persistentes solo si hay evidencia.

No fuerces prioridades de tiempo real, afinidades universales ni modificaciones del scheduler sin pruebas específicas.

**GPU**

- Evaluación de HAGS.
- Presentación gráfica.
- Límites FPS.
- VSync/VRR.
- Reflex y tecnologías equivalentes cuando el juego las soporte.
- Configuraciones compatibles de NVIDIA, AMD e Intel.
- Diagnóstico de versiones de drivers.
- Recomendaciones diferenciadas por GPU y juego.

No alteres archivos protegidos ni inyectes código en juegos.

**Memoria y almacenamiento**

- Identificación de presión de memoria.
- Revisión del archivo de paginación.
- Actividad excesiva de disco.
- Programas residentes.
- Espacio y mantenimiento.
- Limpieza selectiva de archivos temporales seguros.

No vacíes standby memory, borres shaders o desactives pagefile indiscriminadamente.

**Red**

- Latencia al router.
- Pérdidas.
- Jitter.
- Problemas DNS.
- Errores de adaptadores.
- Saturación de enlaces.
- Bufferbloat mediante pruebas voluntarias y controladas.
- Diagnóstico de configuración de red.

No comercialices supuestas reducciones universales de ping mediante cambios TCP no respaldados.

**Latencia del sistema**

Investiga ETW, ISR/DPC y técnicas documentadas para detectar interferencia del sistema, distinguiendo latencia del renderizado, entrada, audio y red.

No confundas la resolución de timers con latencia real.

Descarta ajustes populares de HPET, BCD, temporizadores o MMCSS cuando carezcan de evidencia suficiente.

**Seguridad**

No ofrezcas desactivar automáticamente Defender, Firewall, TPM, Secure Boot, VBS/HVCI o componentes del anticheat para ganar FPS.

Cualquier ajuste experimental debe estar separado, documentado y bloqueado cuando no cumpla sus requisitos.

---

### 9. Sistema de optimización automática

Desarrolla progresivamente una experiencia llamada **Optimización guiada**.

Funcionamiento esperado:

1. Detectar el hardware y el sistema.
2. Obtener una línea base de rendimiento.
3. Analizar problemas y oportunidades.
4. Generar recomendaciones personalizadas.
5. Ordenarlas por impacto probable, riesgo y confianza.
6. Solicitar aprobación.
7. Guardar configuraciones originales.
8. Aplicar cambios reversibles.
9. Ejecutar una nueva medición.
10. Comparar contra la línea base.
11. Conservar los cambios beneficiosos y proponer revertir los que causen regresión.

No apliques una colección de cambios simultáneos si después resulta imposible saber cuál produjo el efecto.

Primero implementa el modo guiado con revisión humana.

La automatización desatendida solo podrá incorporarse cuando existan controles suficientes de compatibilidad, recuperación y evidencia.

El sistema debe aprender de resultados locales guardados como experimentos, no afirmar que un cambio es beneficioso simplemente porque fue aplicado.

---

### 10. Game Booster universal

Extiende las sesiones gaming existentes.

El objetivo es permitir que el usuario seleccione un juego y habilite una sesión temporal de optimización.

Funciones previstas:

- Identificación voluntaria del juego.
- Detección segura de procesos por identidad y ruta.
- Evaluación de carga en segundo plano.
- Identificación de aplicaciones opcionales que consumen recursos.
- Aplicación temporal de un perfil compatible.
- Control seguro de operaciones elegidas.
- Restauración al terminar la sesión.
- Registro completo.
- Recuperación después de cierres inesperados.

Nunca termines indiscriminadamente procesos, servicios de Windows o componentes de seguridad.

Evita modificar procesos protegidos del juego.

Las prioridades y afinidades deben tratarse como intervenciones experimentales sujetas a compatibilidad y benchmarking.

No conviertas el programa en un servicio residente permanente si todavía no existe una necesidad técnica demostrada.

---

### 11. Perfil experimental de la PC del propietario

La primera computadora física de validación será:

**CPU:** AMD Ryzen 7 5800X3D
**GPU:** NVIDIA GeForce RTX 5060 Ti 16 GB
**Motherboard:** ASUS ROG Strix B550-F Gaming WiFi II
**RAM:** 32 GB DDR4 3200, cuatro módulos de 8 GB
**Sistema:** Windows 11 Pro x64
**Refrigeración:** DeepCool AK500 Digital
**Juego prioritario:** Call of Duty: Warzone
**Dispositivo de entrada:** GameSir G7 Pro

El escenario principal será Warzone competitivo, inicialmente a 1080p y ajustes gráficos bajos, sujeto a confirmación de la configuración real.

El usuario ha experimentado variaciones importantes de FPS y posibles irregularidades de frametime.

No asumas que esos problemas son consecuencia de Windows, del driver o del CPU sin medirlos.

Implementa:

- Reconocimiento exacto del hardware.
- Verificación de compatibilidad.
- Checklist técnico de BIOS, chipset, memoria y drivers.
- Registro de configuración de Warzone.
- Evaluación de resolución, Hz, límites FPS, VRR, DLSS, Frame Generation y Reflex.
- Captura de sesiones repetibles.
- Diagnóstico de frametimes.
- Comparación A/B.
- Recomendaciones experimentales.
- Historial de resultados y reversión.

El procesador 5800X3D tiene una topología diferente de los modelos X3D con varios CCD; no transfieras automáticamente recomendaciones diseñadas para esas plataformas.

No fuerces modificaciones de BIOS, voltajes, clocks o perfiles de memoria.

Respeta las restricciones de RICOCHET y otros sistemas anticheat.

**Este perfil será un caso de prueba y calibración, no el núcleo del producto.**

Todo el motor debe continuar funcionando en una PC con otro procesador, GPU, RAM o fabricante.

---

### 12. Perfiles comerciales universales

Diseña perfiles basados en objetivos y compatibilidad:

**Equilibrado**
Priorizar funcionamiento general, estabilidad y consumo razonable.

**Gaming competitivo**
Priorizar consistencia de frametimes, respuesta y rendimiento verificable.

**Gaming calidad**
Equilibrar rendimiento y calidad gráfica, considerando pantalla y capacidades GPU.

**Gaming + Streaming**
Considerar OBS, capturadoras, codificación y procesos necesarios sin desactivar componentes utilizados por el creador.

**Laptop gaming**
Respetar energía, temperaturas, batería y restricciones del fabricante.

**Productividad**
Identificar recursos y aplicaciones que afectan la respuesta del escritorio sin prometer FPS.

**Personalizado**
Permitir crear perfiles y experimentos, con validación y recuperación.

Los perfiles representan objetivos, no colecciones inmutables de cambios.

Deben filtrar opciones incompatibles y explicar por qué se recomienda o descarta cada intervención.

---

### 13. Interfaz profesional

Diseña una experiencia moderna, coherente y lista para evolucionar a un producto comercial.

Referencias de calidad: paneles profesionales de hardware, herramientas de benchmarking y aplicaciones modernas de rendimiento.

No copies directamente las interfaces de otras compañías.

Mantén una identidad visual propia.

Secciones esperadas:

- Inicio y estado general.
- Diagnóstico completo.
- Hardware y sensores.
- Uso de recursos.
- Gaming y perfiles.
- Laboratorio de benchmarks.
- Optimizaciones.
- Procesos y aplicaciones.
- Red y latencia.
- Drivers.
- Almacenamiento y mantenimiento.
- Historial.
- Recuperación.
- Configuración y ayuda.

Prioriza legibilidad, información accionable y jerarquía visual.

Incluye gráficas útiles, estados claros, explicaciones técnicas accesibles y controles de progreso/cancelación.

Soporta escalado de Windows, teclado y accesibilidad.

Conserva español como idioma inicial, diseñando la localización para agregar inglés posteriormente.

La interfaz debe distinguir claramente datos reales, simulaciones, funciones experimentales y capacidades no disponibles.

No agregues botones sin implementación o funciones ficticias.

---

### 14. Preparación para comercialización

Desde esta etapa, diseña el proyecto considerando distribución comercial futura.

**Propiedad intelectual y licencias**

Audita todas las dependencias, herramientas, fuentes, bibliotecas, activos y componentes externos.

Identifica sus licencias y obligaciones de redistribución comercial.

Respeta licencias MIT, MPL, LGPL, GPL y licencias propietarias según correspondan.

No copies código propietario ni distribuyas binarios de terceros sin autorización.

Documenta qué componentes pueden distribuirse y cuáles deben obtenerse separadamente.

**Seguridad y confianza**

El producto final deberá considerar:

- Ejecutables firmados.
- Instalador verificable.
- Actualizaciones autenticadas.
- Integridad de archivos.
- Privilegios mínimos.
- Validación de entradas.
- Protección del historial.
- Recuperación de operaciones interrumpidas.
- Auditoría de dependencias.
- Protección contra actualización o carga de componentes manipulados.

No incorpores actualizaciones automáticas inseguras.

**Privacidad**

Adopta una arquitectura local-first.

No envíes identificadores del equipo, procesos, configuraciones, benchmarks o registros a servicios externos de forma predeterminada.

Cualquier telemetría comercial futura deberá tener consentimiento explícito y controles claros.

**Licenciamiento comercial**

Prepara una separación arquitectónica opcional para futuras funciones de licenciamiento, suscripciones o ediciones del producto.

No implementes pagos, DRM, activaciones obligatorias o infraestructura de cuentas en esta fase salvo que exista una decisión comercial posterior.

El motor de optimización debe funcionar sin depender de servidores externos.

Considera eventual soporte, diagnósticos exportables, reportes de errores sanitizados y migraciones entre versiones.

---

### 15. Calidad y pruebas

La aplicación debe tratarse como software que eventualmente instalarán clientes en máquinas con configuraciones desconocidas.

Exige:

- Pruebas unitarias del dominio.
- Pruebas de integración de proveedores.
- Pruebas con sensores ausentes.
- Pruebas de archivos malformados.
- Pruebas de permisos insuficientes.
- Pruebas de cancelación.
- Pruebas de concurrencia.
- Pruebas de recuperación.
- Pruebas de cambio externo de configuraciones.
- Pruebas de reinicio y fallos parciales.
- Pruebas de compatibilidad de Windows.
- Pruebas de rendimiento y overhead de la propia aplicación.

Usa proveedores simulados para comprobar el motor de decisiones.

No utilices simulaciones como prueba de que Windows o un videojuego mejoraron.

El pipeline de GitHub debe compilar, ejecutar pruebas, analizar código y publicar artefactos de prueba.

Mantén una matriz documentada de hardware y versiones Windows validadas físicamente.

La compatibilidad con anticheats debe verificarse mediante escenarios legítimos, sin intentar eludirlos.

Ningún benchmark sintético sustituye las pruebas reales dentro del juego.

---

### 16. Plan de implementación

No intentes implementar todo el producto en un único cambio gigante.

Divide el desarrollo en entregas coherentes, completas y comprobables.

**Release 0.6 — Performance Intelligence**

Prioridad máxima.

- Integración temporal de benchmarking y telemetría.
- Un modelo de sesión unificado.
- Correlación de frametimes con recursos.
- Diagnóstico explicable.
- Hipótesis y evidencia.
- Persistencia de experimentos.
- Gráficas correlacionadas.
- Pruebas automatizadas y validación real pendiente claramente marcada.

**Release 0.7 — Optimization Intelligence**

- Motor de recomendaciones por capacidades.
- Contratos enriquecidos de optimización.
- Perfiles adaptativos.
- Flujo guiado de optimización.
- Asociación automática entre cambios y pruebas A/B.
- Reglas de conservación/reversión.
- Mejoras de recuperación.

**Release 0.8 — Universal Gaming Optimizer**

- Game Booster avanzado y seguro.
- Perfil Warzone calibrable.
- Perfiles para diferentes familias de hardware.
- Mejor diagnóstico de procesos.
- Proveedores adicionales de sensores.
- Diagnóstico de drivers, pantalla y red más completo.

**Release 0.9 — Product Hardening**

- Refactorización arquitectónica restante.
- Interfaz comercial consolidada.
- Pruebas de integración Windows.
- Matriz de compatibilidad.
- Seguridad, estabilidad, privacidad y recuperación.
- Benchmark de overhead.
- Preparación de distribución y actualización.

**Release 1.0 — Commercial Readiness**

Solo cuando exista evidencia suficiente:

- Instalación estable.
- Firma y procedencia verificables.
- Actualización segura.
- Documentación de usuario y soporte.
- Validación con distintos equipos físicos.
- Compatibilidad documentada.
- Calidad suficiente para distribuir a terceros.
- Sin funciones críticas incompletas presentadas como terminadas.

Estas versiones representan hitos, no fechas ni garantías de finalización.

---

### 17. Requisitos de ejecución para Codex

Trabaja de manera autónoma dentro de los límites del repositorio y del entorno disponible.

Antes de desarrollar cada módulo:

1. Revisa el código existente.
2. Consulta documentación oficial y fuentes técnicas relevantes.
3. Formula los objetivos verificables.
4. Diseña la solución.
5. Implementa código funcional.
6. Incorpora pruebas.
7. Ejecuta build, tests y análisis aplicables.
8. Corrige los errores encontrados.
9. Actualiza documentación.
10. Registra qué está terminado y qué requiere pruebas físicas.

No declares terminada una funcionalidad porque compile.

No inventes resultados de benchmarks ni compatibilidad.

Si Codex Cloud no tiene acceso al Windows o hardware físico requerido, implementa los contratos, proveedores y pruebas que sí pueda verificar; deja un procedimiento concreto y reproducible para validación local.

No apliques cambios del sistema a la máquina de desarrollo ni ejecutes experimentos privilegiados sin autorización.

No sacrifiques arquitectura, calidad o seguridad por entregar muchas funcionalidades rápidamente.

Evita escribir documentación excesiva para sistemas que todavía no funcionan.

Prioriza código verificable, resultados medibles y entregas utilizables.

Utiliza commits descriptivos y pull requests revisables.

No fusiones automáticamente cambios críticos a `main`.

---

### 18. Criterios de aceptación de la siguiente entrega

La Release 0.6 solamente se considerará completada cuando:

- Exista un modelo de experimento con timestamps y contexto reproducible.
- Los datos PresentMon y los sensores puedan correlacionarse cuando exista información temporal suficiente.
- Los sensores faltantes no provoquen diagnósticos falsos.
- Se detecten anomalías de frametime con criterios documentados.
- Se produzcan hallazgos explicables y pruebas automatizadas para ellos.
- Sea posible conservar y reabrir sesiones correlacionadas.
- Se pueda exportar un informe comprensible.
- La interfaz muestre gráficas y recomendaciones basadas en datos.
- Las operaciones se puedan cancelar sin corrupción.
- Las mediciones no modifiquen automáticamente configuraciones.
- Existan pruebas de errores y limitaciones relevantes.
- El proyecto compile correctamente en CI.
- Se documente explícitamente qué validaciones en Windows físico siguen pendientes.

No inventes capturas de Warzone ni datos térmicos del equipo del propietario.

---

### 19. Resultado esperado

Quiero que PCOptimizer evolucione hasta convertirse en una plataforma seria de diagnóstico, benchmarking y afinamiento de Windows.

Debe poder responder, con evidencia, preguntas como:

- ¿Por qué un juego presenta caídas bruscas de FPS?
- ¿Qué componente limita el rendimiento durante una escena?
- ¿Existe throttling o presión de memoria?
- ¿Qué procesos podrían interferir?
- ¿Qué cambios son compatibles con esta computadora?
- ¿Cuáles vale la pena probar?
- ¿El resultado realmente mejoró?
- ¿Es recomendable conservar la optimización?
- ¿Cómo puedo regresar exactamente al estado anterior?

La prioridad es desarrollar un producto que genere confianza por su precisión, estabilidad y transparencia.

No busco optimización aparente.

**Busco el mejor afinamiento verificable que permita el hardware, mediante un procedimiento reproducible y seguro.**

### INSTRUCCIÓN FINAL

Comienza inspeccionando el estado remoto actual de `omi-idk/pcoptimizer`.

Actualiza la auditoría y la hoja de ruta, selecciona el trabajo de mayor impacto pendiente e implementa la primera entrega funcional de **PCOptimizer 0.6 — Performance Intelligence** en una rama nueva.

No te detengas después de escribir un plan: desarrolla el primer incremento vertical, ejecuta las pruebas disponibles y prepara un PR revisable.

Al terminar, informa:

- Qué se implementó realmente.
- Qué archivos y módulos cambiaron.
- Qué pruebas se ejecutaron y sus resultados.
- Qué funcionalidades siguen parciales.
- Qué riesgos o limitaciones identificaste.
- Qué pruebas debo realizar en mi Windows 11.
- Qué sigue para la próxima entrega.

No afirmes que el programa ya está listo para comercialización hasta completar la validación correspondiente.
