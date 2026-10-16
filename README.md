> **Rama Ultimate · Preview 0.4:** añade la página **Benchmarks**: captura manual mediante PresentMon 2.6.0 x64 verificado, importación CSV por proceso/swapchain, lows/percentiles, gráficas, historial, CSV/JSON y comparación A/B por repeticiones. La captura ETW en juegos y su compatibilidad anticheat requieren validación Windows real. [Uso y fórmulas](docs/BENCHMARKING.md) · [Auditoría](docs/ARCHITECTURE_AUDIT.md) · [Hoja de ruta](docs/PRODUCT_ROADMAP.md) · [Comparativa técnica](docs/COMPETITOR_RESEARCH.md).

# PCOptimizer

Aplicación gráfica en español para diagnosticar y ajustar una PC Windows orientada a gaming. Base inicial en **C# / .NET 10 / WPF**. No se anuncian mejoras de rendimiento sin mediciones.

## Preview 0.3: funciones implementadas

- **41 ajustes ejecutables** de registro, lectura del estado actual, selección individual y perfiles Mínimo, Recomendado, Gaming e Intensivo. Revisión de los valores exactos antes de escribir, respaldo por valor/tipo/existencia, verificación posterior y reversión con detección de conflictos. Consulta [qué aplica cada perfil](docs/ajustes-aplicables.md).
- **Ajustes del equipo con UAC por lote:** HAGS activado o desactivado y MPO para evaluación manual; rutas largas, ejecución de apps compatibles en segundo plano, descarga de actualizaciones sin pares y dos políticas de Windows Update. Los perfiles automáticos no incluyen estos ajustes. No se cambian voltajes ni protecciones.
- Interfaz reconstruida: barra lateral por función, resumen de hardware, tabla de ajustes virtualizada con búsqueda/filtros, detalle lateral de motivo/riesgo/activación y recuperación por operación. Los perfiles seleccionan 4 / 9 / 7 / 16 ajustes antes de excluir los no compatibles. Se pueden guardar y cargar perfiles personalizados de identificadores, sin scripts ni respaldos privados.
- Inventario real de CPU, GPU, memoria, Windows, motherboard, BIOS y unidades. Consulta de carga actual de CPU, memoria disponible y máximo de instancia de motor 3D; no es medición de FPS ni diagnóstico suficiente de cuello de botella.
- **Red:** diez muestras ICMP por destino, puerta de enlace detectada, Cloudflare y Google; media/mínimo/máximo, variación entre respuestas consecutivas y porcentaje sin respuesta. No se confunde ping a un resolvedor con rendimiento DNS ni con latencia de un juego.
- **Drivers:** inventario de versiones, fechas, proveedor, INF y firma reportada por Windows. Enlaces oficiales según proveedor; no se marca un driver como desactualizado por su fecha ni se recomienda una versión sin evidencia.
- **Almacenamiento:** consulta de salud y contadores de fiabilidad de Windows (temperatura, desgaste, horas y errores cuando se informen). Soporte y permisos dependen del controlador; no se predice vida restante ni se inventan contadores ausentes.
- **Audio:** detección de dispositivos y acceso al panel de mejoras. Loudness Equalization depende del driver; no se promete activación universal ni se fuerzan propiedades no documentadas.
- Planes de energía existentes: aplicar, comprobar y recuperar el anterior, con detección de cambios externos.
- Instalación rápida: **236 aplicaciones en diez categorías**, búsqueda, selección y revisión completa del lote, fuentes WinGet/Store explícitas y acuerdos interactivos. Historial individual; los códigos de WinGet no sustituyen una verificación independiente de instalación.
- Limpieza: análisis y eliminación selectiva de 18 apps opcionales para el usuario actual, comprobación de ausencia e historial. No elimina componentes críticos ni guarda datos de las apps.
- Puntos de Windows antes de cambios/lotes (intento activado por defecto), registros asociados, creación manual y acceso al asistente nativo. Los puntos no son copias de archivos personales.

**Límites actuales:** los candidatos restantes del catálogo no tienen ejecutor. No hay captura de FPS/frametimes, sensores térmicos de CPU/GPU, comparación automática de último driver frente a recomendado, cambios de red automáticos, ajuste universal de altura de barra ni pruebas de anticheat. La compilación y pruebas del núcleo no validan el funcionamiento real en Windows ni garantizan rendimiento.

## Descargar y abrir

