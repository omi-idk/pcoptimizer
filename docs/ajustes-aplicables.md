# Ajustes aplicables: Preview 0.3

La pantalla Ajustes de Windows y la sección Gaming comparten un motor de ejecución. Sus controles escriben preferencias y políticas reales del registro. El catálogo original es una referencia separada: los candidatos restantes no se anuncian como ejecutables.

## Perfiles

| Perfil | Selección exacta |
|---|---|
| Mínimo | Modo Juego, extensiones visibles, consejos de Windows y sugerencias de Configuración (4) |
| Recomendado | Mínimo, identificador publicitario, experiencias personalizadas, instalaciones sugeridas, recomendaciones de Inicio y consejos de bloqueo (9) |
| Gaming | Modo Juego, capturas, aceleración del puntero, tres atajos de accesibilidad y reapertura de apps al iniciar sesión (7) |
| Intensivo | Gaming, transparencias, minimizar ventanas, animaciones de barra, Aero Peek y cinco preferencias de sugerencias (16) |

Seleccionar un perfil **no aplica cambios**. Puedes modificar la selección, leer los riesgos y revisar el lote completo antes de confirmar. «Recomendado» es una selección general y conservadora, no un resultado de benchmarks de tu PC. «Intensivo» describe más preferencias modificadas; no implica overclock, modificación de voltajes ni desactivación de seguridad.

## Ajustes individuales

| Ajuste | Alcance y activación |
|---|---|
| Modo Juego | HKCU; reabrir juego y comprobar Configuración |
| Desactivar captura de Game Bar | HKCU; cierra Game Bar y vuelve a iniciar sesión; no desactiva OBS u otros grabadores |
| Desactivar aceleración del puntero | HKCU; próxima sesión; juegos con Raw Input suelen ignorarlo |
| Desactivar transparencias | HKCU; puede requerir próxima sesión |
| Desactivar animación al minimizar | HKCU; próxima sesión; no desactiva todas las animaciones |
| Mostrar extensiones | HKCU; puede requerir reabrir Explorador/próxima sesión |
| Mostrar archivos ocultos | HKCU; conserva protegidos los archivos de sistema |
| Alinear barra a la izquierda | HKCU; Windows 11 compatible; no cambia altura |
| Ocultar botón Vista de tareas | HKCU; Windows + Tab sigue disponible |
| Tema oscuro | HKCU; reabrir apps compatibles |
| Solicitar HAGS | HKLM; UAC y reinicio; comprueba soporte de GPU/controlador antes de elegir |
| Desactivar MPO | HKLM; UAC y reinicio; workaround para problemas específicos, no mejora general |
| Aplazar funciones de Windows 30 días | HKLM; UAC, edición compatible y reinicio; no cambia aplazamiento de parches de calidad |
| Excluir drivers de actualizaciones de calidad | HKLM; UAC, edición compatible y reinicio; revisar manualmente drivers y correcciones |

HAGS (activar/desactivar), MPO y las políticas no entran en los perfiles automáticos. Las opciones HAGS se excluyen mutuamente tanto en la selección como en el motor. Los perfiles excluyen ajustes no compatibles y muestran cuántos se omitieron; no se modifica Windows al seleccionarlos. Las políticas requieren Pro, Enterprise o Education y se bloquean ante inscripción MDM/unión organizativa detectada; el proceso elevado comprueba además pertenencia a dominio. La detección no certifica ausencia de toda política local o administración de terceros: no uses estos controles para sustituir decisiones de una organización.

Cada ficha muestra motivo, riesgo, origen, alcance, rutas y valores solicitados. **La lectura posterior confirma los valores del registro, no que Windows haya aceptado o activado la función ni que mejoren los FPS.** Deben comprobarse el interruptor/efecto y reinicios indicados en cada ficha. No se reinicia Explorer ni Windows automáticamente.

## Respaldo y reversión

Antes de escribir se guarda una operación por GUID en `%LOCALAPPDATA%\PCOptimizer\Tweaks`. Para cada valor conserva identidad del ajuste, tipo, datos y existencia previa. Si no existía, revertir borra solo ese valor y deja la clave y sus vecinos intactos. Se mantiene además la asociación al registro/punto de Windows en `Recovery`.

El intento de crear punto está activado por defecto; si falla, continuar requiere confirmación expresa. El respaldo de valores existe aunque el punto no esté disponible. No es una imagen del sistema ni una copia de archivos personales.

