# Seguridad y condiciones de distribución

PCOptimizer es una base en desarrollo. La compilación, pruebas y auditoría de paquetes no certifican ausencia de vulnerabilidades, compatibilidad anticheat o recuperación completa. Antes de distribuir debe completarse la validación real de Windows indicada aquí y en README.

## Límites de confianza y controles implementados

- **Privilegios:** manifiesto `asInvoker`; la GUI rechaza iniciarse como administrador. Elevación limitada a energía, creación del punto, consulta opcional de almacenamiento y escrituras DWORD de los destinos HKLM permitidos. Un mutex por cuenta evita dos instancias escribiendo los registros desde sesiones distintas.
- **Comandos:** ejecutables de Windows por ruta absoluta; las importaciones nativas buscan en System32. Identificadores de software y desinstalación provienen de listas cerradas. Los GUID de energía se contrastan con los planes disponibles. No se admite una consola de comandos o scripts proporcionados por el usuario ni scripts descargados de catálogos.
- **Instalación:** se busca App Installer por identidad de paquete de Microsoft y se comprueba Authenticode del ejecutable. WinGet recibe ID exacto y fuente explícita, conserva hashes y acuerdos y no actualiza instalaciones existentes automáticamente. No hay aceptación global de licencias ni descarga/ejecución automática de las herramientas del catálogo al abrir la app.
- **Proceso elevado:** creación de puntos mediante script fijo, GUID generado y módulos del sistema, sin omitir políticas de ejecución. Devuelve metadatos por named pipe restringida al usuario y al PID esperado, con límite de tamaño. No recibe rutas de escritura, texto de registros o scripts arbitrarios desde archivos del usuario.
- **Registros:** JSON de tipos concretos, validación de identificadores y asociaciones, límites de tamaño/profundidad, escritura temporal y sustitución, y preservación de registros inválidos. Un fallo al guardar el estado previo impide comenzar la operación. No se trata el historial como prueba de una instalación no verificada.
- **Concurrencia y fallos:** procesos por lote secuenciales, estado por acción, pasos no ejecutados diferenciados, ausencia de reintentos automáticos de operaciones ambiguas y comprobación de conflictos del plan antes de activarlo. No se termina una restauración del sistema por un timeout de lectura.
- **Cambios sensibles:** hay un motor de registro para una lista compilada de ajustes, con respaldo previo, validación, lectura posterior y reversión por valor. Las escrituras HKLM usan scripts fijos y valores DWORD tipados; no importan rutas o scripts de respaldos. Los candidatos restantes del catálogo solo informan. No hay cambios de BCD, BIOS, servicios o desactivación de seguridad conectados. No se inyecta código en juegos, no se lee su memoria y no se incluyen drivers propios.
- **Datos:** no hay backend, cuentas ni envío de telemetría de la aplicación implementados. Los registros locales contienen planes, software, resultados y puntos, no contraseñas o tokens. Software externo, WinGet y Windows tienen sus propias conexiones, términos y tratamiento de datos. La prueba de red, iniciada por el usuario, envía ICMP a la puerta de enlace detectada y a 1.1.1.1/8.8.8.8; no envía inventario o registros.

Estos controles no protegen frente a un Windows ya comprometido, un administrador malicioso, la sustitución de toda la aplicación, un firmante comprometido o el comportamiento completo de un programa externo. El hash de un instalador y su presencia en un catálogo no constituyen un análisis antimalware ni una certificación de su editor.

## Dependencias, herramientas y CI

- SDK fijado en `global.json`: .NET 10.0.402. Se consultó la [metadata oficial de .NET 10](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json); la versión corresponde a la publicación de seguridad del 13 de octubre de 2026. El archivo Linux se verificó con el SHA-512 publicado antes de extraerlo. Mantener SDK y runtime al día forma parte del mantenimiento continuo.
- Dependencias NuGet fijadas mediante lockfiles y restauración `--locked-mode`. Auditoría de dependencias directas y transitivas activada. Los avisos de vulnerabilidades y fallos de auditoría configurados son errores de restauración.
- Acciones de GitHub fijadas a SHA comprobados en sus repositorios oficiales. Checkout sin persistencia de credenciales; permisos de lectura por defecto y escritura de eventos de seguridad solo para CodeQL.
- Flujo CodeQL de C# configurado, con compilación explícita en Windows. Dependabot configurado para proponer actualizaciones de NuGet y acciones, sin fusión automática. **Configurado no equivale a ejecutado:** no se han ejecutado ni revisado resultados de CodeQL remoto en esta conversación; su disponibilidad también depende de GitHub y ajustes/licencia del repositorio.
- Analizadores de seguridad de .NET activados como advertencias; la app y el núcleo tratan advertencias como errores.

La consulta de NuGet realizada durante la revisión no reportó paquetes vulnerables en las fuentes consultadas. Es un resultado de esa consulta, no una garantía sobre el código propio, SDK, Windows o futuras publicaciones.

## Pruebas requeridas antes de publicar

