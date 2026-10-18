# Recursos, sensores y sesiones gaming — Preview 0.5

## Funciones conectadas a la interfaz

En **Uso de recursos**: Inventario detallado, Analizar recursos (20 muestras), Exportar captura JSON, Evaluar mi PC/Warzone y Seleccionar base Warzone. Las consultas CPU/memoria son cancelables y no escriben configuraciones. La aplicación impide iniciar otras operaciones durante el muestreo.

El inventario informa núcleos/procesadores lógicos, cachés y frecuencia máxima declarada por CIM, módulos/capacidad/velocidades SMBIOS y modo de vídeo informado por Win32_VideoController. No se confunden con frecuencia efectiva, canales de memoria, XMP activo, enumeración fiable de cada monitor, HDR/VRR ni FPS. CPU híbridas y grupos de procesadores requieren proveedores adicionales; el contador heredado puede omitir grupos. No se deduce saturación del hilo del juego a partir del uso total.

En NVIDIA, un proveedor opcional consulta **NVML instalado en System32**: nombre, VRAM total/usada, utilización GPU, temperatura, reloj gráfico, consumo y máscara de eventos de reloj cuando el driver los expone. Solo usa funciones de lectura; no instala drivers, no carga DLL desde PATH o descargas y no escribe voltajes ni límites. Cada valor puede ser N/D sin impedir el inventario CIM. La espera NVML tiene un límite de ocho segundos y una sola consulta nativa en vuelo: cancelar deja de esperar, no intenta terminar el driver. Temperatura/VRAM AMD e Intel y temperatura CPU permanecen sin proveedor. La máscara se muestra en hexadecimal, sin atribuir automáticamente throttling térmico. Consumo/utilización pueden estar promediados por el propio driver y no son métricas del proceso del juego.

También se consultan versión TPM por CIM, Secure Boot por API y VBS/HVCI por Win32_DeviceGuard. Los errores de permisos/proveedor son «No informado», nunca «desactivado». VBS distingue habilitado de en ejecución; no se modifican estas protecciones ni se infiere certificación anticheat. Secure Boot puede requerir permisos que la GUI normal no tenga y no se eleva automáticamente para leerlo.

## Reglas del diagnóstico

`ResourceDiagnostics` es independiente de Windows. Valida números, orden, duplicados y límites. Exige diez muestras y ocho segundos como mínimo, sin huecos mayores de cinco segundos. Para cada regla requiere cobertura en al menos el 80% de las muestras; falta de datos no significa ausencia de presión.

- CPU lógica: observación ≥90% en al menos el 80% de las lecturas de ese procesador lógico. La carga puede provenir de cualquier proceso o migrar entre CPUs. No identifica un hilo crítico del juego.
- Memoria: simultáneamente disponible <5%, compromiso ≥90% y Pages Input/sec >5 en al menos el 80% de las muestras cubiertas. Pages Input incluye archivos mapeados; no equivale automáticamente a acceso al pagefile ni demuestra un problema del juego.
- Confianza cualitativa moderada en la observación, baja o insuficiente cuando corresponde; **no es una probabilidad ni un diagnóstico causal**. Los umbrales son heurísticos, no una certificación de mejoras.

Cada resultado incluye evidencia, información faltante, sugerencia, riesgos y validación. Los datos no están sincronizados con PresentMon: la correlación automática con frametimes sigue pendiente. La exportación conserva UTC inicial, tiempos relativos, lecturas y limitaciones. No limpia standby, shaders ni memoria.

## Sesiones gaming de energía

En **Energía → Sesiones gaming**:

1. Actualizar, seleccionar un juego abierto y otro plan existente, revisar e iniciar; o elegir **Esperar inicio de un ejecutable**. La espera es voluntaria, solo una vez y mientras la ventana siga abierta. No inicia el archivo ni se reactiva sola después de reiniciar.
2. Se identifica el proceso por PID y hora de inicio. La espera compara también ruta completa mediante QueryFullProcessImageName y QUERY_LIMITED_INFORMATION. No usa PROCESS_VM_READ/WRITE, enumera módulos ni toca memoria, prioridades o afinidades.
3. Se guarda el plan original y la intención antes de cualquier cambio. Se respeta la opción de punto de restauración de la ventana principal; si Windows lo impide, el usuario decide continuar sin un nuevo punto o cancelar. El punto registra ID de sesión, proceso y GUID anterior/propuesto.
4. Se verifica el estado tras aplicar. Puede requerir UAC. Al terminar el juego se intenta restaurar; si hay benchmark/otra operación se espera sin modificar energía. La restauración puede requerir otro UAC o decisión sobre el punto. Una cancelación/fallo queda pendiente, sin bucle de solicitudes.
5. La reversión relee planes y rechaza cambios externos. Si el original ya está activo, no se escribe ni se pide un punto para un cambio inexistente. Nunca se recrea un plan eliminado ni se alteran sus opciones internas.
6. Tras un cierre inesperado, se avisa al iniciar. La recuperación se revisa desde esa ventana: no se sobrescribe configuración automáticamente. Un registro preparado que nunca intentó aplicar no puede deshacer el cambio de otro programa, incluso después de detectar un conflicto.
7. **Dejar pendiente** detiene la supervisión por decisión explícita y conserva el diario para recuperar después. **Conservar un cambio externo** solo reconoce un conflicto ya detectado, sin escribir energía. No se eliminan los registros. Las últimas 40 sesiones, incluidas las ya restauradas, aparecen en Historial con planes e ID para relacionarlas con el punto de Windows.

