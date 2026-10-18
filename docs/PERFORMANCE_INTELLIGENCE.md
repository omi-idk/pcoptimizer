# Preview 0.6 · primer incremento de Performance Intelligence

## Flujo implementado

En **Benchmarks**, selecciona el auxiliar oficial PresentMon 2.6.0 x64, actualiza procesos y elige el juego abierto. Marca **Incluir sensores** y una duración de 5–600 segundos. La captura es voluntaria; no cambia ajustes, prioridades, seguridad ni archivos del juego. El auxiliar se obtiene separadamente y conserva su verificación SHA-256. Los permisos ETW y la compatibilidad con el juego siguen pendientes de validación física.

Al terminar, selecciona el stream si hay más de uno y pulsa **Analizar coincidencias**. Completa hardware, juego/build, driver, Windows, ajustes y escenario; pulsa **Guardar sesión**. El historial permite **Cargar** el experimento completo y recalcula su evidencia. JSON conserva frames, sensores y contexto; **Informe de rendimiento** produce Markdown con hallazgos, evidencia, hipótesis, confianza, faltantes, recomendación, riesgos y experimento sugerido. CSV conserva QPC por frame, pero no las series de sensores: usa JSON para ese fin. Las exportaciones JSON son informes con una propiedad `Session`; se reabre el historial interno, no un JSON externo.

El perfil específico 5800X3D/5060 Ti se conserva como caso experimental. Registra la configuración real de Warzone: 1080p/bajo es un escenario propuesto, no una lectura automática. No hay capturas reales del propietario incluidas.

## Reloj y procedencia

Las capturas nuevas solicitan `--qpc_time`. Se importa **CPUStartQPC** como entero de 64 bits, sin convertirlo primero a `double`. `0`/NA/ausencia indican timestamp desconocido. Es el inicio del trabajo CPU del frame, mientras `MsBetweenPresents` mide el intervalo entre llamadas Present: no son el mismo evento. El desfase y la granularidad del proveedor impiden atribuir un spike a un instante exacto.

`Stopwatch.GetTimestamp/Frequency` en Windows emplea QPC. El colector guarda frecuencia, límites QPC, UTC inicial, ID de captura, PID y creación del proceso elegido, y comienzo/fin QPC y UTC de cada consulta. UTC sirve de contexto, nunca de clave de correlación. Se adjuntan sensores únicamente al CSV obtenido en esa misma operación; no se mezcla un CSV antiguo con una lectura actual. QPC de otro arranque/equipo no es comparable. SHA-256 identifica el contenido del CSV; no es firma ni prueba de autenticidad de mediciones.

La extensión del esquema 1 es aditiva: `CpuStartQpc` y `Telemetry` son opcionales. Las sesiones previas siguen abriendo con análisis sin atribución; no se inventan timestamps sumando intervalos. No hay migración destructiva ni cambio de los cálculos anteriores.

## Proveedores y coste

- Memoria física usada: `GlobalMemoryStatusEx`, %. No mide commit/paginación ni prueba presión de memoria por sí sola.
- CPU total: diferencias de `GetSystemTimes`, %. Primera lectura no disponible. Promedio desde lectura anterior; no representa uso por hilo ni identifica al juego. En equipos >64 procesadores existen limitaciones por grupos.
- NVIDIA: NVML del driver en System32, utilización %, temperatura °C, reloj gráfico MHz, potencia W y VRAM usada MiB cuando se expongan. Cada dispositivo queda separado por índice/nombre NVML; no hay asociación automática al adaptador que utiliza el juego. La ventana interna de agregación puede ser desconocida. Ausencia es `Unavailable`/null, nunca cero. Se conserva la ventana QPC nativa; una respuesta pendiente anterior no se reetiqueta como lectura nueva.

Cadencia: espera de 500 ms después de cada ciclo, sin recuperar intervalos atrasados. No se mantienen sondeos cuando termina la operación. Cancelar una espera NVML no puede abortar una llamada nativa ya en curso: esta puede terminar después; no se inicia otra simultánea. Las observaciones de la traza quedan congeladas antes de devolverla.

Se registra tiempo CPU del **proceso completo PCOptimizer**, incluyendo GUI, excluyendo el proceso PresentMon; no equivale al overhead causal del colector. La inicialización repetida NVML puede tener coste. Debe medirse externamente antes de recomendar esta captura como ligera. No hay sensores térmicos CPU, clocks efectivos, I/O, procesos/hilos ni proveedores GPU AMD/Intel en este incremento. Las lecturas existentes de otros módulos permanecen disponibles por separado.

## Regla reproducible y límites

Spikes: intervalo >máx(50 ms, 2×mediana), sin eliminar outliers. Se conserva la definición de lows y percentiles de [Benchmarks](BENCHMARKING.md).

