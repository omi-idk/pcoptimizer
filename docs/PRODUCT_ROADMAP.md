# Hoja de ruta — PCOptimizer Ultimate

El [prompt recibido](requirements/PCOptimizer-Ultimate-recibido.md) es el requisito de producto. Su alcance completo aún no está implementado. No se sustituye una función por una tarjeta que diga que existe.

| Prioridad | Entrega verificable | Criterio de aceptación |
| --- | --- | --- |
| P0 · auditoría | ARCHITECTURE_AUDIT y separación de estados | Comparar main/work remotos, preservar cambios y publicar en rama separada. |
| P0 · medición | Módulo de benchmarks, importación PresentMon, captura manual fijada, estadísticas, CSV/JSON, historial, gráfica y comparación exploratoria | Pruebas con capturas conocidas y errores; identidad proceso/PID/swapchain; nunca combinar streams silenciosamente; distinguir presentación de pantalla. Captura Windows requiere validación ETW real. |
| P0 · recuperación | Prueba Windows de fallo parcial, UAC denegado y reinicio | Snapshot previo duradero; no sobrescribir valores externos; recuperación pendiente identificada al inicio. |
| P1 · capacidades / MVVM | Proveedores inyectables, estados tipados, consultas cancelables | Sensores no soportados no provocan fallo global; las ventanas no contienen lógica de métricas o privilegios. Migración incremental de módulos existentes. |
| P1 · sensores | CPU lógica/topología/caché, RAM/módulos/commit, DXGI/VRAM/pantalla, proveedores térmicos opcionales | Cada métrica incluye fuente, instante, unidades y disponibilidad; LHM/NVML/ADLX solo tras evaluar licencia, despliegue y acceso kernel. |
| P1 · protocolo A/B | Contexto obligatorio y 3–5 repeticiones alternadas por configuración | Misma escena, duración, juego, Windows, driver, pantalla y escalado; intervalos basados en repeticiones independientes. Una pareja no prueba causalidad. Overhead de la app medido con/sin captura. |
| P1 · diagnóstico | Correlación sincronizada de telemetría y frametimes | Hipótesis/evidencia/confianza/datos faltantes/acción/riesgo/validación; no porcentaje ficticio de cuello de botella. |
| P1 · perfiles | Conservador, competitivo, equilibrado, streaming, experimental y personalizado | Objetivos separados de preferencias de privacidad; condicionales requieren selección explícita y evidencia; no «intensivo = más FPS». |
| P1 · Warzone | Perfil calibrable experimental del equipo 5800X3D/B550-F/5060 Ti16GB/4×8GB3200 | Detectar hardware real; confirmar versión del juego, DLSS/Reflex/HAGS, drivers y pantalla; protocolo A/B; compatibilidad RICOCHET no presumida. No modificar memoria, archivos protegidos ni tráfico. |
| P2 · sesiones | Detección opt-in de juego, plan temporal y restauración tras salida/fallo | Identidad PID+inicio, exclusiones, diario previo, conflicto externo; no matar procesos ni tocar afinidades/anticheat. |
| P2 · drivers | Instalado / último / recomendado independientes | Datos oficiales por hardware/OS, fecha y razones; desconocido explícito. No inferir recomendación por fecha ni scraping no fiable. |
| P2 · red | Adaptador, negociación, errores, DNS y carga controlada opcional | ICMP/DNS/juego/render separados; consentimiento para tráfico de carga; sin TCP/offloads universales. |
| P2 · ajustes | Contratos de evidencia y compatibilidad más ricos; mantenimiento selectivo | Mantener lista cerrada y recuperación; no Defender/Firewall/TPM/Secure Boot/BCD/voltajes automáticos. Candidatos no justificados quedan por evaluar. |
| P3 · entrega estable | Instalador firmado, actualización autenticada, accesibilidad, localización y pruebas largas | Procedencia verificable, migraciones de datos, matriz hardware/Windows, presupuestos de consumo y soporte. |

## Restricciones que permanecen

No hay garantía de FPS, cero riesgo, EQ universal, lifespan exacta de SSD o certificación anticheat. No vaciar standby ni borrar shaders como optimización general. Ningún dato ilustrativo debe aparecer como medición del usuario.

