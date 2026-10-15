# Revisión de implementaciones existentes

Revisión de código y documentación: 15 de octubre de 2026. Se descargaron instantáneas para lectura; no se ejecutaron sus scripts ni sus pruebas sobre este entorno Linux. La revisión se centra en instalación, selección de ajustes, eliminación de aplicaciones y restauración. No es una auditoría completa ni un benchmark comparativo de estos proyectos.

## Referencias fijadas

| Proyecto | Instantánea revisada | Licencia del repositorio |
| --- | --- | --- |
| [WinUtil / Chris Titus Tech](https://github.com/ChrisTitusTech/winutil) | [`07ccd8e`](https://github.com/ChrisTitusTech/winutil/tree/07ccd8e2e755a706f31569808b31f5b77acad6a9) | MIT; copyright CT Tech Group LLC |
| [Sophia Script](https://github.com/farag2/Sophia-Script-for-Windows) | [`3a24646`](https://github.com/farag2/Sophia-Script-for-Windows/tree/3a24646741c56be258f326d63523f105a3fec467) | MIT; copyright farag2 |
| [Win11Debloat](https://github.com/Raphire/Win11Debloat) | [`4079b27`](https://github.com/Raphire/Win11Debloat/tree/4079b276e9e93aa8698a9f70f5aa228ba11f8265) | MIT; copyright Raphire |

Se toman patrones de diseño y hechos de interfaces públicas. El catálogo de software adapta los 236 registros de WinUtil y el catálogo informativo de ajustes adapta los metadatos de sus 67 controles. Se conservan la licencia MIT completa y los avisos, que se distribuyen con la aplicación. No se incorporan ni ejecutan sus scripts de aplicación/deshacer. El Wrapper de Sophia se describe en su README como un componente cerrado de terceros: la licencia MIT del repositorio no debe extrapolarse a ese producto.

## Hallazgos y aplicación a PCOptimizer

### WinUtil

Archivos revisados: [instalación con WinGet](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/functions/private/Install-WinUtilProgramWinget.ps1), [aplicación/deshacer ajustes](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/functions/private/Invoke-WinUtilTweaks.ps1), [presets](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/preset.json) y entradas representativas de [tweaks.json](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json).

- Su catálogo y presets separan selección de ejecución. **Aplicado:** catálogo categorizado, búsqueda y revisión de una selección completa. La importación de perfiles todavía no está implementada.
- La función de WinGet produce resultados por paquete y distingue omisiones, fallos y reinicios. **Aplicado:** el núcleo de PCOptimizer ahora clasifica códigos de salida, conserva resultados por operación y detiene el lote ante solicitudes de reinicio. Los valores HRESULT se contrastaron con el [encabezado oficial de Microsoft](https://github.com/microsoft/winget-cli/blob/master/src/AppInstallerSharedLib/Public/AppInstallerErrors.h).
- El ejecutor de tweaks puede conservar el inicio de un servicio cuando no coincide con lo esperado. **Criterio adoptado:** detectar conflictos y respetar configuraciones previas, antes de ampliar los ajustes.
- En el trayecto revisado, deshacer usa `OriginalValue`/`OriginalType` del catálogo; eso no demuestra que se esté restaurando el valor personalizado anterior de cada equipo. Para PCOptimizer exigimos captura del estado real.
- Los presets incluyen cambios de privacidad, interfaz y limpieza. Su presencia no demuestra mejora en juegos. No se adoptará un preset entero como «gaming» sin validar cada acción.

### Sophia Script

Archivos revisados: [README](https://github.com/farag2/Sophia-Script-for-Windows/blob/3a24646741c56be258f326d63523f105a3fec467/README.md), [InitialActions para Windows 11](https://github.com/farag2/Sophia-Script-for-Windows/blob/3a24646741c56be258f326d63523f105a3fec467/src/Sophia_Script_for_Windows_11/Module/Private/InitialActions.ps1) y funciones `XBOXGameBar`, `XBOXGameTips` y eliminación UWP de [Sophia.psm1](https://github.com/farag2/Sophia-Script-for-Windows/blob/3a24646741c56be258f326d63523f105a3fec467/src/Sophia_Script_for_Windows_11/Module/Sophia.psm1).

- Las funciones describen propósito, parámetros opuestos y alcance de usuario. **Criterio adoptado:** cada ajuste tendrá una ficha independiente con esos datos y condiciones de compatibilidad.
- Comprobar requisitos antes de ejecutar es útil, pero el inicializador revisado también inicia/cambia servicios y configura la administración del archivo de paginación. En PCOptimizer el diagnóstico debe seguir siendo de solo lectura; las reparaciones se presentan como acciones explícitas separadas.
- `XBOXGameBar` ofrece habilitar/deshabilitar mediante valores concretos del registro. Tener ambas funciones no equivale a recuperar una configuración previa ni a demostrar un aumento de FPS. Es un candidato a investigar, no un ajuste aprobado en PCOptimizer.
- La eliminación de paquetes contempla alcance de todos los usuarios y, en ciertos casos, otro componente asociado. Nuestro alcance inicial es el usuario actual y cuatro aplicaciones independientes; cada dependencia adicional requerirá otra explicación y consentimiento.
- La declaración del README sobre VAC no certifica PCOptimizer ni otros anticheats. Nuestra matriz de juegos sigue sin ejecutar.

### Win11Debloat

Archivos revisados: [README](https://github.com/Raphire/Win11Debloat/blob/4079b276e9e93aa8698a9f70f5aa228ba11f8265/README.md), [backup de registro](https://github.com/Raphire/Win11Debloat/blob/4079b276e9e93aa8698a9f70f5aa228ba11f8265/Scripts/Features/Backup-RegistryState.ps1), [captura del estado](https://github.com/Raphire/Win11Debloat/blob/4079b276e9e93aa8698a9f70f5aa228ba11f8265/Scripts/Features/Backup-RegistrySnapshotCapture.ps1), [restauración](https://github.com/Raphire/Win11Debloat/blob/4079b276e9e93aa8698a9f70f5aa228ba11f8265/Scripts/Features/Restore-RegistryApplyState.ps1) y [eliminación seleccionada](https://github.com/Raphire/Win11Debloat/blob/4079b276e9e93aa8698a9f70f5aa228ba11f8265/Scripts/AppRemoval/Remove-SelectedApps.ps1).

- El backup captura existencia, tipos y datos; la restauración distingue valores ausentes de valores existentes. **Criterio adoptado:** conservar ausencia y tipo, no solo un supuesto valor por defecto. El motor de registro aún no está implementado.
- La eliminación incluye comprobación posterior, alcance de usuario y tratamiento de resultados incompletos. **Aplicado en nuestra base:** selección explícita, consulta del usuario actual, comprobación de ausencia después de eliminar e historial de operaciones pendientes antes de actuar.
- Ofrece funciones de despliegue, todos los usuarios y desinstalaciones diferidas. Esas funciones requieren otro alcance operativo; PCOptimizer no las ejecuta.
- Existen suites específicas para backup, validación, restauración y eliminación. Se tomó ese criterio para priorizar pruebas del historial y límites del catálogo. No se afirma haber ejecutado las suites ajenas.
- Una reinstalación desde Store no es una copia de los datos del usuario. PCOptimizer lo explica antes de eliminar. No promete revertir datos o proyectos borrados.

## Cómo refinar lo existente para gaming

1. **Comprobar beneficio:** mantener separados rendimiento, privacidad, espacio y personalización. Medir tiempos de cuadro y latencia antes y después cuando se anuncie una mejora.
2. **Explicar el cambio concreto:** mostrar alcance, valor actual, valor propuesto, funciones afectadas, riesgos y restauración; informar si falta una medición.
3. **Restaurar con detección de conflictos:** antes de deshacer, comprobar si el estado sigue coincidiendo con el que aplicamos. Restaurar solo lo tocado; una clave nueva no se elimina con todos sus hijos si otro programa añadió contenido después.
4. **Conservar funciones de gaming:** evaluar Game Pass, Xbox, Store, grabación, mandos, audio y anticheats como dependencias del usuario. No asumir que toda app de Windows es bloatware.
5. **Resultados precisos:** diferenciar aplicado, omitido, incompatible, pendiente de reinicio, fallido y no verificado. Un registro de ejecución no sustituye una lectura posterior del sistema.

Estas mejoras son requisitos verificables del proyecto. La restauración de energía, el catálogo inicial y el historial de software ya tienen implementación; el motor general de ajustes, los perfiles aplicables y los benchmarks siguen pendientes.
