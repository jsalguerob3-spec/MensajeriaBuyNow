# BuyNow — Prototipo de mensajería

Aplicación de consola en C# y .NET 10 para registrar compras y notificar su progreso por correo mediante Brevo. SMS y WhatsApp quedan fuera de esta versión.

## Ejecutar

Abrí MensajeriaBuyNow.slnx en Visual Studio y seleccioná CapaPresentacion como proyecto de inicio. También podés ejecutar desde esta carpeta:

```powershell
dotnet run --project MensajeriaBuyNow/CapaPresentacion.csproj
```

Copiá MensajeriaBuyNow/correo.ejemplo.json como correo.local.json en la misma carpeta y completá ApiKey con tu clave de Brevo. Se copia a la carpeta de ejecución al compilar. El remitente configurado es BuyNow <no-reply@buynow.org.uk>. La configuración local está excluida de Git.

## Demostración

1. Registrá una compra con nombre, correo, descripción y total en quetzales.
2. Consultá la compra para ver sus datos.
3. Entrá a la opción 5, revisá el destinatario y escribí SI para enviar un correo real.
4. Cambiá el estado desde la opción 4 y volvé a la opción 5 para notificarlo.
5. Consultá el historial de correos desde la opción 6.
6. Cerrá y abrí la aplicación para comprobar que conserva las compras.

El estado sigue Recibida → Preparando → Enviada → Entregada. Se permite cancelar desde Recibida o Preparando. Un estado terminal no puede modificarse.

## Capas

| Proyecto | Responsabilidad |
| --- | --- |
| CapaPresentacion (carpeta MensajeriaBuyNow) | Menú, entrada de datos y composición de dependencias |
| CapaEntidades | Compras, estados, historial y contratos |
| CapaLogica | Validaciones, transiciones y coordinación de notificaciones |
| CapaDatos | Lectura y escritura de JSON |
| CapaServicios | Comunicación HTTP con Brevo |

La lógica trabaja con interfaces para permitir el cambio de almacenamiento o de servicio de correo.

## Persistencia y alcance

Los datos están en Datos/compras.json junto al ejecutable (por defecto MensajeriaBuyNow/bin/Debug/net10.0). Respaldá esa carpeta antes de limpiar bin o cambiar a Release. Se permite una instancia por carpeta de datos y se guarda mediante archivo temporal antes del reemplazo.

El límite local es de hasta 100 intentos por día UTC, incluidos los fallidos. No consulta el consumo de otras aplicaciones ni sustituye la cuota de Brevo. El envío es manual, con vista previa, y cada estado se notifica una sola vez cuando es aceptado.

Aceptado significa que Brevo recibió la solicitud, no que llegó a la bandeja. Ante un timeout, el resultado es Incierto y se bloquea el reenvío de ese estado para evitar duplicados. Revisá el panel de Brevo; esta versión no incluye conciliación automática ni webhooks. Si se cierra durante un envío, el registro queda En proceso y también requiere revisión.

## Verificación

```powershell
dotnet build MensajeriaBuyNow.slnx
dotnet run --project Pruebas/Pruebas.csproj
```

Las pruebas usan almacenamiento temporal y un proveedor simulado. Verifican persistencia, validaciones, estados, duplicados, cuota, timeout y el contrato HTTP. No consumen correos. La entrega real requiere una prueba con un destinatario autorizado desde el menú.

Integración basada en la [documentación oficial de Brevo](https://developers.brevo.com/docs/send-a-transactional-email).