## Seguimiento de esta entrega

La auditoría y la investigación preceden al código. Las comprobaciones de esta rama y sus límites se registran en `BENCHMARKING.md`; la validación previa de 96 pruebas no se presenta como resultado de funcionalidades nuevas.

En esta rama se implementó la primera entrega de medición: módulo separado, captura manual PresentMon con pin y hash, importación, cálculos, historial, exportación, gráficas y comparación descriptiva / entre repeticiones. La captura ETW y el cierre del auxiliar aún requieren prueba real Windows. Continúan pendientes el diagnóstico sincronizado, Game Booster, sensores completos, perfil Warzone calibrado y resto de la tabla. No se declaran implementados por estar descritos aquí.

## Avance Preview 0.5

- Implementados: inventario CPU/módulos/vídeo ampliado, proveedor NVML opcional, capturas cancelables/exportables de CPU lógica/memoria y reglas de presión con evidencia, sesiones de energía temporales con detección voluntaria de ruta y salida, diario recuperable, protección frente a conflictos y aviso al reiniciar, consulta de adaptadores/DNS y ofertas WUA de drivers.
- Perfil del equipo solicitado: evaluación por componente y base Warzone revisable. No está calibrado; ver [perfil de tu PC](PERFIL_PC_WARZONE.md).
- Parciales: diagnóstico de recursos sin correlación automática con frametimes; Game Booster limitado a energía; drivers ofrecidos por Windows Update sin recomendación de fabricante; sensores térmicos CPU y GPU AMD/Intel ausentes.
- Pendientes: correlación sincronizada, display/generated FPS, protocolo completo persistente, medición del consumo de la app, bufferbloat bajo carga controlada, recomendaciones de drivers verificadas, validación Windows física y matriz de juegos/anticheats. Se conservan los criterios de aceptación originales; este avance no los declara completados por estar documentados.

[Funcionamiento, fuentes y pruebas](RESOURCE_AND_GAMING_SESSIONS.md).

## Plan comercial actualizado y avance 0.6

El [nuevo prompt comercial completo](requirements/PCOptimizer-Ultimate-comercial-recibido.md) amplía el requisito anterior. Se preserva la base integrada Preview 0.5; este incremento no reconstruye la aplicación ni completa todos los hitos.

| Hito | Estado de esta entrega / criterio restante |
| --- | --- |
| 0.6 Performance Intelligence | **Primer incremento funcional:** QPC en PresentMon y sensores optativos CPU global/memoria/NVML; sesión unificada y persistida, análisis de coincidencias con cobertura, gráfica temporal común, informe y cancelación. Pendiente ampliar sensores, alinear eventos Present, capturar contexto automáticamente y validar física/overhead. |
| 0.7 Optimization Intelligence | Pendiente: recomendaciones por capacidades, contratos enriquecidos, objetivos adaptativos y flujo revisión→un cambio→A/B→conservar/revertir con experimento persistente. Se conservan los 41 ejecutores existentes. |
| 0.8 Universal Gaming Optimizer | Parcial por las sesiones de energía/perfil experimental existentes; faltan Booster ampliado, familias de hardware, proveedores adicionales y mejor diagnóstico de procesos/drivers/pantalla/red. |
| 0.9 Product Hardening | Parcial: controles de seguridad/recuperación y CI existentes. Faltan MVVM/DI restante, integración Windows con UAC/reinicio/fallos, matriz física, accesibilidad y presupuesto de overhead. |
| 1.0 Commercial Readiness | No alcanzado: firma, instalador, actualización autenticada, licencias de distribución verificadas y validación multi-equipo siguen pendientes. Sin DRM, pagos o cuentas en esta fase. |

Criterios 0.6: modelo/contexto, correlación con QPC suficiente, ausencias explícitas, anomalías documentadas, hallazgos, persistencia/reapertura, exportación, gráfica, cancelación y mediciones sin cambios están implementados en este incremento. CI valida código y flujo con fixtures; no convierte una simulación en evidencia de mejora. Los resultados verificables y el procedimiento Windows están en [Performance Intelligence](PERFORMANCE_INTELLIGENCE.md). No se declara completa una release comercial por compilar.