El motor comprueba el lote antes de empezar, vuelve a comprobar cada valor antes de escribir y verifica la lectura posterior. El proceso elevado vuelve a comprobar el valor tras UAC. Esto no es una transacción del registro ni vigilancia continua: otro programa puede escribir después. No edites los mismos ajustes simultáneamente desde otras utilidades.

Una interrupción puede dejar un lote parcial; no se vuelve a ejecutar automáticamente ni se afirma que todo falló. En **Revertir ajustes aplicados**, se reconstruye la operación inversa de los valores que aún coinciden con lo solicitado. Si encuentra un valor distinto del original y del solicitado, detiene la reversión antes de escribir. Una operación cancelada antes de escribir no puede recuperar cambios ajenos hechos después.

Los destinos no se cargan de JSON: pertenecen a una lista compilada cerrada. Los respaldos corruptos, tipos no compatibles, valores fuera del catálogo y referencias de reversión no válidas bloquean la operación y se conservan para revisión. El proceso elevado solo escribe DWORD en los destinos HKLM permitidos; nunca importa archivos `.reg`, recibe scripts del usuario ni escribe archivos de resultado con privilegios.

## Pruebas manuales necesarias

En una VM Windows preparada con instantánea, guarda primero los valores existentes y comprueba cada ajuste y su reversión, incluidos valores ausentes y tipos originales. Prueba cancelación de UAC, punto no disponible, cambio externo durante la revisión, interrupción tras un valor y cierre/reapertura. Contrasta las preferencias tras cerrar sesión o reiniciar. Para HAGS/MPO, usa también hardware real compatible y un escenario reproducible.

Linux compila WPF y ejecuta pruebas de lógica con un almacén simulado. **No prueba escrituras reales del registro, UAC, políticas efectivas, presentación visual ni recuperación de Windows.**

## Ampliación de catálogo y selección

El catálogo ejecutable actual tiene 41 ajustes. Además de los originales incluye: atajos de Teclas especiales/filtro/alternancia; limitación por política de apps Store en segundo plano; Delivery Optimization sin pares; rutas largas; ID publicitario; experiencias personalizadas; consejos y sugerencias de Windows/Configuración/Inicio/bloqueo; instalaciones sugeridas; historial de búsqueda; historial de portapapeles; animaciones de barra, Aero Peek y gesto de agitar ventanas; vista compacta y ubicación inicial del Explorador; ruta en el título; búsqueda en barra; Finalizar tarea; segundos del reloj; Bloq Num y reapertura de apps.

Los tres ajustes de accesibilidad eliminan únicamente HOTKEYACTIVE (bit 0x4), conservando el resto, incluido el bit que indica si la función está activada. Bloq Num establece su bit sin eliminar otras banderas. Si no existe un valor persistido válido, estos controles quedan no disponibles: no se inventan valores por defecto.

Las preferencias no son todas mejoras de gaming. Cada ficha indica finalidad real y límites: reducir sugerencias es privacidad; mostrar segundos incluso puede aumentar consumo. Los mapeos de preferencias del escritorio se contrastaron con las instantáneas de Sophia Script/Win11Debloat de la revisión; CSP de privacidad, Delivery Optimization, rutas largas y flags se consultaron en documentación Microsoft. Su presencia en una referencia no constituye un benchmark ni garantiza el efecto en cada build. Las fuentes se abren desde la ficha; no se ejecutan scripts externos.

Los filtros no eliminan selecciones ocultas. El contador y la revisión final incluyen todo el catálogo. Seleccionar estándar visibles excluye avanzados, y la aplicación verifica de nuevo valores y compatibilidad antes de escribir. La vista compacta no renderiza todos los detalles en cada fila: el panel lateral mantiene propósito, riesgos, activación, valores y fuente.

## UAC por lote

Los valores HKLM se preparan como un lote. Se guardan **todos** sus originales antes de solicitar una única elevación para escribir ese lote. El proceso elevado comprueba todos los originales antes de empezar, vuelve a comprobar cada valor y solo admite las rutas del catálogo y DWORD tipados. El proceso principal lee de nuevo cada resultado. Si una escritura o la autorización falla, puede haber un lote parcial: no se anuncia como revertido automáticamente; sus originales y estados quedan disponibles para recuperar. La creación opcional del punto de Windows es una operación elevada separada.

## Perfiles personalizados

Guardar perfil exporta un JSON versionado con nombre e identificadores de ajustes. No contiene claves arbitrarias, comandos, scripts, valores actuales ni copias de recuperación. Cargar perfil valida formato, identificadores, duplicados y exclusiones. Selecciona solo los compatibles y muestra omisiones; requiere la revisión normal para aplicar. Se comparten selecciones, no respaldos de otro equipo.
