# Ajustes aplicables: Preview 0.2

La pantalla Ajustes de Windows y la sección Gaming comparten un motor de ejecución. Sus controles escriben preferencias y políticas reales del registro. El catálogo original es una referencia separada: los candidatos restantes no se anuncian como ejecutables.

## Perfiles

| Perfil | Selección exacta |
|---|---|
| Mínimo | Modo Juego |
| Recomendado | Modo Juego y extensiones de archivos visibles |
| Gaming | Modo Juego, desactivar capturas de Game Bar y desactivar aceleración del puntero |
| Intensivo | Gaming, desactivar transparencias y animación de minimizar ventanas |

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

HAGS, MPO y las políticas no entran en los perfiles automáticos. Las políticas requieren Pro, Enterprise o Education y se bloquean ante inscripción MDM/unión organizativa detectada; el proceso elevado comprueba además pertenencia a dominio. La detección no certifica ausencia de toda política local o administración de terceros: no uses estos controles para sustituir decisiones de una organización.

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
