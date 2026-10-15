# Instalación, limpieza y niveles de optimización

Diseño y revisión inicial: 15 de octubre de 2026. Complementa los [criterios técnicos](criterios-tecnicos.md) y la [revisión de optimizadores existentes](revision-de-optimizadores.md).

## Instalación rápida: implementación inicial

Catálogo: 236 aplicaciones de las capturas, en diez categorías. Se adaptaron los datos de WinUtil conservando su licencia MIT. Se comprobaron 234 identificadores en el índice público de Microsoft y dos fichas de Microsoft Store; la versión disponible y el manifiesto pueden cambiar. La ejecución especifica `--id`, `--exact` y la fuente apropiada (`winget` o `msstore`), mantiene comprobaciones y acuerdos; los instaladores WinGet se abren en modo interactivo. El catálogo comunitario de WinGet no es una certificación de Microsoft de todos los paquetes. Consulta [cobertura y verificación](catalogo-y-opciones-windows.md).

Ningún programa está seleccionado por defecto. Se revisa la lista antes del lote. WinGet y el instalador conservan sus preguntas de licencia; PCOptimizer no usa aceptación automática, instalación silenciosa, omisión de hashes o `--allow-reboot`. `--no-upgrade` evita actualizar software existente por el simple hecho de seleccionarlo para instalar. Un WinGet antiguo que no admita estas opciones debe actualizarse: no se reintenta quitando restricciones.

Los instaladores pueden requerir permisos y ofrecer componentes, asociaciones o inicio automático. El usuario debe revisar sus opciones. PCOptimizer no supervisa todo el comportamiento del software instalado ni garantiza compatibilidad anticheat de cada programa.

La aplicación registra la solicitud antes de abrir WinGet. Se conservan resultados individuales, omisiones y solicitudes de reinicio; el lote se detiene ante fallos o reinicios pendientes. **El código de salida no es una comprobación independiente de que el programa y su versión estén instalados.** La consulta posterior de inventario de software sigue pendiente. No se revierte automáticamente la instalación de un lote.

Requiere App Installer registrado para el usuario. Se resuelve el ejecutable del paquete `Microsoft.DesktopAppInstaller_8wekyb3d8bbwe` y se exige una firma Authenticode válida de Microsoft antes de ejecutarlo. La GUI rechaza iniciarse elevada; el instalador puede solicitar permisos. Hay que validar este flujo y la comprobación de firma en Windows real. No hay alternativa automática que omita la verificación cuando falla.

## Limpieza selectiva: implementación inicial

La etiqueta de «bloatware» depende del uso. Por eso la pantalla se llama **Limpieza de apps**. El catálogo inicial permite analizar y quitar Noticias, El Tiempo, Solitaire Collection y Clipchamp, si están registrados para el usuario actual. No se evalúan otros paquetes ni se usan comodines para decidir qué eliminar.

El análisis es de solo lectura. Después, el usuario selecciona apps concretas y revisa la pérdida de función y posible pérdida de datos. La ejecución usa `Get-AppxPackage -Name <identificador permitido>` y `Remove-AppxPackage -Package <nombre completo detectado>`, sin `-AllUsers` ni eliminación de paquetes aprovisionados. Se comprueba después que no queden registros coincidentes para ese usuario.

No se quitan Store, Windows Update, Defender, Edge/WebView2, Xbox/Gaming Services, controladores o servicios del fabricante mediante este catálogo. Pueden existir dependencias de juegos, seguridad, dispositivos y aplicaciones. La ausencia de una app para un usuario no significa que se haya eliminado del disco para todos o que no pueda volver a instalarse.

Desinstalar puede perder datos locales. Reinstalar desde Store no garantiza recuperarlos. `Remove-AppxPackage -PreserveApplicationData` no proporciona una copia de seguridad general: Microsoft limita esa opción a ciertos paquetes de desarrollo. No se ofrece una restauración de datos inexistente ni una promesa de más FPS por eliminar aplicaciones inactivas.

## Perfiles de optimización: diseño, aún sin ejecución

| Nivel | Alcance previsto | Condiciones |
| --- | --- | --- |
| Conservadora | Recomendaciones y ajustes compatibles acotados a necesidades detectadas. | Selección explícita, explicación y restauración del estado previo cuando sea reversible. |
| Intensiva | Revisión más amplia del inicio automático, captura en segundo plano y funciones opcionales que el usuario no utiliza. | Mostrar pérdidas de función y dependencias. Medir si mejora. No seleccionar apps para eliminar sin revisión. |
| Experimental | Investigación con protocolos reproducibles en máquinas de prueba. | No es un perfil automático disponible en la versión actual. Una hipótesis no se presenta como mejora comprobada. |

