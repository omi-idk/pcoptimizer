# Respaldo, puntos y recuperación

## Dos mecanismos con alcances distintos

PCOptimizer mantiene un **registro local por operación o lote** antes de empezar. Contiene identificador y fecha, acciones previstas, destino, estado previo conocido, objetivo solicitado y resultado de cada acción. La selección de energía anterior se consulta realmente; para instalaciones se indica que el estado/versión previa no se han inventariado, en lugar de inventar un respaldo completo.

Además, puede solicitar un **punto de restauración de Windows**. La opción se inicia activada, se comparte entre las pantallas y puede desmarcarse durante la sesión. No cambia una política permanente ni se supone que esa elección sobreviva al reinicio de la app.

El registro local no constituye una imagen del sistema o copia de archivos. Un punto de Windows tampoco sustituye copias de proyectos, partidas guardadas o archivos personales. Eliminar apps puede perder datos que ni reinstalar ni restaurar un punto recuperen.

## Flujo antes de aplicar cambios

1. El usuario revisa y confirma la acción o lote, incluida la preferencia de crear punto.
2. Se escribe primero el registro de acciones planificadas. Si falla su escritura, no se inicia la operación.
3. Con la opción activada se pide elevación solo al proceso de creación del punto. Se usa `Checkpoint-Computer -RestorePointType MODIFY_SETTINGS`, sin alterar políticas de frecuencia ni protección.
4. El proceso consulta `Get-ComputerRestorePoint` y busca la descripción única `PCOptimizer-<identificador del registro>`. Solo un resultado coherente y un proceso completado correctamente permiten asociar la secuencia de Windows y marcar la creación como confirmada.
5. Si no se confirma uno nuevo, el usuario puede cancelar —opción predeterminada— o autorizar expresamente continuar **sin nuevo punto confirmado**. La decisión queda diferenciada en el registro. Un error no se presenta como una creación exitosa ni se atribuye una causa única sin evidencia.
6. Se registra cada acción antes de ejecutarla y su resultado después. Si se detiene el lote, los pasos no iniciados se marcan como no ejecutados. Una interrupción puede dejar pasos pendientes o en ejecución: deben comprobarse en Windows antes de reintentar.

En energía, después de preparar el punto se vuelve a consultar el plan activo y la presencia del destino. Si cambió durante la preparación, se cancela la activación para no sobrescribir un cambio externo basándose en una captura anterior.

La creación manual de un punto está disponible en **Respaldo y recuperación** aunque se haya desmarcado la creación automática. No aplica cambios de rendimiento.

## Restricciones de Windows

La documentación de [Checkpoint-Computer](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/checkpoint-computer?view=powershell-5.1) limita el método a sistemas cliente y normalmente a un punto nuevo cada 24 horas desde Windows 8. Protección del sistema, espacio, políticas y permisos pueden impedirlo. No se cambia `SystemRestorePointCreationFrequency`, no se habilita protección automáticamente y no se omite la política de ejecución de PowerShell.

Se ofrece abrir **Protección del sistema** para revisar y configurar sus opciones mediante Windows. Un punto registrado como creado lo fue según la consulta de entonces; Windows puede eliminarlo después. La lista de PCOptimizer contiene sus registros, no es un inventario actualizado de todos los puntos del sistema.

## Qué muestra cada registro

- Fecha y descripción de la sesión; estado de creación o de ausencia de punto confirmado.
- Descripción única y secuencia del punto asociado, si se confirmó.
- Acciones de PCOptimizer y sus destinos.
- Estado previo conocido y objetivo, distinguiendo expresamente los datos no inventariados.
- Resultado: planificado, en ejecución, aplicado/verificado, omisión, pendiente de reinicio, cancelado o no confirmado, según el mecanismo de cada acción.

La lista no registra cambios que hagan otros programas. Una captura al inicio y un resultado por acción tampoco ofrecen vigilancia continua del sistema.

## Recuperar desde la aplicación

**Abrir asistente de restauración** inicia `rstrui.exe` de Windows desde la interfaz. El usuario identifica allí el punto por descripción/fecha, revisa programas afectados y decide si restaurar. PCOptimizer no preselecciona un punto, no llama a restauración automática ni reinicia el equipo para ello. Windows puede revertir otros cambios posteriores al punto y requerir reinicio; hay que guardar el trabajo.

**Recuperar selección de plan guardada** lee el registro seleccionado, comprueba que el plan original siga existiendo y pasa por el mismo flujo de confirmación, respaldo y verificación que cualquier cambio de energía. Solo recupera el GUID activo; no exporta/recrea las opciones internas de un plan borrado.

La Preview 0.2 implementa reversión de los ajustes de registro de su lista cerrada: conserva tipo, datos y ausencia previa en `%LOCALAPPDATA%\PCOptimizer\Tweaks`. Abre Ajustes de Windows → Revertir ajustes aplicados, elige la operación y revisa la inversa. Se detiene ante conflictos y admite revisar lotes parciales. Esto incluye las políticas implementadas, no servicios ni candidatos sin ejecutor. Consulta [el alcance exacto](ajustes-aplicables.md).

## Almacenamiento y validación

Los registros están en `%LOCALAPPDATA%\PCOptimizer\Recovery`, como JSON versionado por identificador. Las escrituras usan archivo temporal y sustitución del archivo; se fuerza la escritura del contenido antes de promoverlo. Los registros inválidos se conservan y se informa el problema. Las lecturas y escrituras tienen un límite de 8 MiB y profundidad JSON limitada; alcanzar un límite impide continuar y requiere archivar el registro, no borrarlo silenciosamente.

La confirmación del proceso elevado viaja por una named pipe local restringida a la misma cuenta y se comprueba el PID del proceso cliente. El proceso elevado no escribe resultados en rutas controladas por el usuario. Solo se aceptan hasta 8 KiB, con descripción, secuencia y fecha coherentes con la solicitud. Si UAC usa otra cuenta administradora, el canal no la acepta y no se confirma el punto; hay que comprobar el resultado en Windows. El script elevado limita la carga de módulos a los módulos del PowerShell del sistema.

Las pruebas del núcleo verifican persistencia, asociación de puntos con su sesión, resultados individuales, captura de selección de energía y errores de escritura/corrupción. No ejecutan la creación o restauración de Windows. Hace falta validar UAC, protección, límites, consulta real de puntos, cancelaciones y apertura del asistente en Windows; este entorno Linux solo compila la aplicación y ejecuta las pruebas del núcleo.
