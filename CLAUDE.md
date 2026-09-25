# 48·72

Tienda web y panel de vendedor para una pizzería napoletana de una sola sucursal. El nombre son las horas de fermentación de la masa. Los clientes piden durante la semana; el vendedor produce y entrega el fin de semana. El pedido se guarda y se le avisa por **Telegram**; después él coordina por WhatsApp desde su celular.

## Estado

**El diseño está cerrado antes que el código.** Doce pantallas maquetadas y medidas, con las decisiones tomadas una por una. Las maquetas viven en `diseño/` y están publicadas:

- **[La tienda](https://claude.ai/code/artifact/019297d7-ce3f-463a-a117-3341c3b04f8b)** (`diseño/la-tienda.html`) — las cuatro pantallas del comprador: el inicio, la carta, el checkout y la confirmación.
- **[El panel](https://claude.ai/code/artifact/0a303942-558d-41e1-940e-fb75788f0e0f)** (`diseño/el-panel.html`) — las ocho del vendedor: las siete de trabajo y la entrada.

Los dos archivos **se explican solos**: cada pantalla trae al costado por qué quedó así, qué se descartó y sus medidas cerradas. Son la especificación; ante una duda de diseño, se miran primero.

**Ya decididas** las dos que estaban abiertas: la dirección se pide **en un solo renglón** —lo que falte se arregla por WhatsApp, que es la conversación que va a haber igual— y cerrada se muestra la **versión C**, el cartel «La tienda está cerrada» en lugar de la carta y de las solapas — sin prometer cuándo vuelve, porque el día no está guardado en ningún lado. Las variantes descartadas siguen en las maquetas.

**No queda nada abierto de diseño.** La entrada del panel, que era lo único que faltaba, está maquetada junto con las otras siete.

**En producción** desde el 2026-09-20, y desde el 2026-09-22 en **[4872.com.ar](https://4872.com.ar)**. La imagen la arma el `Dockerfile` del repo y **cada push a `main` despliega solo**. La base es un Postgres del mismo proyecto: la cadena llega en `ConnectionStrings__Postgres` como dirección `postgresql://` y `Program.cs` la traduce, y las migraciones corren al arrancar. Las llaves con las que se firma la cookie del panel viven en una tabla, no en un disco, así que la sesión sobrevive a cada publicación.

**El dominio** está registrado en NIC Argentina —se administra desde Trámites a Distancia— y delegado a **Cloudflare**, que lo pasa por su proxy a Railway (`4872.up.railway.app`). Dos cosas que, si se tocan, tiran el sitio: el cifrado de Cloudflare va en **Full**, ni **Flexible**, que entra en un bucle de redirecciones, ni **Full (strict)**, que según Railway no anda; y el TXT `_railway-verify` se queda, porque sin él Railway devuelve 404 aunque el CNAME esté bien.

Las variables del servicio son cuatro —`ConnectionStrings__Postgres`, `Panel__Clave`, `Telegram__Token` y `Telegram__Chat`— y **ninguna vive en el repo**. En la máquina de uno, las mismas van por `dotnet user-secrets`.

## Stack

.NET 9 · ASP.NET Core MVC · EF Core code-first con Fluent API · PostgreSQL (nativo en la máquina de uno, un servicio en Railway) · **CSS propio, sin frameworks** · Razor + JS vanilla · Railway con `Dockerfile` para el deploy.

Bootstrap y jQuery venían con la plantilla y se fueron en el commit `c2ac2ef`: el diseño está medido al píxel y el reboot de Bootstrap pelea con él. Si alguna pantalla parece necesitar una grilla o un componente, se dibuja.

**Un solo proyecto**, `f4872` (el repo se llama `4872`, pero un identificador de C# no puede empezar con dígito):

```
Controllers/  Models/  Data/  Data/Configuraciones/  Services/
ViewModels/   Helpers/ Views/ Migrations/            wwwroot/
```

Sin tests. Sin ASP.NET Identity al principio: el panel lo usa una persona, alcanza una clave y una cookie.

## Cómo se escribe el código acá

Destilado de **Sistema-Control-Almuerzos**, que es código suyo al 100%:

- **Todo en español**, métodos en infinitivo: `Listar`, `BuscarPorCredencial`, `EmpleadosSinAlmorzar`.
- **`IdEntidad` como clave**: `IdProducto`, `IdIngrediente`.
- **Lo derivado no se guarda**: propiedades calculadas de solo lectura, como `TotalGeneral` o un `Estado` que sale de si otro campo es nulo.
- **Parámetros opcionales con default**, no sobrecargas: `int? idFamilia = null, string busqueda = null`.
- **Errores con contexto humano**: la excepción lleva el texto que va a leer una persona, no el de la base.
- **Comentarios solo para el porqué de lo raro.** No se comenta lo que el nombre ya dice. Sí se comenta *por qué la cantidad va por par producto–ingrediente* o *por qué la base se divide por el rinde*.
- Fluent API en `Data/Configuraciones/`, una clase `XConfiguracion : IEntityTypeConfiguration<X>` por entidad.

## El modelo de datos

Salió del diseño y está validado contra los números reales del vendedor:

- **Producto** — familia (`pizza` / `foc` / `emp`), nombre, precio, activo. Las empanadas **no tienen precio propio**: se cobran por pack de 6 o de 12.
- **La cantidad es del par producto–ingrediente**, no del ingrediente. Una fugazzeta lleva 200 g de cebolla y una empanada de carne, 15.
- **Quitable**, también por par: si el cliente puede pedirlo *sin* ese ingrediente.
- **Ingrediente** — unidad (`g` / `ml` / `u`), stock, y **cómo se compra**: qué cantidad trae la compra y cuánto sale. El precio unitario se deriva. Bandera para lo que no se compra (agua, masa madre).
- **Receta** — lo que se prepara aparte y usan varios productos, de tres tipos: **base, salsa y relleno**. Se carga **entera con un `rinde`**, como se hace, y el panel divide: una base rinde bollos, una salsa pizzas y un relleno empanadas. Las bases son los dos bollos, una por familia, y cada pizza o focaccia toma la suya sola. La tapa de empanada **no** es una base: se compra hecha, así que es un ingrediente más de cada gusto.

Las dos recetas de bollo, dichas por el vendedor. Son lo único real del sistema:

| | pizza | focaccia |
|---|---|---|
| harina | 1 kg | 1 kg |
| agua | 600 g | 750 g |
| masa madre | 100 g | 100 g |
| sal | 30 g | 30 g |
| oliva | — | 50 g |
| **la tanda pesa** | 1730 g | 1930 g |
| **el bollo sale de** | 288 g | 483 g |
| **rinde** | **6** | **4** |

La pizza y la focaccia **no comparten bollo**: son masas distintas.

## Cómo trabajamos

**Yo codeo, él commitea. Yo no commiteo nunca.**

Entre una cosa y la otra tengo que **explicar en detalle qué hice** — archivo por archivo, qué es, por qué existe y qué decide. No un resumen de una línea. Me lo pidió porque en el Recetario le costó seguir el código. Y **tandas chicas**: ya me frenó dos veces por avanzar demasiado sin checkpoint.

Cada tanda termina con: la explicación, **cómo verificarla** (comandos concretos, qué mirar) y el **mensaje de commit** listo para copiar.

## Commits

`tipo(scope): verbo en infinitivo` y bullets con `-` en el cuerpo, en español.

```
feat(tienda): mostrar la carta con las tres solapas
- Pizzas y focaccias comparten el mismo renglón
- Las empanadas van por pack de 6 o de 12, de un solo gusto
```

Scopes: `tienda`, `panel`, `datos`, `app`.

## Los datos de la maqueta son inventados

**Ninguna de las 7 pizzas, las 4 focaccias, los 8 gustos de empanada ni los ~37 ingredientes es real** — los inventé yo para poder diseñar. Tampoco los precios de venta, las compras ni las cantidades.

No sembrar la base con eso como si fuera la carta de verdad. Lo real lo tiene que dar él, o cargarlo desde las pantallas de Productos e Ingredientes, que están diseñadas justo para eso. Lo único real hoy son las dos recetas de bollo.

## Comandos

```bash
dotnet build
dotnet run
dotnet ef migrations add <Nombre>
dotnet ef database update
```
