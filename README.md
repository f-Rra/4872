# 48·72

[![CI](https://github.com/f-Rra/4872/actions/workflows/ci.yml/badge.svg)](https://github.com/f-Rra/4872/actions/workflows/ci.yml)
![.NET 9](https://img.shields.io/badge/.NET-9.0-blue)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-EF%20Core-336791)
![Docker](https://img.shields.io/badge/Docker-Railway-0db7ed)

Tienda web y panel de vendedor para una pizzería napoletana de una sola sucursal.

**Sitio web:** [4872.com.ar](https://4872.com.ar) (la tienda). El panel es privado y se ve en las capturas, que son de una copia local con datos inventados.

## Funcionalidades

- **Pedido en un minuto desde el teléfono**, sin cuenta ni contraseña. Se pueden sacar ingredientes («una margarita sin albahaca») y las empanadas se piden por pack.
- **Carta que se actualiza sola**: lo agotado se calcula a partir del stock de ingredientes, sin que nadie lo marque.
- **Aviso de cada pedido por Telegram** al vendedor.
- **Plan de producción del fin de semana**: el panel suma todos los pedidos y calcula cuántas recetas de masa amasar y qué falta comprar.
- **Costos y márgenes** de cada producto y de cada mes, marcando lo que rinde poco.

## Capturas

**La tienda**, pensada para el teléfono:

| | | | |
|---|---|---|---|
| <img src="docs/capturas/tienda-1-inicio.png" width="200"> | <img src="docs/capturas/tienda-2-carta.png" width="200"> | <img src="docs/capturas/tienda-3-checkout.png" width="200"> | <img src="docs/capturas/tienda-4-gracias.png" width="200"> |
| Inicio | La carta, con lo agotado tachado | Tu pedido | Confirmación |

**El panel** del vendedor:

| | | | |
|---|---|---|---|
| <img src="docs/capturas/panel-1-inicio.png" width="200"> | <img src="docs/capturas/panel-2-pedidos.png" width="200"> | <img src="docs/capturas/panel-3-produccion.png" width="200"> | <img src="docs/capturas/panel-7-costos.png" width="200"> |
| Qué hornear y qué comprar | Pedidos, con WhatsApp al cliente | Producción: todo sumado para amasar una vez | Costos y márgenes por producto y por mes |

## Stack

.NET 9 · ASP.NET Core MVC · EF Core (Fluent API) · PostgreSQL · Razor y JavaScript sin librerías · CSS propio · Docker · Railway · Cloudflare.

## Modelo de datos

```mermaid
erDiagram
    PEDIDO ||--|{ ITEM_PEDIDO : contiene
    ITEM_PEDIDO ||--o{ ITEM_QUITADO : "sin ingrediente"
    PRODUCTO ||--o{ ITEM_PEDIDO : "se pide en"
    PRODUCTO ||--o{ PRODUCTO_INGREDIENTE : lleva
    PRODUCTO ||--o{ PRODUCTO_SALSA : lleva
    INGREDIENTE ||--o{ PRODUCTO_INGREDIENTE : "se usa en"
    INGREDIENTE ||--o{ RECETA_INGREDIENTE : "se usa en"
    RECETA ||--|{ RECETA_INGREDIENTE : "se hace con"
    RECETA ||--o{ PRODUCTO_SALSA : "es salsa de"
    RECETA |o--o{ PRODUCTO : "es la base de"
    RECETA |o--o{ PRODUCTO : "es el relleno de"

    PEDIDO {
        string Cliente
        datetime FechaPedido
        enum Estado "Nuevo, Preparando, Entregado, Cancelado"
    }
    ITEM_PEDIDO {
        int Cantidad
        int UnidadesPorPack "solo empanadas"
        decimal PrecioUnitario "el de ese día"
    }
    PRODUCTO {
        enum Familia "Pizza, Focaccia, Empanada"
        string Nombre
        decimal Precio
    }
    INGREDIENTE {
        string Nombre
        decimal Stock
        decimal PrecioDeCompra
    }
    RECETA {
        enum Tipo "Base, Salsa, Relleno"
        int Rinde "cuántas piezas salen"
    }
    PACK {
        int Unidades
        decimal Precio
    }
    TIENDA {
        bool Abierta
    }
```

La cantidad de cada ingrediente es del par producto-ingrediente, y las recetas (bases, salsas y rellenos) se cargan enteras con su rinde.

## Arquitectura

```mermaid
flowchart LR
    Cliente["Cliente (teléfono)"] --> CF[Cloudflare]
    Vendedor["Vendedor (panel)"] --> CF
    CF --> Ctrl
    subgraph Railway["Railway · contenedor Docker"]
        Ctrl["Controladores<br/>Tienda · Panel"] --> Serv["Servicios<br/>PedidoService · RecetaService"]
    end
    Serv --> DB[("PostgreSQL")]
    Serv --> Tg["Telegram<br/>(pedidos y fallas)"]
```

## Decisiones técnicas

- **El diseño va antes que el código.** Las pantallas se maquetaron y se midieron primero; las maquetas de [`diseño/`](diseño/) son la especificación.
- **Sin ASP.NET Identity.** Una clave comparada en tiempo fijo, límite de 5 intentos por minuto por IP, antiforgery en el panel y las llaves de la cookie guardadas en la base.
- **Recetas enteras con su rinde.** El sistema divide, no la persona: 1 kg de harina da 6 bollos.
- **Avisos por Telegram** de pedidos y de fallas, sin datos del cliente. Si Telegram falla, el pedido se guarda igual.
- **Despliegue continuo.** `Dockerfile` en dos etapas, Railway con deploy en cada push a `main` y las migraciones al arrancar.

El porqué de cada una está en [`docs/decisiones-tecnicas.md`](docs/decisiones-tecnicas.md).

## Tests y CI

138 tests de xUnit sobre `PedidoService` (las reglas del checkout) y `RecetaService` (qué hornear, qué comprar y cuánto deja cada producto), con el resultado esperado calculado a mano. El CI los corre en cada push.

## Autor

- Estudiante de la Tecnicatura Universitaria en Programación - UTN FRGP
- Email: [facundo.rra@gmail.com](mailto:facundo.rra@gmail.com)