Un nivel intensivo no garantiza cero fallos de software ni ausencia de pérdida de funciones. No aumenta voltajes, frecuencias o límites de potencia, no cambia BIOS/firmware ni desactiva seguridad o anticheats. Las consecuencias de consumo/temperatura de un plan de energía siguen dependiendo del hardware.

## Registro: requisitos antes de habilitar un ajuste

Cada acción necesita una ficha con:

- Propósito, fuente y calidad de evidencia: documentar una clave no demuestra una mejora de rendimiento.
- Windows/build/edición aplicables, hardware y requisitos; excluir equipos administrados cuando haya políticas que deban respetarse.
- Ruta y nombre exactos, tipo, estado actual —incluida ausencia— y estado propuesto.
- Beneficio esperado, funciones afectadas y riesgos; reinicio o cierre de sesión si corresponde.
- Snapshot del estado real antes del cambio; aplicar, leer para verificar y registrar el resultado.
- Restauración de lo que se tocó, con comprobación de conflictos posteriores. Conservar datos añadidos por otros programas.

**No hay todavía ajustes de registro aprobados o implementados.** Las opciones de Game Bar, por ejemplo, son candidatas a investigar cuando haya grabación activa o un problema concreto; deshabilitarlas no se etiqueta como un aumento universal de FPS. Privacidad, cosmética y rendimiento se presentan como objetivos distintos.

Por petición del usuario, se conservan visibles todos los controles de la referencia y cuatro candidatos adicionales, incluso los desaconsejados como ajuste general de gaming. Son 71 fichas con la etiqueta **Requiere evaluación**, riesgos y metadatos de la referencia. No son cambios propuestos ya aprobados para el equipo ni interruptores conectados a ejecución. Los valores mostrados de la referencia no son valores detectados en el sistema.

Un punto de restauración es una capa adicional cuando esté disponible, no un sustituto de los snapshots por acción ni una copia de los proyectos del usuario.

## Timers y latencia: resultados de la investigación

- Microsoft identifica `useplatformclock`, `useplatformtick` y `disabledynamictick` como opciones de depuración. No se aplican como perfil de gaming ni se modifican arranque/HPET automáticamente.
- Resolución de timers, contador de rendimiento y latencia de entrada son conceptos diferentes. `timeBeginPeriod` no mejora la precisión de QPC. Desde Windows 10 2004 su comportamiento dejó de ser una petición global universal; Windows 11 añade condiciones para procesos ocultos/minimizados.
- Una resolución más alta puede aumentar consumo, impedir estados de ahorro y reducir rendimiento. No se instalará un «timer booster» que prometa mejoras para todos los juegos.
- La documentación de MMCSS indica que `SystemResponsiveness` menor de 10 se limita a 20; escribir 0 no equivale a reservar 0 % de CPU. También describe `GPU Priority` como no utilizado. No se convertirán estos cambios populares en ajustes de rendimiento aprobados.
- Antes de diagnosticar latencia, distinguir red, tiempo de cuadro, audio y cadena de entrada/pantalla. Un resultado de DPC o ICMP no es una medición completa de click a fotón. No hay analizador de latencia implementado todavía.

## Fuentes oficiales consultadas

- Microsoft: [WinGet install](https://learn.microsoft.com/en-us/windows/package-manager/winget/install), [show](https://learn.microsoft.com/en-us/windows/package-manager/winget/show) y [manifiestos](https://learn.microsoft.com/en-us/windows/package-manager/package/manifest).
- Microsoft: [AppInstallerErrors.h](https://github.com/microsoft/winget-cli/blob/master/src/AppInstallerSharedLib/Public/AppInstallerErrors.h), códigos HRESULT de WinGet.
- Microsoft: [Remove-AppxPackage](https://learn.microsoft.com/en-us/powershell/module/appx/remove-appxpackage?view=windowsserver2025-ps), alcance y límites de conservación de datos.
- Microsoft: [BCDEdit /set](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/bcdedit--set), opciones de timers y riesgos del arranque.
- Microsoft: [Acquiring high-resolution time stamps](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps), selección de contadores por Windows.
- Microsoft: [timeBeginPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod), consumo y cambios por versión.
- Microsoft: [Multimedia Class Scheduler Service](https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service), valores y comportamiento de MMCSS.

La comparación con otros optimizadores ayuda a encontrar implementaciones y posibles fallos; la aprobación de un ajuste para gaming necesita además documentación aplicable y mediciones. No se han ejecutado instalaciones, desinstalaciones o benchmarks de juegos en este entorno Linux.
