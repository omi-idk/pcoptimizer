# PCOptimizer

Aplicación gráfica en español para diagnosticar y ajustar una PC Windows orientada a gaming. Base inicial en **C# / .NET 10 / WPF**. No se anuncian mejoras de rendimiento sin mediciones.

## Estado actual

Implementado:

- Interfaz con navegación por Inicio, Gaming, Optimización, Personalización, Windows Update, Instalación rápida, Limpieza de apps, Energía, Red, Audio, Almacenamiento e Historial.
- Inventario de CPU, adaptadores gráficos, memoria, Windows y unidades físicas mediante consultas CIM de solo lectura.
- Enumeración y selección de planes de energía existentes, confirmación explícita, elevación de `powercfg` y comprobación del plan activo.
- Historial local escrito antes de cada cambio y restauración del plan anterior. La restauración se detiene si el plan actual cambió por otra vía o el anterior ya no existe.
- Instalación rápida: **236 aplicaciones en diez categorías**, tarjetas, búsqueda por nombre/categoría/identificador y selección conservada al filtrar. Revisión desplazable de todo el lote, términos interactivos y resultados individuales. 234 identificadores comprobados en el índice público de WinGet y dos fichas de Microsoft Store verificadas. Esto no prueba su instalación ni recomienda una versión. No se omiten hashes ni se actualizan instalaciones existentes. Las solicitudes de reinicio detienen el lote; no se programa un reinicio automático.
- Catálogo de **71 ajustes por evaluar**: los 67 controles de la referencia de WinUtil más tamaño de barra, timers, HAGS y capturas. Fichas con riesgos, fuente y objetivos de la referencia; no se consultan valores actuales ni se ejecutan los cambios.
- Personalización y Windows Update: accesos a las opciones nativas. Tamaño de barra/iconos, aplazamientos, restauración de políticas y desactivación de updates quedan visibles con evaluación pendiente. No se aplican políticas ni se reinicia Explorer desde estos módulos.
- Respaldo y recuperación: intento de crear y consultar un punto de Windows antes de cada cambio de energía o lote de instalación/limpieza, activado por defecto. Registro local previo de acciones y sus resultados, asociado al punto confirmado cuando exista. Creación manual, acceso a Protección del sistema y al asistente de restauración. Si no se confirma un punto, se pide decidir antes de aplicar cambios. Recuperación de la selección de plan anterior si ese plan aún existe.
- Limpieza de apps: análisis y desinstalación selectiva de cuatro aplicaciones opcionales para el usuario actual, comprobación de ausencia e historial. No tiene copia/restauración automática de datos.

Gaming, perfiles aplicables de optimización general, red y audio son funciones **pendientes**, identificadas en la interfaz. Optimización explica los niveles y límites previstos; no cambia registro, timers o arranque. El inventario de discos no evalúa aún salud SMART/NVMe. No hay captura de FPS, sensores térmicos, pruebas de anticheat ni exportación de datos.

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
11. Confirmar que las 71 fichas avanzadas muestran evaluación pendiente y ningún botón ejecuta scripts de la referencia. Abrir barra de tareas, Modo Juego y opciones de Update y comprobar las páginas que ofrece la compilación real. No confundir abrir Configuración con aplicar un ajuste.
12. En una máquina de prueba con Protección del sistema configurada, crear un punto y verificar descripción/secuencia con `Get-ComputerRestorePoint` elevado. Ejecutar un cambio de energía o un lote pequeño y contrastar sus acciones con el registro asociado. No considerar el método validado sin esta prueba real.
13. Probar cancelación de UAC, protección desactivada, espacio insuficiente y el límite de 24 horas. Cancelar la continuación sin punto no debe lanzar el cambio ni instaladores. Desmarcar la opción debe producir un registro marcado como punto no solicitado, sin afirmar que existe uno.
14. Comprobar que un error al guardar el respaldo impide la operación, que los pasos no ejecutados siguen diferenciados de los confirmados y que no se borran registros corruptos. Reiniciar la app y comprobar los registros y la recuperación de selección de energía. Abrir el asistente y cancelar sin restaurar; cualquier restauración real debe probarse aparte en una máquina preparada para ello.

La integración continua está configurada para compilar y ejecutar pruebas del núcleo en Windows. Un build correcto no sustituye estas pruebas manuales.

Consulta [SECURITY.md](SECURITY.md) para conocer el alcance de la revisión, los controles implementados y las comprobaciones que faltan antes de distribuir. Se configuraron auditoría NuGet, avisos de actualización y CodeQL; no se ha ejecutado el flujo remoto de GitHub desde este entorno.
