# 48·72

Tienda web y panel de vendedor para una pizzería napoletana de una sola sucursal. El nombre son las horas de fermentación de la masa. Los clientes piden durante la semana; el vendedor produce y entrega el fin de semana. El pedido se guarda y se le avisa por **Telegram**; después él coordina por WhatsApp desde su celular.

## Estado

**El diseño está cerrado antes que el código.** Nueve pantallas maquetadas y medidas, con las decisiones tomadas una por una. Las maquetas viven en `diseño/` y están publicadas:

- **[La tienda](https://claude.ai/code/artifact/019297d7-ce3f-463a-a117-3341c3b04f8b)** (`diseño/la-tienda.html`) — las tres pantallas del comprador: la carta, el checkout y la confirmación.
- **[El panel](https://claude.ai/code/artifact/0a303942-558d-41e1-940e-fb75788f0e0f)** (`diseño/el-panel.html`) — las seis del vendedor.

Los dos archivos **se explican solos**: cada pantalla trae al costado por qué quedó así, qué se descartó y sus medidas cerradas. Son la especificación; ante una duda de diseño, se miran primero.

**Sin decidir todavía:** cómo se pide la dirección en el checkout (tres versiones armadas) y cómo se ve la tienda cerrada (tres versiones armadas).

## Stack

.NET 9 · ASP.NET Core MVC · EF Core code-first con Fluent API · PostgreSQL (instalado nativo, no Docker) · Bootstrap 5 + CSS propio · Razor + JS vanilla · Railway o Render para el deploy.

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
- **Base** — sub-receta que consume un producto: el **bollo de masa** y la **tapa de empanada**. Se carga **por tanda entera con un `rinde`**, como se amasa, y el panel divide.

La receta del bollo, dicha por el vendedor: **1 kg de harina · 700 g de agua · 100 g de masa madre · 30 g de sal → 6 bollos de 280 g**.

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

No sembrar la base con eso como si fuera la carta de verdad. Lo real lo tiene que dar él, o cargarlo desde las pantallas de Productos e Ingredientes, que están diseñadas justo para eso. Lo único real hoy es la receta del bollo.

## Comandos

```bash
dotnet build
dotnet run
dotnet ef migrations add <Nombre>
dotnet ef database update
```
