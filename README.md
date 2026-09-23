# 48·72

Tienda web y panel de vendedor para una pizzería napoletana de una sola sucursal. El nombre son las horas que fermenta la masa: se pide durante la semana y se entrega el fin de semana.

## Stack

.NET 9 · ASP.NET Core MVC · EF Core + PostgreSQL · CSS propio, sin frameworks.

## Cómo se levanta

Hace falta el SDK de **.NET 9** y un **PostgreSQL** andando.

```bash
git clone https://github.com/f-Rra/4872.git
cd 4872
dotnet user-secrets set "Postgres:Clave" "tu-clave"
dotnet user-secrets set "Panel:Clave" "la-que-quieras"
dotnet run
```

La base se crea sola al arrancar: no hace falta `createdb` ni `database update`. En `Development` se siembra una carta de ejemplo con **datos inventados**, para tener algo que mirar en las pantallas.

## El diseño va antes que el código

Las pantallas se maquetaron y se midieron antes de escribir la primera vista. Las maquetas están en `diseño/` y son la especificación: ante una duda, se miran primero.

## Más

`CLAUDE.md` tiene las convenciones de código y el modelo de datos. `ROADMAP.md`, el plan de trabajo y qué se hizo de verdad.
