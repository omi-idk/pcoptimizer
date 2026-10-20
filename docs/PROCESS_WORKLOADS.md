# Procesos y objetivos gaming · Preview 0.6 incremento 2

Abre **Gaming / Uso de recursos → Procesos y objetivos gaming → Medir procesos**. Se toman dos instantáneas separadas por unos dos segundos; consultar muchos procesos añade tiempo. El límite total es 45 segundos, sin sondeo residente después. Puedes cancelar; se conserva el resultado anterior. Cerrar la ventana durante una operación solicita cancelación y espera a que termine antes de permitir el cierre.

Elige voluntariamente el juego por PID/inicio, el objetivo y si necesitas voz/clips. Selecciona una fila para ver cálculo, límites y recomendación. Un nombre de ejecutable es una etiqueta, no verificación de fabricante ni autorización para terminarlo. Por diseño no hay acciones para cerrar/matar procesos, cambiar afinidad/prioridad, leer memoria privada ni desactivar seguridad.

## Qué se mide

Fuente: APIs oficiales `GetProcessTimes` y `GetProcessIoCounters`, mismo handle con `PROCESS_QUERY_LIMITED_INFORMATION`, tiempo QPC por consulta. PID + creación evita unir un proceso nuevo con un PID reciclado. Se incluye al propio colector cuando sus metadatos son accesibles. No se enumeran módulos ni se activa SeDebugPrivilege. No se instala un driver ni se necesita UAC.

CPU capacidad = 100 × Δtiempo usuario/kernel / Δtiempo QPC / procesadores disponibles para PCOptimizer. **CPU (1 lógico=100%)** omite ese divisor y puede superar 100%; no es una lectura por núcleo físico ni por hilo del juego. `Environment.ProcessorCount` puede reflejar afinidad/límites de CPU del colector y diferir de toda la máquina: hay que evaluar equipos con grupos >64 lógicos o restricciones diferentes. Normalización >105% se declara incompatible/N/D, sin recortarla.

E/S lectura/escritura = Δbytes de cada contador / Δtiempo. Puede incluir archivos, red y dispositivos; no se presenta como velocidad de disco ni tráfico del juego. No se muestran otras operaciones E/S, GPU por proceso o memoria de trabajo en este incremento.

N/D significa observación faltante, proceso nuevo/terminado/no legible/PID reutilizado, reinicio de contador o intervalo insuficiente (<250 ms). No significa cero. Los contadores no legibles/omitidos se cuentan por instantánea, incluyendo el límite de 4096 procesos; la cobertura no representa todo el sistema. No hay diagnóstico causal de stuttering basado en dos lecturas.

Las sugerencias de revisión se limitan a etiquetas conocidas de navegador/grabación/comunicación con ≥5% de capacidad observada. Desconocidos, Windows, drivers, seguridad y anticheats reciben solo observación. El juego seleccionado y colector se identifican por PID+creación; Streaming conserva grabadores y «Conservar chat de voz» conserva comunicación. No se atribuye mejora o interferencia al nombre o umbral: se propone una prueba A/B voluntaria.

## Guardar, reabrir y aplicar ajustes

**Guardar captura** exporta JSON con contadores, cobertura, objetivo y contexto. **Abrir captura** lo valida y reabre sin consultar los procesos actuales. **Exportar informe** produce JSON legible con evidencia/unidades/límites; no es el formato de captura reabrible. Nombres, PIDs e inicios pueden ser datos privados: revisa antes de compartir. No se guardan rutas completas ni se envían datos. Límite de archivo 8 MiB; escrituras mediante temporal/flush/move. Un guardado que ya alcanzó su fase de escritura termina atómicamente; la cancelación no revierte un archivo completo ya confirmado.

**Revisar ajustes del objetivo** cierra este panel y selecciona IDs ejecutables reales en Ajustes de Windows. No aplica todavía. Equilibrado/Streaming/Laptop proponen Modo Juego; Competitivo añade desactivar Game Bar solo si declaras no usar sus clips. Streaming conserva captura incluso si desmarcas clips. Los ajustes seleccionados entran al flujo existente: revisión exacta, intento de punto Windows, snapshots/journal, aplicación y recuperación con conflictos. No se añaden cambios de seguridad, HAGS, FPS caps, energía OEM, scheduler o configuración del juego a estos objetivos.

Esta captura es una observación independiente, todavía no una serie sincronizada automáticamente con PresentMon ni un experimento A/B persistido con intervenciones.

## Validación Windows pendiente / cómo probar

1. Ejecuta el portable sin elevar. Mide procesos y confirma que el propio PCOptimizer aparece con CPU comparable en dos lecturas. Compara tendencias con Administrador de tareas, considerando distintas ventanas/escalas: no se espera igualdad exacta.
2. Mantén tu juego abierto y elígelo por PID/inicio. Las recomendaciones deben conservarlo; ninguna acción escribe en él.
3. Elige Streaming con OBS abierto y voz marcada; revisa que ambos se conserven. Competitivo sin clips debe seleccionar únicamente Modo Juego y captura Game Bar para revisión.
4. Guarda, cambia contexto y reabre; deben recuperarse objetivo, selección y datos originales. Prueba cancelar otra captura y verifica el resultado anterior.
5. Valida cambios solo en el flujo existente y primero en un equipo/VM de prueba. No se probaron modificaciones reales de registro/UAC ni juegos/anticheats durante esta entrega. Deben medirse overhead, grupos de procesadores, laptop/OEM y compatibilidad legítima en hardware físico.

CI Windows consulta contadores reales de su propio proceso, cancela un sondeo y comprueba persistencia/contexto e importación malformada con datos sintéticos; renderiza esta ventana a dos tamaños. Las fixtures no son resultados de Warzone ni del PC del propietario.

Fuentes: [GetProcessTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes), [GetProcessIoCounters](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getprocessiocounters), [derechos de procesos](https://learn.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights), [CPU disponible](https://learn.microsoft.com/en-us/dotnet/api/system.environment.processorcount), [revisión del referente](research/WZCONTROL_REVIEW.md).
