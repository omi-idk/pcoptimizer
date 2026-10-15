# Primera prueba en Windows

Esta es una versión preliminar para Windows 11 x64. La publicación incluye .NET: no necesitas instalar el SDK para abrirla. La compilación desde Linux no valida su funcionamiento real en Windows.

## Abrir la versión portable

1. Descarga `PCOptimizer-win-x64-preview.zip` y extrae **todo** su contenido a una carpeta. No ejecutes el programa desde dentro del ZIP ni copies solamente el EXE.
2. Abre `PCOptimizer.App.exe` como usuario normal, sin elegir «Ejecutar como administrador». La aplicación solicita UAC para las operaciones que lo requieren.
3. En Inicio, compara procesador, gráfica, memoria y unidades con las herramientas de Windows. Revisa las categorías de Instalación rápida y las fichas de Optimización. Buscar, seleccionar aplicaciones y leer fichas no inicia una instalación.
4. Para esta primera revisión, cancela las confirmaciones de instalación, limpieza o cambio de energía. Los ajustes marcados «Requiere evaluación» son informativos; no se aplican desde estas fichas.

El ejecutable preliminar no tiene firma de editor. Si Windows o las políticas de tu equipo bloquean la ejecución, conserva sus protecciones y registra el mensaje. No desactives Defender, SmartScreen ni el anticheat para probarlo.

## Probar cambios y recuperación

Haz estas pruebas en una máquina de prueba o una máquina virtual Windows con una instantánea previa y una copia independiente de tus datos importantes.

1. Abre Respaldo y recuperación y accede a Protección del sistema. Comprueba en Windows que la protección esté disponible y configurada.
2. Solicita un punto manual y verifica que exista en las herramientas de Windows. Abrir la protección no la activa automáticamente. Windows puede impedir puntos frecuentes; si no se confirma un punto, cancela la continuación para esta prueba.
3. En Energía, anota el plan activo, cambia a otro existente y verifica el resultado en las opciones de energía de Windows. Después recupera el anterior y comprueba ambos eventos en el historial.
4. Deja la instalación de programas y la eliminación de apps para pruebas posteriores. La desinstalación puede eliminar datos de la aplicación; un punto de restauración no es una copia de esos datos.

La interfaz, UAC, WinGet, los puntos de restauración y la compatibilidad con anticheats siguen pendientes de validación real en Windows. No hay todavía medición de FPS, diagnóstico de cuello de botella, recomendaciones de drivers ni salud SMART. No se debe interpretar el inventario como esas funciones.

## Informar un problema

Anota la versión de Windows (`winver`), la sección y los pasos, qué esperabas y el mensaje exacto. Comparte una captura si ayuda, ocultando información personal. Los registros locales están en `%LOCALAPPDATA%\PCOptimizer`; revísalos antes de compartirlos y conserva los originales si necesitas recuperación.