1. **Windows y permisos:** usuario normal, administrador con UAC y credenciales de otra cuenta. Comprobar que la GUI elevada se rechaza y los errores dejan el sistema y los registros en estados explicables.
2. **Integridad de WinGet:** paquete ausente, ejecutable sin firma o firma no confiable, rutas/alias falsos en PATH y versiones antiguas. Verificar que no hay ejecución alternativa que evite la comprobación.
3. **Canal elevado:** PID incorrecto, otro usuario, mensajes grandes o truncados, cancelación, cierre del proceso y resultados incoherentes. Estos casos requieren Windows y todavía no se ejecutaron aquí.
4. **Recuperación real:** protección desactivada, límite de 24 horas, espacio insuficiente, fallo de escritura y punto eliminado después. Comprobar una recuperación en un equipo de prueba preparado para reinicios; los puntos no sustituyen copias de datos personales.
5. **Operaciones reales:** instalaciones con acuerdos rechazados, omisiones, reinicio requerido y fallos parciales; desinstalación limitada al usuario y a IDs permitidos; energía cambiada por otro programa durante el respaldo.
6. **Interfaz y privacidad:** selección completa tras filtros, confirmaciones legibles, accesibilidad, ausencia de secretos en errores/exportaciones y revisión del contenido de logs. No hay exportación automática implementada.
7. **Compatibilidad:** varias compilaciones de Windows 11, equipos OEM, laptops, idiomas y juegos/anticheats. Un ensayo sin problemas no garantiza compatibilidad permanente.
8. **Distribución:** firma de nuestros binarios, verificación del artefacto final, mecanismo de actualización, licencias y revisión independiente del código sensible. Firma propia, instalador y actualizador aún no están implementados; no deben anunciarse como listos.

Las herramientas del catálogo incluyen utilidades de administración, red y automatización con usos legítimos y posibles usos indebidos. PCOptimizer limita sus propias operaciones, informa condiciones y no las ejecuta para automatizar juegos, evadir anticheats o eludir controles. No puede garantizar cómo utilizará otra persona un programa independiente.

Para comunicar una vulnerabilidad, proporciona al mantenedor una descripción reproducible y versiones afectadas por un canal privado disponible. Evita publicar credenciales, datos personales o material de otros usuarios.

## Motor de ajustes de registro

Consulta [el alcance y protocolo](docs/ajustes-aplicables.md). Se rechazan destinos o valores solicitados fuera del catálogo, referencias circulares de reversión, tipos no soportados, copias corruptas y operaciones sin respaldo durable. Se comprueba el estado antes del lote, antes de cada valor y, en HKLM, después de UAC. La comprobación no es una transacción atómica ni impide escrituras posteriores de otros programas. Las pruebas de lógica simulan fallos parciales y concurrencia; falta validar las APIs reales, scripts elevados y políticas efectivas en Windows.

La consulta elevada de almacenamiento ejecuta únicamente la consulta de lectura compilada, envía JSON por una pipe restringida a usuario/PID y limita su respuesta a 256 KiB/profundidad 8. No escribe archivos elevados ni cambia discos. Su timeout solo puede terminar esa consulta de lectura; no se reutiliza para crear puntos de restauración. También requiere pruebas reales de permisos y canal en Windows.

## Preview 0.3: cambios de confianza

Los perfiles importados contienen solo identificadores del catálogo; se rechazan desconocidos, duplicados y conflictos. Las banderas de accesibilidad/teclado se modifican con máscara preservando bits ajenos. Los destinos HKLM de un lote se respaldan antes de una sola elevación; el script se construye en `MachineTweakScript` con identidades cerradas, booleanos y UInt32. El perfil no puede aportar rutas o código. El parser de PowerShell en CI valida sintaxis de aplicación/reversión sin ejecutar escrituras. Las pruebas de lógica cubren el lote parcial y su reversión; no sustituyen validar UAC y políticas efectivas en un Windows real.

El renderer de diseño es una herramienta separada no distribuida en el portable. Instancia la vista con datos ilustrativos, bloquea interacción y no inicia diagnóstico o tweaks. No incorpora un argumento para omitir la comprobación de privilegios de la aplicación de producción. Sus capturas no son mediciones ni resultados de prueba de hardware.

## Resource and gaming-session boundaries

Optional NVML is loaded only by absolute path from Windows System32 and uses read-only exports. No vendor DLL or kernel sensor driver is downloaded/bundled. Executable watching uses QUERY_LIMITED_INFORMATION/QueryFullProcessImageName, not VM read/write or module enumeration. Windows Update driver search retrieves metadata only and does not download/install drivers or change policies.

Gaming energy sessions persist the previous plan and application-attempt state before requesting the fixed powercfg change. Restoration rechecks active/available plans, rejects external conflicts and never recreates a deleted plan. Journals remain after failure/crash; automatic recovery does not silently overwrite a new configuration. The shared UI gate prevents overlapping system mutations or benchmarking. Native Windows UAC/driver/game validation remains required.
