# Benchmarks — Preview 0.4

## Qué funciona en esta rama

La página **Benchmarks** permite importar CSV PresentMon, escoger aplicación/PID/swapchain, calcular métricas, ver máximos temporales y distribución, guardar/cargar sesiones, exportar CSV/JSON y comparar resultados. El módulo `PCOptimizer.Benchmarking` es .NET puro, sin WPF ni dependencia de Windows. La vista tiene ViewModel propio y proveedores inyectables para captura/persistencia; la ventana principal solo navega hacia ella.

La captura manual requiere seleccionar el console oficial **PresentMon 2.6.0 x64**, disponible en el botón Obtener PresentMon. No se incluye ni instala automáticamente. Se comprueba SHA-256 antes de ejecutar y se mantiene un handle de solo lectura para impedir su sustitución durante la captura en Windows. Se selecciona un proceso con ventana y se verifica PID + hora de inicio antes de capturar. Procesos protegidos/inaccesibles no se fuerzan.

Duración admitida: 5–600 segundos. El auxiliar funciona como el usuario actual, con argumentos separados, nombre de sesión aleatorio y sin DLL inyectada. Usa `--process_id`, `--session_name`, `--timed`, `--terminate_after_timed`, `--terminate_on_proc_exit`, `--track_frame_type`. No termina sesiones de otras herramientas. Detener solicita cerrar únicamente nuestra sesión; si el auxiliar no responde tras el plazo se termina únicamente el proceso auxiliar creado por la app. Si queda un CSV cerrado con datos al detener, se importa como captura parcial. Un timeout/fallo conserva el CSV y muestra el error.

Mientras hay una operación de benchmarks, la aplicación bloquea los cambios del sistema y sus otras consultas; Detener permanece disponible. El módulo tampoco se inicia durante otra operación de la app. Los controles de captura se pliegan al importar datos para dejar visibles las métricas y gráficas.

PresentMon documenta la pertenencia a **Performance Log Users** y limitaciones sin privilegios administrativos. La app informa los fallos ETW; no cambia grupos, no eleva toda la GUI ni intenta sortear restricciones anticheat. No está certificada para RICOCHET/Vanguard/EAC/BattlEye. El inicio/cierre ETW real y su overhead en juegos necesitan prueba manual Windows.

## Formatos, identidad y límites

CSV separado por comas con `Application`, `ProcessID`, `SwapChainAddress`, `MsBetweenPresents`: PresentMon 1.x y salida predeterminada 2.6.0. No se aceptan exportaciones 2.x sin ese intervalo ni se calcula FPS desde CPU Busy. Se admiten UTF-8/BOM y campos entrecomillados; números invariantes, columnas únicas y filas completas.

Cada aplicación/PID/swapchain forma un stream distinto; si hay varios se requiere elegir. Máximo 64 MiB, 200000 filas y 128 streams. Intervalos cero/NA se omiten y contabilizan para el archivo completo; no son FPS cero. Los intervalos negativos, infinitos o inválidos se rechazan; el intervalo positivo admitido va de 1 ns a 1 hora para impedir desbordamientos numéricos con entradas artificiales. Los outliers válidos permanecen. Se conservan los busy opcionales y su número de muestras; no se sustituye una métrica ausente por cero.

Las sesiones guardadas son inmutables, con ID, SHA-256 del CSV, identidad, contexto declarado y muestras. Los archivos JSON no se ejecutan. Escritura temporal/flush/rename, sin sobreescribir un ID previo. Se verifican esquema, identidad, límites y muestras al cargar y se recalculan resultados; no se confía en un resumen importado. El historial enumera las 200 sesiones más recientes por fecha local de guardado; una sesión corrupta se conserva y su carga falla con explicación. No se envía telemetría.

Ubicación: `%LOCALAPPDATA%\PCOptimizer\Benchmarks` y CSV originales de captura en `Captures`. El CSV importado externo permanece en su ubicación. No hay limpieza automática. JSON exporta contexto, muestras y definiciones; CSV exporta solo muestras del stream, con protección frente a fórmulas de hoja de cálculo en los campos de texto. El CSV de exportación propio no se presenta como CSV original de PresentMon. Una exportación cancelada conserva el archivo destino anterior.

## Definiciones matemáticas

Para los N intervalos de presentación válidos positivos `tᵢ`, en milisegundos:

