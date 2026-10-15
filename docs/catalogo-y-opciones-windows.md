# Catálogo ampliado y opciones de Windows

Revisión: 15 de octubre de 2026. Alcance acordado: incluir las aplicaciones y ajustes de las capturas; mantener los ajustes sensibles visibles con evaluación pendiente. Esta revisión documenta el catálogo original. La Preview 0.2 añade ajustes ejecutables independientes: consulta [el estado actual](ajustes-aplicables.md). Los candidatos restantes no se presentan como mejoras comprobadas.

## Cobertura del software

Se adaptaron los 236 registros de `config/applications.json` de WinUtil en el commit `07ccd8e2e755a706f31569808b31f5b77acad6a9`. Categorías: Navegadores (15), Comunicación (18), Desarrollo (41), Documentos (17), Juegos (17), Windows y sistema (18), Multimedia (22), Diagnóstico y redes (19), Servidores y streaming (12), Utilidades (57).

Las fichas usan los nombres del software, no incluyen marcas de compatibilidad anticheat y no recomiendan ejecutar sus funciones. Herramientas de drivers, particiones, overclock, automatización, red o acceso remoto tienen notas de evaluación específicas. Instalar una herramienta y ejecutar sus operaciones son acciones distintas.

El catálogo es un recurso integrado: no descarga ni ejecuta scripts de WinUtil al iniciar. Conserva las descripciones originales en inglés y añade categorías y notas en español. La licencia y atribución están en `licenses/WinUtil-MIT.txt` y `THIRD-PARTY-NOTICES.md`, copiadas también a la salida de compilación/publicación.

## Verificación de las fuentes