El código actualizado está en la rama `work`. En [GitHub Actions](https://github.com/omi-idk/pcoptimizer/actions), abre un flujo **Build and test** de esa rama que haya terminado correctamente y descarga el artefacto **PCOptimizer-windows-x64-preview**. Extrae todo el ZIP y abre `PCOptimizer.App.exe` como usuario normal. El artefacto incluye .NET y caduca a los 14 días; requiere acceso a GitHub. Su disponibilidad depende de que se complete el trabajo de publicación del flujo.

El ejecutable preliminar no está firmado. Conserva las protecciones de Windows si lo bloquean. Empieza en un equipo de prueba; lee [la guía de primera prueba](docs/primera-prueba.md).

Consulta los [criterios técnicos y fuentes](docs/criterios-tecnicos.md) para conocer la evidencia revisada, limitaciones y requisitos antes de ampliar estas funciones.
La [revisión de WinUtil, Sophia Script y Win11Debloat](docs/revision-de-optimizadores.md) registra las implementaciones estudiadas y mejoras adoptadas. Los [criterios de instalación, limpieza, registro y timers](docs/optimizacion-y-software.md) explican los nuevos flujos y sus límites.
El [catálogo ampliado y opciones de Windows](docs/catalogo-y-opciones-windows.md) recoge cobertura, verificación y alcance de las últimas secciones. Las descripciones originales del catálogo se conservan en inglés; las categorías y notas añadidas están en español. Los avisos de licencia se incluyen en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Desarrollo en Windows

Objetivo inicial: Windows 11 x64. Instala el SDK .NET **10.0.402** (o un parche 10.0.4xx compatible con `global.json`) y mantén el runtime actualizado. La aplicación rechaza ejecutarse completa como administrador; inicia el IDE y la GUI como usuario normal.

Desde la raíz del repositorio:

```powershell
dotnet restore PCOptimizer.slnx --locked-mode
dotnet build PCOptimizer.slnx -c Release --no-restore
dotnet test tests/PCOptimizer.Core.Tests -c Release --no-build --no-restore
dotnet run --project src/PCOptimizer.App
```

Los paquetes están fijados mediante archivos `packages.lock.json`. Las consultas usan Windows PowerShell incluido en el sistema; no se omiten sus políticas de ejecución. Un bloqueo de CIM o `powercfg` se presenta como error, no como un diagnóstico correcto.

El historial se guarda en `%LOCALAPPDATA%\PCOptimizer\power-history.json`. No lo borres si necesitas recuperar un cambio pendiente. Los estados no verificados no demuestran que el cambio haya fallado: consulta el plan activo en Windows antes de reintentar. Si la lectura del historial falla, la aplicación impide nuevos cambios para conservar la información de recuperación. Se permite una instancia por cuenta de Windows, también entre sesiones, para evitar escrituras simultáneas.

El historial de instalaciones y limpieza está en `%LOCALAPPDATA%\PCOptimizer\software-history.json`. Una solicitud completada por WinGet no verifica por sí sola la instalación; el historial distingue omisiones y reinicios pendientes. La ausencia de una app después de eliminarla se consulta para el usuario actual. Si no se puede guardar el registro inicial, no se inicia la operación. El catálogo requiere App Installer registrado para ese usuario y una firma de Microsoft verificable de su ejecutable; no se busca `winget.exe` en el directorio actual ni en PATH.

Los registros de respaldo y punto asociado están en `%LOCALAPPDATA%\PCOptimizer\Recovery`. La opción de crear punto se inicia activada y se comparte entre pantallas durante la sesión. La creación necesita elevación y Protección del sistema disponible. Windows normalmente impide otro punto mediante este método antes de 24 horas; no se modifica esa restricción. Los puntos no son copias de archivos ni garantizan recuperar datos de aplicaciones. El asistente de Windows determina qué puntos siguen disponibles y realiza la restauración; abrirlo no inicia ni preselecciona una restauración. Consulta [el alcance de recuperación](docs/respaldo-y-recuperacion.md).

## Compilación desde Linux

WPF solo se ejecuta en Windows. Linux permite restaurar, compilar con `EnableWindowsTargeting` y ejecutar las pruebas del núcleo; no permite validar la interfaz, CIM, UAC o los cambios de energía. Usa los tres primeros comandos anteriores. No se necesitan servicios externos ni claves de API.

## Verificación manual pendiente en Windows

1. Abrir como usuario normal, navegar con ratón y teclado y comprobar legibilidad con escalado de pantalla. Comparar inventario con las herramientas de Windows.
2. Verificar nombres de planes con acentos y nombres personalizados. Probar un equipo con un único plan y otro con varios.
3. Seleccionar otro plan, cancelar la confirmación y cancelar UAC; comprobar que el plan permanece igual. Aplicarlo después y contrastar con `powercfg /getactivescheme`.
4. Reiniciar la aplicación y restaurar el último cambio. Cambiar el plan desde Windows y comprobar que una restauración incompatible se detiene. Eliminar un plan de prueba y comprobar el mensaje correspondiente.
5. Probar errores de permisos e historial inaccesible/corrupto conservando antes una copia. Comprobar que no se cambia energía sin guardar recuperación.
6. Validar compatibilidad por juego/anticheat antes de distribuir. Esta versión no tiene certificación ni pruebas de compatibilidad con anticheats.
7. En Instalación rápida, comprobar selección vacía, cancelación y WinGet ausente/antiguo. En un equipo de prueba, instalar un programa, repetir la solicitud y comprobar que no se actualiza. Verificar sus términos interactivos, resultados y detención ante error o reinicio requerido.
8. En Limpieza de apps, analizar como usuario normal; comparar con `Get-AppxPackage`. Confirmar que nada está seleccionado automáticamente. Guardar antes cualquier dato y probar una app prescindible. Verificar ausencia para ese usuario, presencia intacta para otros usuarios y errores de desinstalación sin informar falsamente de éxito. Reinstalar desde Store si se desea, sin asumir recuperación de datos.
9. Buscar con/sin acentos, filtrar por categoría y seleccionar apps en filtros distintos. Comprobar que la revisión muestra todas las seleccionadas, incluidas las ocultas por el filtro; cancelar no debe iniciar nada. Revisar la disposición de tarjetas y el contraste de filtros con escalado de pantalla.
10. En un equipo de prueba, comprobar las rutas separadas de WinGet y Microsoft Store. Rechazar acuerdos debe impedir la instalación. Verificar individualmente programas y versiones en Windows después de cada solicitud.
11. Confirmar que los candidatos restantes muestran evaluación pendiente, que los cubiertos aparecen en ajustes ejecutables sin duplicados y que ningún botón ejecuta scripts descargados de la referencia. Abrir barra de tareas, Modo Juego y opciones de Update y comprobar las páginas que ofrece la compilación real. No confundir abrir Configuración con aplicar un ajuste.
12. En una máquina de prueba con Protección del sistema configurada, crear un punto y verificar descripción/secuencia con `Get-ComputerRestorePoint` elevado. Ejecutar un cambio de energía o un lote pequeño y contrastar sus acciones con el registro asociado. No considerar el método validado sin esta prueba real.
13. Probar cancelación de UAC, protección desactivada, espacio insuficiente y el límite de 24 horas. Cancelar la continuación sin punto no debe lanzar el cambio ni instaladores. Desmarcar la opción debe producir un registro marcado como punto no solicitado, sin afirmar que existe uno.
14. Comprobar que un error al guardar el respaldo impide la operación, que los pasos no ejecutados siguen diferenciados de los confirmados y que no se borran registros corruptos. Reiniciar la app y comprobar los registros y la recuperación de selección de energía. Abrir el asistente y cancelar sin restaurar; cualquier restauración real debe probarse aparte en una máquina preparada para ello.

La integración continua está configurada para compilar y ejecutar pruebas del núcleo en Windows. Un build correcto no sustituye estas pruebas manuales.

Consulta [SECURITY.md](SECURITY.md) para conocer el alcance de la revisión, los controles implementados y las comprobaciones que faltan antes de distribuir. Se configuraron auditoría NuGet, avisos de actualización y CodeQL; no se ha ejecutado el flujo remoto de GitHub desde este entorno.

Pruebas adicionales de Preview 0.2: aplicar cada perfil y restaurar exactamente valores y tipos, incluidos valores originalmente ausentes; comprobar interrupciones parciales y conflictos. Validar UAC de HAGS/MPO y las políticas en ediciones compatibles y rechazadas. Contrastar red con herramientas de Windows, drivers con Administrador de dispositivos y discos con la utilidad del fabricante. Revisar interfaz con escalado de 100%, 125%, 150% y navegación por teclado.

## Revisar la nueva interfaz

CI genera el artefacto **PCOptimizer-ui-preview** con capturas de las vistas de resumen, ajustes, tamaño compacto, aplicaciones y recuperación. Se renderiza el XAML real en Windows, con datos ilustrativos identificados y sin consultar hardware ni escribir ajustes. Es una comprobación visual de diseño, no una validación de los cambios de Windows. El renderer no se incluye en el portable. También analiza la sintaxis PowerShell del lote elevado de aplicación y reversión sin ejecutarlo.
