-- Pedidos entregados de meses pasados, INVENTADOS, solo para ver la vista por
-- mes de Costos con algo adentro.
--
-- Todos van con el cliente «PRUEBA» para poder sacarlos de una. Para borrarlos:
--     psql -U postgres -d f4872 -f pruebas/meses-de-mentira-borrar.sql
--
-- Los productos y los precios salen de lo que ya esta cargado, asi que los
-- numeros son consistentes con la carta que haya en ese momento.

BEGIN;

-- tres meses cerrados y algo del que esta corriendo
WITH nuevos AS (
    INSERT INTO "Pedidos" ("Cliente", "Telefono", "Direccion", "FechaPedido", "Estado")
    SELECT 'PRUEBA ' || to_char(f, 'TMMonth'), '1100000000', 'Calle Falsa 123', f, 'Entregado'
    FROM (VALUES
        (now() - interval '4 months'),
        (now() - interval '3 months'),
        (now() - interval '2 months'),
        (now() - interval '1 month'),
        (now() - interval '3 days')
    ) AS t(f)
    RETURNING "IdPedido", "FechaPedido"
),
-- A cada pedido le entran cuatro productos, y cada mes arranca en otro punto
-- de la carta: si todos llevaran lo mismo, todos los meses darian el mismo
-- margen y la tabla no mostraria nada.
elegidos AS (
    SELECT n."IdPedido",
           p."IdProducto",
           p."Familia",
           p."Precio",
           -- el corrimiento por mes es lo que hace que cada uno lleve otra cosa
           ((row_number() OVER (PARTITION BY n."IdPedido" ORDER BY p."IdProducto")
             + extract(month FROM n."FechaPedido")::int * 3) % 19) AS puesto,
           1 + (extract(month FROM n."FechaPedido")::int % 5) AS cuantas
    FROM nuevos n
    JOIN "Productos" p ON p."Activo"
)
INSERT INTO "ItemPedidos" ("IdPedido", "IdProducto", "Cantidad", "UnidadesPorPack", "PrecioUnitario")
SELECT e."IdPedido",
       e."IdProducto",
       e.cuantas,
       CASE WHEN e."Familia" = 'Empanada' THEN 12 END,
       COALESCE(e."Precio", (SELECT "Precio" FROM "Packs" WHERE "Unidades" = 12 AND "Activo"))
FROM elegidos e
WHERE e.puesto < 4;

COMMIT;

SELECT to_char("FechaPedido", 'TMMonth YYYY') AS mes, COUNT(*) AS pedidos
FROM "Pedidos" WHERE "Cliente" LIKE 'PRUEBA%' GROUP BY 1, date_trunc('month', "FechaPedido")
ORDER BY date_trunc('month', "FechaPedido");
