-- Saca todo lo que metio meses-de-mentira.sql. No toca nada mas: los pedidos de
-- prueba son los unicos con el cliente «PRUEBA».

BEGIN;

DELETE FROM "ItemQuitados"
WHERE "IdItemPedido" IN (
    SELECT i."IdItemPedido" FROM "ItemPedidos" i
    JOIN "Pedidos" p ON p."IdPedido" = i."IdPedido"
    WHERE p."Cliente" LIKE 'PRUEBA%');

DELETE FROM "ItemPedidos"
WHERE "IdPedido" IN (SELECT "IdPedido" FROM "Pedidos" WHERE "Cliente" LIKE 'PRUEBA%');

DELETE FROM "Pedidos" WHERE "Cliente" LIKE 'PRUEBA%';

COMMIT;

SELECT COUNT(*) AS "pedidos de prueba que quedan" FROM "Pedidos" WHERE "Cliente" LIKE 'PRUEBA%';