Se consultó por HTTPS el [índice público de WinGet](https://cdn.winget.microsoft.com/cache/source2.msix) y se leyó su `Public/index.db` para cotejar identificadores. No se instalaron ni ejecutaron contenidos de ese archivo. Los 234 identificadores de fuente `winget` estaban presentes. La evidencia, hash del archivo y versiones observadas están en [catalogo-verificacion.json](catalogo-verificacion.json). Es una instantánea, no una consulta de la última versión en vivo ni una recomendación de actualización.

Los otros dos paquetes usan la fuente `msstore`, sin pasar el prefijo `msstore:` como parte del identificador a WinGet:

| Aplicación de la captura | ID | Ficha consultada |
| --- | --- | --- |
| ChatGPT Desktop | `9NT1R1C2HH7J` | Microsoft Store lo denomina **ChatGPT Classic**; el catálogo público identifica a **OpenAI** como editor y lo describe como la aplicación oficial de Windows. Se informa de la diferencia de nombre antes de instalar. |
| WhatsApp Desktop | `9NKSQGP7F2NH` | Microsoft Store: **WhatsApp**, editor **WhatsApp Inc.** |

Se consultaron las páginas `https://apps.microsoft.com/detail/<ID>` y los metadatos públicos de producto de Microsoft en `displaycatalog.mp.microsoft.com`. La comprobación de identidad no sustituye revisar los términos, requisitos, arquitectura, región o disponibilidad en el equipo de destino.

Cada tarjeta ofrece un acceso al sitio oficial. Abrirlo no se registra como una descarga o instalación completada. Las rutas de instalación siempre usan el identificador exacto y fuente permitida, sin aceptar acuerdos por el usuario, omitir hashes ni actualizar versiones existentes automáticamente.

## Selección y revisión

El buscador ignora mayúsculas y acentos; permite combinar palabras de nombre, categoría, descripción o identificador. Cambiar de filtro conserva las selecciones en sus objetos originales. El contador y la revisión incluyen también las aplicaciones seleccionadas que no son visibles en el filtro actual. «Quitar toda la selección» afecta a todo el catálogo.

La revisión del lote es una ventana desplazable para que una selección grande no quede cortada en un cuadro de diálogo. Muestra nombres, fuente, identificador, sitio, descripción y condiciones. Cancelar no lanza instaladores. La ejecución es secuencial y conserva el tratamiento de errores, omisiones y reinicios documentado en [instalación y limpieza](optimizacion-y-software.md).

## Ajustes de la referencia

Se incluyen 67 controles de `config/tweaks.json` y cuatro adicionales: tamaño de barra, timers/HPET, HAGS y capturas. Todos indican **Requiere evaluación · Sin ejecutor conectado**. Sus fichas no consultan el estado actual: los valores que se pueden expandir son objetivos del código de referencia. No se incluyen cuerpos de scripts remotos ni se invoca PowerShell desde las fichas.

BitLocker, IPv6, Teredo, servicios, cambios de seguridad y otros ajustes sensibles permanecen visibles porque el usuario pidió mantenerlos para evaluación. Se explican sus riesgos. Incluirlos no los selecciona para un perfil automático. La futura aprobación requiere compatibilidad, necesidad concreta, recuperación y evidencia de beneficio. Microsoft [desaconseja desactivar IPv6](https://learn.microsoft.com/en-us/troubleshoot/windows-server/networking/configure-ipv6-in-windows); esa advertencia se conserva en su ficha.

## Barra de tareas de Windows 11

Se añadió Personalización con acceso documentado `ms-settings:taskbar`. El botón solicita abrir Configuración; **no reduce la barra ni comprueba que haya una opción de iconos pequeños**.

Hay que distinguir tamaño de iconos/botones, altura de toda la barra y escala general de la pantalla. El artículo [Personalizar la barra de tareas](https://support.microsoft.com/en-us/windows/customize-the-taskbar-in-windows-0657a50f-0cc7-dbfd-ae6b-05020b195b07) contiene apartados distintos para Windows 11 y 10: sus instrucciones de arrastrar la altura o usar botones pequeños no deben atribuirse automáticamente a cualquier Windows 11.

Falta comprobar opciones por compilación y los límites del mecanismo nativo. No se usa `TaskbarSi` como solución universal ni se modifica Explorer. StartAllBack y otros programas del catálogo son herramientas externas con licencia, compatibilidad y riesgos propios; no se instalan ni configuran automáticamente para lograr ese resultado.

## Windows Update

La sección muestra tres conceptos: aplazamientos de la referencia, recuperación del estado previo y desactivación avanzada. Sus políticas **no están implementadas ni se aplican**. Están disponibles los accesos nativos documentados a Windows Update, horas activas, opciones avanzadas e historial.

Se revisaron las funciones [`Invoke-WPFUpdatessecurity`](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/functions/public/Invoke-WPFUpdatessecurity.ps1), [`Invoke-WPFUpdatesdefault`](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/functions/public/Invoke-WPFUpdatesdefault.ps1) y [`Invoke-WPFUpdatesdisable`](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/functions/public/Invoke-WPFUpdatesdisable.ps1) de la misma instantánea. No se ejecutaron.

- Su perfil recomendado aplaza funciones 365 días y calidad 4 días, excluye drivers y configura notificación después de descargar. También modifica servicios, tareas y otras políticas: no se adoptará como una sola operación sin inventario completo y recuperación.
- `AUOptions=3` controla descarga/notificación; no garantiza por sí solo ausencia de reinicios durante una partida.
- El perfil de desactivación también detiene servicios/tareas y elimina descargas de SoftwareDistribution. No se convierte en una optimización predeterminada de gaming; puede impedir correcciones de seguridad.
- «Valores predeterminados» y «valores anteriores» no significan lo mismo. Antes de una futura restauración debemos guardar y comprobar lo que PCOptimizer modificó, conservando políticas ajenas.

Las [políticas Update de Microsoft](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-update) documentan valores y matrices de aplicabilidad; hay que revisarlas para edición/build, administración MDM/GPO y soporte vigente de la versión. No basta escribir una clave para acreditar su efecto.

Los destinos nativos están tomados de [Launch the Windows Settings app](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings-app). Se resuelven mediante una lista cerrada de nombres; las fichas no pueden aportar un comando arbitrario.

## Validación y límites

Se ejecutan compilación y pruebas del núcleo en Linux. La comprobación de argumentos, búsquedas, catálogos e historial no prueba instalación, disposición gráfica, páginas nativas o compatibilidad anticheat en Windows. Esas pruebas manuales siguen pendientes en [README](../README.md). No se cambió Windows, no se aplicaron perfiles y no se instalaron las aplicaciones del catálogo en este entorno.