Los diarios están en `%LOCALAPPDATA%\PCOptimizer\GamingSessions`, con esquema, identidad, planes, estado y errores. Escritura temporal/flush/rename; un diario corrupto bloquea nuevas sesiones. Una sola sesión pendiente bloquea otra. La GUI serializa operaciones del motor; la selección de plan del sistema no puede hacerse atómica frente a otros programas. Se conserva ese límite y se comprueba el estado antes/después.

Esta entrega automatiza la **selección temporal de energía**, no un optimizador de todos los procesos. No termina programas, no instala un servicio residente, no aplica automáticamente los tweaks condicionales de un perfil y no acredita aumento de FPS. Las exclusiones para cierre de procesos no se necesitan para esta implementación porque ningún proceso ajeno se termina.

## Red y drivers

En **Red**, la consulta de adaptadores observa dos segundos de tráfico, velocidad de enlace, errores acumulados, DNS configurados y puertas. La tasa no es velocidad contratada. Contadores reiniciados no generan tasas negativas/inventadas. Resolver Microsoft/Cloudflare es una acción explícita, con timeout y posible caché local; no compara resolutores ni mide ping del juego. No hay descarga de carga, cambios TCP/DNS/IPv6/offloads ni diagnóstico de bufferbloat sin ensayo bajo carga.

En **Drivers**, la nueva búsqueda usa `Microsoft.Update.Session` y `IUpdateSearcher.Search` con filtro de controladores no instalados/no ocultos. Consulta metadatos del servicio configurado (incluido WSUS según políticas), no descarga ni instala controladores. Una oferta aplicable no demuestra que sea la última del fabricante ni la mejor para un juego. Se conserva inventario instalado y acceso a fuentes oficiales. La consulta tiene timeout de 45 s; el agente de Windows puede continuar su búsqueda tras finalizar el cliente. No se detienen servicios. El proveedor de recomendaciones de fabricantes sigue pendiente.

## Fuentes y límites de validación

- [Win32_Processor](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-processor), [Win32_PhysicalMemory](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-physicalmemory), [Win32_VideoController](https://learn.microsoft.com/windows/win32/cimwin32prov/win32-videocontroller).
- [NVML oficial](https://docs.nvidia.com/deploy/nvml-api/index.html) y [encabezado publicado por NVIDIA](https://github.com/NVIDIA/go-nvml/blob/main/pkg/nvml/nvml.h). Se contrastaron firmas de lectura y estructuras memory/utilization. No se redistribuye la biblioteca ni se copió su implementación. Funciones antiguas pueden ser no soportadas por drivers nuevos: N/D explícito.
- [Confirm-SecureBootUEFI](https://learn.microsoft.com/powershell/module/secureboot/confirm-securebootuefi) y [VBS/Win32_DeviceGuard](https://learn.microsoft.com/windows/security/hardware-security/enable-virtualization-based-protection-of-code-integrity).
- [QueryFullProcessImageName](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew), [derechos de proceso](https://learn.microsoft.com/windows/win32/procthread/process-security-and-access-rights).
- [IUpdateSearcher.Search](https://learn.microsoft.com/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-search), [IWindowsDriverUpdate](https://learn.microsoft.com/windows/win32/api/wuapi/nn-wuapi-iwindowsdriverupdate).

Las pruebas del motor usan un proveedor simulado (incluyen escritura seguida de fallo, reinicio del motor, conflictos, PID reutilizado y diario corrupto). Windows CI analiza scripts sin ejecutarlos, prueba cancelación de una consulta inofensiva, comprueba layouts NVML y renderiza la UI con datos ilustrativos. No aplica cambios de energía ni registro al anfitrión CI. El proveedor NVML puede informar no disponible en el runner; esto verifica degradación controlada, no los sensores de tu GPU.

Antes de una versión estable falta validar en Windows real: UAC concedido/denegado, Modern Standby, desaparición del proceso/plan, recuperación después de cerrar/terminar la app, espera de ruta protegida, WUA con políticas/errores y lectura de NVML en la GPU física. No hay certificación de anticheats ni garantía de FPS.