Por serie, se busca la observación cuyo centro QPC está más cerca del inicio CPU de cada frame. Solo se admite distancia ≤500 ms y consulta ≤250 ms. Promedios con ventana conocida >1 s se omiten; `AggregationSeconds=null` significa ventana desconocida, no lectura instantánea. No se interpola ni extrapola fuera de la captura. Si la lectura más próxima no está disponible o fue lenta, no se sustituye por otra. El informe muestra cobertura de frames y spikes por serie. La gráfica utiliza el mismo rango QPC: frametimes arriba, porcentajes abajo, con puntos sin unir; los valores térmicos/eléctricos permanecen en JSON, todavía sin selector gráfico propio.

Una hipótesis de coincidencia de carga ≥90% exige simultáneamente:

- ≥80% de todos los frames con QPC y ≥80% de frames cubiertos por la serie.
- ≥3 spikes cubiertos y ≥80% de todos los spikes cubiertos.
- ≥10 frames normales cubiertos.
- Carga alta en ≥80% de spikes y ≤60% de frames normales; diferencia ≥20 puntos porcentuales.
- ≥3 observaciones de sensor distintas coincidiendo con spikes altos. No se presentan como repeticiones estadísticamente independientes.

Aplica a CPU global, memoria física y utilización GPU. Confianza **baja** y asociación exploratoria, no diagnóstico de cuello de botella. Carga alta constante no explica spikes por esta regla; temperatura sola no implica throttling. Si falta evidencia aparece **Sin atribución concluyente**. Esto tampoco descarta un problema. La recomendación es un experimento controlado, sin aplicación automática ni promesa de mejora.

Límites de entradas: 64 MiB por sesión/CSV; 200000 frames; 10000 observaciones; 128 series y lecturas por observación; 700 s de traza; tipos, unidades, valores finitos, rangos y ventanas ordenadas/no solapadas. Los archivos del historial son inmutables y se escriben mediante temporal/flush/move. Cancelar análisis/carga mantiene el estado anterior; cancelar guardado no publica un archivo parcial. Al detener PresentMon puede importarse un CSV parcial explícitamente identificado, junto a las observaciones completas obtenidas hasta detenerse.

## Validación disponible y procedimiento físico pendiente

Pruebas de dominio cubren correlación/rechazo, carga constante, datos ausentes, consultas lentas, huecos, lectura vecina no disponible, distinta GPU/PID, múltiples frames por lectura, QPC >2^53, entradas inválidas, persistencia, cancelación e informes. CI Windows añade sondeo de memoria real con GPU simulada ausente, parada/cancelación del colector y renderizado WPF con datos **ilustrativos**. Esto no valida un juego, la telemetría de una GPU física ni mejoras de rendimiento.

| Entorno | Validación | Estado |
| --- | --- | --- |
| Linux cloud / .NET 10 | Dominio, archivos, cross-build WPF | Automatizada; consultar resultado de la entrega |
| GitHub windows-latest | Build/tests, renderizado WPF, memoria y cancelación de solo lectura | Pipeline; consultar ejecución del PR |
| Windows 11 del propietario / 5800X3D + 5060 Ti | NVML, PresentMon, Warzone/RICOCHET, overhead y recuperación real | Pendiente |
| Otros fabricantes, laptops, híbridos y múltiples GPUs físicas | Cobertura y coste real | Pendiente |

En Windows 11:

1. Extrae todo el artefacto; abre `PCOptimizer.App.exe` sin elevar. Conserva las protecciones de Windows. No uses cambios de seguridad para habilitar ETW.
2. Primero prueba un proceso gráfico no protegido: 15–30 s con sensores, contexto completo y guardado. Confirma QPC y cobertura; sin NVML debe aparecer limitación, no GPU=0%.
3. Reabre desde historial y exporta JSON/informe. Confirma mismo ID de sesión, timestamps, valores, cobertura y hallazgos.
4. Cancela otra captura a mitad; verifica que la app vuelva a estado disponible y el historial anterior permanezca íntegro. Un CSV parcial no equivale a la duración solicitada.
5. Para medir overhead, usa una herramienta externa estable y escenario controlado: alterna 3–5 sesiones con PCOptimizer cerrado, abierto sin sensores y capturando con sensores. Mantén herramienta externa, calentamiento, límites y escena constantes. Compara variabilidad, medias por repetición, p99 y lows; no fijamos un porcentaje aceptable sin medir.
6. Antes de Warzone, revisa condiciones y restricciones vigentes de RICOCHET. Si hay bloqueo, detén la prueba y registra el error, sin intentar eludirlo. La compatibilidad todavía no está certificada. Realiza el protocolo del [perfil de tu PC](PERFIL_PC_WARZONE.md).

Próximo incremento: tiempos GPU/CPU del proceso, mejor identidad de adaptador, alineación con eventos Present, selector de sensores/zoom, protocolo A/B persistido y evidencia más específica. La 0.7 añadirá optimización guiada por capacidades; esta entrega no la presenta como terminada.

Fuentes oficiales consultadas: [PresentMon 2.6 consola](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/README-ConsoleApplication.md), [escritor CSV](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/PresentMon/CsvOutput.cpp), [Microsoft QPC](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps), [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes), [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex), [NVIDIA NVML](https://docs.nvidia.com/deploy/nvml-api/).
