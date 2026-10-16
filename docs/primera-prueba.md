# Primera prueba en Windows

Esta es una versión preliminar para Windows 11 x64. La publicación incluye .NET: no necesitas instalar el SDK para abrirla. La compilación desde Linux no valida su funcionamiento real en Windows.

## Abrir la versión portable

1. Descarga `PCOptimizer-win-x64-preview.zip` y extrae **todo** su contenido a una carpeta. No ejecutes el programa desde dentro del ZIP ni copies solamente el EXE.
2. Abre `PCOptimizer.App.exe` como usuario normal, sin elegir «Ejecutar como administrador». La aplicación solicita UAC para las operaciones que lo requieren.
3. En Inicio, compara procesador, gráfica, memoria y unidades con las herramientas de Windows. Revisa las categorías de Instalación rápida y las fichas de Optimización. Buscar, seleccionar aplicaciones y leer fichas no inicia una instalación.
4. Abre Ajustes de Windows, pulsa Analizar y selecciona un perfil. Revisa las casillas y pulsa Revisar y aplicar: la confirmación muestra motivo, riesgos y valores anteriores/solicitados. Cancela primero para comprobar que no escribe. Los candidatos «Requiere evaluación» del catálogo separado siguen siendo informativos.

El ejecutable preliminar no tiene firma de editor. Si Windows o las políticas de tu equipo bloquean la ejecución, conserva sus protecciones y registra el mensaje. No desactives Defender, SmartScreen ni el anticheat para probarlo.

## Probar cambios y recuperación

Haz estas pruebas en una máquina de prueba o una máquina virtual Windows con una instantánea previa y una copia independiente de tus datos importantes.

1. Abre Respaldo y recuperación y accede a Protección del sistema. Comprueba en Windows que la protección esté disponible y configurada.
2. Solicita un punto manual y verifica que exista en las herramientas de Windows. Abrir la protección no la activa automáticamente. Windows puede impedir puntos frecuentes; si no se confirma un punto, cancela la continuación para esta prueba.
3. En Energía, anota el plan activo, cambia a otro existente y verifica el resultado en las opciones de energía de Windows. Después recupera el anterior y comprueba ambos eventos en el historial.
4. Aplica un ajuste de usuario de prueba y comprueba el registro y su efecto después de cerrar sesión si se indica. En Recuperación → Revertir ajustes, selecciona la operación y revisa la inversa. Compara tipo, datos y ausencia originales.
5. Prueba HAGS/MPO únicamente en hardware compatible, con reinicio manual y un problema reproducible; no entran en perfiles automáticos.
6. Deja la instalación de programas y la eliminación de apps para pruebas posteriores. La desinstalación puede eliminar datos de la aplicación; un punto de restauración no es una copia de esos datos.

La interfaz, UAC, WinGet, los puntos de restauración y la compatibilidad con anticheats siguen pendientes de validación real en Windows. La carga actual no es una captura de FPS ni un diagnóstico de cuello de botella. Drivers muestra versiones instaladas y fuentes oficiales, no una comparación automática de último/recomendado. Salud de discos consulta contadores cuando el controlador los expone, no predice vida restante.

## Informar un problema

Anota la versión de Windows (`winver`), la sección y los pasos, qué esperabas y el mensaje exacto. Comparte una captura si ayuda, ocultando información personal. Los registros locales están en `%LOCALAPPDATA%\PCOptimizer`; revísalos antes de compartirlos y conserva los originales si necesitas recuperación.

## Ajustes heredados de Preview 0.3

Comprueba el título Preview 0.4 para evitar ejecutar una copia antigua. En Ajustes usa Ctrl+F, área y estado; el panel lateral muestra detalles de la fila seleccionada. Los perfiles Mínimo/Recomendado/Gaming/Intensivo seleccionan 4/9/7/16 candidatos y excluyen los no disponibles. Prueba Guardar/Cargar perfil: no debe aplicar al cargar. Comprueba que seleccionar HAGS desactivado desmarca HAGS activado. En Recuperación, elige una operación y revisa la inversa; el catálogo de candidatos por evaluar está en una pestaña separada.

## Benchmarks (Preview 0.4)

Abre **Benchmarks** en Diagnóstico. Puedes comenzar importando un CSV PresentMon que contenga `MsBetweenPresents` y elegir el proceso/PID/swapchain. Para captura manual, descarga el console oficial desde el botón de la app, selecciónalo para verificar su hash, actualiza procesos, selecciona el juego y pulsa Iniciar captura. Si ETW no tiene permisos se mostrará el error sin cambiar grupos ni elevar la GUI. Detener cierra solo la sesión propia. Completa el contexto, guarda y exporta. Para A/B usa al menos tres capturas independientes por estado; no prometemos mejora ni certificación anticheat. Consulta [las fórmulas y límites](BENCHMARKING.md).
