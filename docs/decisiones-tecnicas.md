# Decisiones técnicas

El [README](../README.md) resume cada decisión en una viñeta; acá está el porqué.

## El diseño va antes que el código

Las pantallas se maquetaron y se midieron antes de escribir la primera vista. Las maquetas están en [`diseño/`](../diseño/) y son la especificación: se pueden abrir en el navegador, y ante una duda de diseño se miran primero.

## Un modelo que respeta cómo trabaja una cocina

- La cantidad es del *par* producto-ingrediente: una fugazzeta lleva 200 g de cebolla y una empanada de carne, 15.
- Las recetas se cargan **enteras, con su rinde**, como se amasan: 1 kg de harina da 6 bollos. El sistema divide, no la persona.
- Lo derivado no se guarda: el precio por gramo, el estado de un pedido o el margen se calculan.

## El panel no usa ASP.NET Identity

No hay usuarios que gestionar: es una sola persona. Alcanza con una clave y una cookie de un mes, con las protecciones que sí importan:

- La clave se compara en **tiempo fijo**, para que medir cuánto tarda no revele cuántas letras acertó.
- Cada intento fallido espera un segundo, y hay un **límite de 5 intentos por minuto por IP** con el limitador que trae ASP.NET Core.
- Los formularios del panel llevan token antiforgery; el del checkout, no, a propósito: ahí no hay sesión que robar.
- Las llaves que firman la cookie se guardan **en la base**, no en el disco del contenedor, así la sesión sobrevive a cada despliegue.

## Operación

- **Aviso de pedidos por Telegram.** Si Telegram no está configurado, el pedido se guarda igual.
- **Aviso de fallas** al mismo chat cuando algo revienta en producción: dice dónde y a qué hora, y **nunca** el texto de la excepción ni datos de un cliente. Como mucho uno cada diez minutos.
- La conexión a Telegram sale **forzada por IPv4**: en redes con IPv6 sin ruta, el cliente de .NET se quedaba esperando más de 30 segundos. Medido: con IPv4, 600 ms.
- Las fechas se guardan en UTC y se muestran en hora de Argentina, con una sola función que hace la conversión.
- La cultura `es-AR` se fija una vez al arrancar: los precios salen `$12.500` y no `$12,500.00`.

## Despliegue

Un `Dockerfile` en dos etapas, Railway con deploy automático en cada push a `main`, PostgreSQL como servicio del mismo proyecto y Cloudflare por delante. Las migraciones corren al arrancar, así publicar es un `git push`.

## Tests

138 tests de xUnit sobre lo que decide plata y trabajo, que son los dos servicios con reglas de negocio:

- **`PedidoService.Confirmar`**: las reglas que apagan el botón de la tienda se repiten en el servidor, porque la pantalla se puede saltear. Datos del cliente, tienda cerrada, producto agotado, packs de empanadas, ingredientes que se pueden sacar y el aviso por Telegram, que si falla no pierde el pedido.
- **`RecetaService`**: lo que hay que hornear, lo que falta comprar y cuánto deja cada producto. Los resultados de los tests están calculados a mano sobre una cocina chica de números redondos ([`Cocina.cs`](../f4872.Tests/Cocina.cs)), no copiados de lo que devolvió el código.

Para saber que los tests sirven y no solo que pasan, se rompió el servicio a propósito de unas cuarenta maneras —redondear para abajo, contar los pedidos cancelados, tomar el mes en UTC— y cada rotura tiene que hacer fallar alguno.

El [CI](../.github/workflows/ci.yml) compila y corre todo en cada push y en cada pull request.

**Lo que falta:**

- Tests contra un **PostgreSQL real**. Los de hoy usan la base en memoria de EF Core: prueban las reglas, pero no que las consultas se traduzcan a SQL de Postgres ni que las restricciones de la base se cumplan.
- Tests de los controladores y de la entrada del panel (la clave, el límite de intentos).