- **FPS presentados medios:** `1000 N / Σtᵢ`. No es la media aritmética de `1000/tᵢ`.
- **FPS instantáneos medianos:** mediana de la serie `1000/tᵢ`. Para N par puede diferir de `1000 / mediana(t)`.
- **1% low:** `1000 / media(de los ceil(0.01 N) intervalos más lentos)`.
- **0.1% low:** mismo cálculo con `ceil(0.001 N)`. Se advierte cuando N < 1000; una cola con un único intervalo es inestable.
- **Frametime p50, p95, p99:** cuantiles tipo 7, interpolación lineal en la posición `(N−1)p` de la serie ordenada. El inverso de p99 no es nuestro 1% low.
- **Variabilidad:** desviación estándar poblacional de los intervalos capturados.
- **Spike:** intervalo estrictamente mayor que `max(50 ms, 2 × mediana)`. Umbral descriptivo, no diagnóstico causal.
- **Duración observada:** suma de intervalos válidos /1000. No es duración de reloj de la sesión ETW; puede incluir un intervalo cuyo inicio precede al primer evento capturado. Filas NA/cero no contribuyen. La gráfica usa esta suma acumulada, con máximo por columna horizontal para preservar spikes.
- **Distribución:** 20 bins de FPS instantáneos entre cero y el máximo observado. Es conteo por intervalo, no distribución ponderada por tiempo.

Dropped=0/1 o DisplayedTime permiten contar filas observadas como mostradas/no mostradas; si hay datos desconocidos, el conteo completo es N/D. Estas filas **no son una medición de FPS realmente mostrados**. `FrameType` se conserva literalmente y se indica cuántas filas lo exponen; no se inventa un conteo de frames generados a partir de valores desconocidos. Falta normalización por versión/instrumentación y análisis de deltas de display. GPU/CPU Busy son tiempos opcionales; su media usa solo datos informados.

DLSS/FSR Frame Generation, VSync, límite de FPS, varias swapchains, carga de menús y ventanas secundarias afectan la interpretación. Ni FPS presentados ni GPU Busy prueban latencia de entrada. No se infiere CPU/GPU bottleneck automáticamente.

## Comparación y protocolo

Completar antes de guardar: hardware/pantalla, versión de juego, driver, build Windows, ajustes de control (resolución/Hz/VSync/límite/DLSS/FG/Reflex/calidad) y escenario. El contexto es declaración del usuario, no un sensor. Una sesión cargada conserva su contexto; los campos quedan de solo lectura.

1. Calentar juego/shaders y fijar escena/ruta y duración. No borrar cachés entre ensayos.
2. Definir una única variable A/B y sus dos estados en el escenario; mantener idénticos los demás ajustes y el texto del contexto.
3. Capturar A/B alternadamente, 3–5 repeticiones independientes por estado como inicio. Mantener la misma duración; registrar orden, temperatura, versión y actividad externa. La app aún no comprueba automáticamente duración/orden/temperaturas ni realiza una aleatorización.
4. Importar/cargar cada sesión y añadirla a A o B. La misma procedencia SHA + stream no puede reutilizarse como repetición. Esto no demuestra independencia si alguien duplica datos alterando el archivo.
5. Analizar repeticiones. Se comparan medias de FPS por captura; los frames de una sola captura no se tratan como miles de repeticiones.
6. El IC aproximado 95% para la diferencia B−A usa error estándar no pareado y grados de libertad de Welch; t se toma de una tabla conservadora con df menor o igual al calculado. Supone medias independientes y aproximadamente normales. Con pocas repeticiones, sesgo de escena o autocorrelación entre ensayos, no acredita causalidad. Si incluye cero, es inconcluso; si no hay variabilidad observada, no se inventa un IC cero.
7. Revisar también lows, p99, distribución y anomalías; el IC implementado corresponde solo a FPS medios por repetición. Los grupos están en memoria; las sesiones guardadas permanecen. La comparación de dos sesiones individuales solo da diferencias descriptivas sin IC.

El beneficio de un ajuste no se rellena al escribir registro. Falta vincular automáticamente la operación de tweak al conjunto A/B, persistir/exportar el protocolo completo y sincronizar sensores. Medir overhead comparando la misma escena con app cerrada, abierta sin captura y con captura; no se publica un porcentaje de overhead sin realizar ese ensayo.

## Validación y límites de la entrega

Las pruebas ejercitan fórmulas con resultados calculados, aislamiento de streams, CSV inválido/NA, cancelación, sesiones inmutables/corruptas, exportación y comparación entre repeticiones. La herramienta UiPreview añade capturas de la página real a 1400×880 y 1120×740, con datos ilustrativos etiquetados. No ejecuta PresentMon ni cambia Windows.

Pendiente: captura ETW real con juego, cancelación bajo Windows, denegación de permisos, ETW con pérdida de eventos, comparación con salida de referencia, display/generated FPS, sensores sincronizados, Warzone y matriz de anticheats. Compilar y renderizar no valida estos puntos.
