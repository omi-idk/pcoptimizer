# Auditoría de arquitectura — base Ultimate

## Alcance y ramas

Inspección del código y CI el 15 de octubre de 2026 (hora de México). Se conserva literalmente el [requisito recibido](requirements/PCOptimizer-Ultimate-recibido.md): el archivo termina en «No prometas reducir el ping a los servidores de». No se inventan secciones faltantes.

`git ls-remote` y `git fetch` comprobaron `main` en `3352684` y `work` en `9281e26`. `main` ya integra la versión anterior y cuatro actualizaciones de dependencias/Actions; `work` aporta el rediseño 0.3, 41 ajustes, perfiles y capturas WPF. Se creó `feature/ultimate-benchmark-foundation` desde `work` y se integró `origin/main`, preservando ambos historiales. No se reemplazó ninguna rama.

## Componentes y estado observado

| Componente | Código revisado / operación real | Estado y límites |
| --- | --- | --- |
| WPF .NET 10 | App, MainViewModel, MainWindow y parciales, temas y diálogos | Implementado; renderizado real en CI anterior. Mucha coordinación en ventana (~950 líneas sumando parciales). No es MVVM completo. |
| Registro | Core/ExecutableTweaks, AdditionalExecutableTweaks; App/WindowsTweakValueStore | 41 operaciones reales en lista compilada, validación de tipos, snapshots, conflictos, lectura posterior. Persistir un valor no demuestra que Windows lo use. Pruebas del motor usan un proveedor simulado; falta matriz Windows real. |
| Recuperación | TweakEngine, RecoveryStore, LocalJsonFile, WindowsRecoveryService | Diario previo, escritura temporal/flush, reversión por valor; punto Windows opcional antes de actuar, puede fallar por políticas/límite de Windows. No es transacción global ni imagen del disco. |
| Elevación | MachineTweakScript y proveedores Windows | Lote cerrado de DWORD por UAC; chequeo antes de cada escritura. Consultas/puntos por pipe restringido a usuario y PID. GUI rechaza inicio elevado. Falta prueba de integración con UAC denegado, políticas y reinicios. |
| Instalación / limpieza | SoftwareServices, SoftwareCatalog, ActionJournal | WinGet exacto, 236 entradas, 18 Appx opcionales del usuario actual. La desinstalación no respalda datos. No hay rollback de instalaciones. |
| Energía | WindowsServices y PowerHistory | Cambio real entre planes existentes, estado previo y restauración con conflictos. No cambia voltajes. Falta prueba hardware y Modern Standby. |
| Inventario | CIM en WindowsServices | CPU/GPU/RAM total/placa/BIOS/Windows/discos; no topología CPU completa, VRAM física fiable, canales, sensores ni pantalla. |
| Drivers | WindowsDiagnostics | Inventario instalado y enlaces oficiales; NO compara última versión con recomendada ni instala drivers. |
| Red | WindowsDiagnostics + NetworkSampleSummary | 10 pings/destino, pérdidas observables, variación de respuestas consecutivas; NO DNS, bufferbloat ni tráfico de juego. |
| Almacenamiento/audio | WindowsDiagnostics y probe restringido | Salud/contadores cuando Windows los expone y panel nativo de audio. No hay vida útil predicha ni EQ universal. |
| Gaming | GetLoadAsync | Lectura puntual de CPU, RAM disponible y máxima instancia 3D. NO benchmark ni diagnóstico causal de cuello de botella. |
| Opciones por evaluar | TweakCatalog JSON | Referencias informativas; el JSON no ejecuta registro ni scripts. Se mantienen visibles por petición del usuario. |
| Pruebas / entrega | Core.Tests, UiPreview, ci.yml | Base anterior: 96 pruebas, capturas WPF con datos ilustrativos, publicación self-contained y CodeQL. No demuestra compatibilidad anticheat ni mejora de FPS. |

## Riesgos priorizados

1. **P0 — evidencia de rendimiento ausente.** Los perfiles son selecciones de preferencias, no resultados de benchmarks. Implementar importación/captura reproducible antes de recomendaciones automáticas.
2. **P0 — integraciones sin verificar.** Antes de una versión estable probar UAC permitido/denegado, cambios parciales, cierre inesperado, edición Home/Pro y cambios externos en VM Windows desechable. Nunca probar cambios del registro del anfitrión CI por defecto.
3. **P1 — coordinación acoplada.** La ventana combina navegación, recuperación, aplicaciones y eventos; las clases estáticas Windows no son sustituibles por DI. Extraer por funcionalidad con interfaces y cancelación, comenzando por benchmarks. Dividir en archivos parciales no resuelve el acoplamiento.
4. **P1 — capacidad y evidencia confluidas.** «Listo» significa lectura posible, no soporte HAGS comprobado. Separar disponible/no soportado/permisos/validación pendiente y registro observado/función activa/beneficio medido.
5. **P1 — perfiles condicionales.** Gaming incluye captura, ratón y accesibilidad: tienen efectos dependientes del usuario. La selección revisable actual no debe convertirse en activación automática. Crear perfiles de objetivos después del protocolo A/B.
6. **P1 — recuperación y concurrencia.** El chequeo/escritura no es atómico frente a programas externos; documentar este límite, conservar diario y rechazar conflictos. No borrar respaldos corruptos. Probar fallos reales de almacenamiento.
7. **P2 — deuda documental.** La revisión de optimizadores anterior menciona estados 0.1/0.2 ya superados; esta tabla y la hoja de ruta expresan el estado actual. No duplicar ejecutores para sumar opciones.
8. **P2 — superficies de entrada.** Archivos importados, CSV y futuros auxiliares deben tener límites, esquema, número finito, cancelación, hash fijo y argumentos separados. No ejecutar código de perfiles, CSV o rutas de un respaldo.

## Decisiones

Conservar WPF: controles, recuperación y CI ya existen; WinUI 3 añadiría empaquetado, interop y regresiones sin resolver medición ni privilegios. Adoptar MVVM por módulos; no crear una docena de proyectos vacíos. `Core` conserva contratos reutilizables; un módulo `Benchmarking` independiente de WPF alojará importación/cálculos/persistencia. Proveedores de captura Windows y presentación quedan separados.

No se ha realizado pentest, certificación anticheat ni validación térmica/hardware. CodeQL y auditoría NuGet son controles complementarios, no garantía de ausencia de vulnerabilidades.
