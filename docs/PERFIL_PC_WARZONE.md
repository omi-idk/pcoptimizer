# Perfil de tu PC — Warzone experimental

Objetivo solicitado: Ryzen 7 5800X3D, ASUS ROG Strix B550-F Gaming WiFi II, RTX 5060 Ti 16 GB, 32 GB DDR4 3200 (4×8 GB), Windows 11 x64, DeepCool AK500 Digital y GameSir G7 Pro.

## Uso real

Abre **Uso de recursos → Evaluar mi PC · Warzone**. Se leen CPU/placa/GPU/módulos y sistema y se muestra **Coincide / Difiere / Por comprobar** por componente. La VRAM solo se confirma si un único dispositivo NVML coincidente expone su total; no se deducen 16 GB del nombre ni se usa AdapterRAM de 32 bits. La clase de 16 GiB admite una pequeña reserva informada por el driver. Refrigeración y mando requieren confirmación del usuario. Que el hardware coincida no demuestra estabilidad ni valida un driver.

**Seleccionar base Warzone** carga una selección revisable de Modo Juego; no aplica nada al cargar. Pasa por el mismo motor, revisión, respaldo y reversión que los otros ajustes. No desactiva captura de OBS/Game Bar ni cambia ratón para un jugador que usa mando. HAGS/MPO, afinidades, temporizadores y energía no se incluyen automáticamente. La sesión temporal de energía se elige aparte, entre planes presentes en el equipo, y exige consentimiento.

## Calibración necesaria en tu equipo

1. Registrar versión concreta de Warzone, Windows, driver NVIDIA y chipset AMD desde fuentes oficiales. Verificar BIOS/AGESA aplicable con ASUS sin flashear desde esta app. No trasladar ajustes de topologías X3D con varios CCD al 5800X3D de un solo CCD.
2. Confirmar los cuatro módulos y su velocidad efectiva/estabilidad; el valor SMBIOS no demuestra DOCP, canales ni ausencia de errores. Cuatro módulos pueden exigir más al controlador de memoria. No se activa DOCP ni se escriben voltajes automáticamente.
3. Registrar resolución, Hz, VSync, límite de FPS y opciones disponibles de DLSS, Frame Generation y Reflex en la versión actual. FPS generados y Hz de pantalla no son FPS presentados ni latencia de entrada. No se asigna una recomendación universal de Reflex/Boost/FG sin medir.
4. Establecer ruta/escena repetible y calentamiento; capturar 3–5 repeticiones alternadas por configuración, cambiando una variable. Guardar el contexto y revisar promedio, lows, p99, spikes y variación. Comparar también con la aplicación cerrada para medir su consumo de recursos.
5. Observar temperaturas/frecuencias CPU con una herramienta compatible y GPU/VRAM mediante el proveedor disponible. Los contadores de CPU de esta entrega son globales por procesador lógico, no pruebas del hilo crítico de Warzone.
6. Probar HAGS por separado solo si Windows/driver lo ofrecen y con reinicios comparables; no seleccionarlo porque el equipo coincide. No editar configuraciones heredadas de versiones antiguas del juego ni archivos protegidos.
7. GameSir: usar opciones y firmware oficiales del modelo exacto. No instalar filtros de polling, macros o auto-clickers para optimizar el mando. Loudness EQ es opcional según el dispositivo; su compresión puede alterar percepción de distancias y no mejora FPS.
8. RICOCHET: compatibilidad pendiente de validación real. No se modifica memoria, tráfico, archivos protegidos ni componentes del anticheat. No se promete riesgo cero ni certificación.

El perfil ya puede evaluar hardware y cargar su base, pero **no está calibrado**: no tenemos capturas de Warzone de este PC ni acceso físico a sus sensores. No se inventan resultados ni un driver recomendado. Los benchmarks y reportes exportados permitirán esa calibración posterior.
