# Roadmap

Un renglón por commit, en orden. **Cada commit hace una sola cosa**, se puede verificar solo y deja la app andando: si algo no compila o no se puede probar, la tanda estaba mal cortada.

Cincuenta commits chicos en vez de veinte grandes. Tarda lo mismo, pero se lee mejor: cada diff entra en una pantalla y cada uno es un lugar donde frenar, mirar y decir «esto no».

**El mensaje de cada commit es el que va a git tal cual.** Los marco con `[x]` a medida que entran.

Es un plan, no un contrato — si aparece algo mejor a mitad de camino, se cambia.

---

## Fase 0 · El terreno

**0 ·** `docs(app): documentar el proyecto y sumar las maquetas` — [x]

Contexto, convenciones y las nueve pantallas explicadas. Va primero para no volver a discutir en cada sesión cómo se escribe el código ni cómo era una pantalla.

**1 ·** `chore(app): crear el proyecto f4872`

El esqueleto de `dotnet new mvc` y el `.gitignore`. No hace nada todavía; es el commit que hace que los demás compilen.
→ *Verificar:* `dotnet run` levanta y abre la página de inicio.

**2 ·** `chore(app): conectar a Postgres con EF Core`

Los paquetes de EF Core y Npgsql, el `AppDbContext` vacío y la cadena de conexión. Sin entidades todavía: primero que la app hable con la base, después qué le dice.
→ *Verificar:* la app levanta sin explotar al pedir el contexto.

**3 ·** `chore(app): fijar la cultura es-AR`

Va sola y temprano porque es de las que se olvidan: sin esto los precios salen `$12,500.00` en vez de `$12.500`. Se fija una vez en el arranque y no en cada vista.
→ *Verificar:* una vista de prueba muestra `$12.500` y `01/09/2026`.

**4 ·** `chore(app): traer la paleta y las tipografías del diseño`

Los ocho colores y las dos fuentes de las maquetas, como variables CSS. Van antes que cualquier pantalla para que ninguna las invente por su cuenta.
→ *Verificar:* el verde `#1E5245` y la Bowlby One cargan en la página de inicio.

---

## Fase 1 · El modelo

Es lo único que ya está validado: sale del diseño, no hay que inventarlo. Una entidad por commit y una migración por commit, así se lee de a una.

**5 ·** `feat(datos): modelar el producto`

Familia, nombre, precio y activo, con su `ProductoConfiguracion`. Las empanadas no llevan precio propio: se cobran por pack.
→ *Verificar:* `dotnet ef database update` y ver la tabla en pgAdmin.

**6 ·** `feat(datos): modelar el ingrediente`

Unidad, stock, cómo se compra (qué cantidad trae y cuánto sale) y la bandera de lo que no se compra — el agua, la masa madre. El precio por gramo se deriva, no se guarda.
→ *Verificar:* la tabla aparece con las dos columnas de la compra.

**7 ·** `feat(datos): modelar la base`

La sub-receta que consume un producto: el bollo y la tapa. Lleva el `rinde` porque se amasa por tanda entera, no de a un bollo.

**8 ·** `feat(datos): relacionar el producto con sus ingredientes`

`ProductoIngrediente`, con la cantidad y el quitable **en el par**: la fugazzeta lleva 200 g de cebolla y la empanada de carne 15; la muzza se saca de una fugazzeta y la salsa no. Es la parte del modelo que más costó cerrar.
→ *Verificar:* la clave compuesta y las dos relaciones en la migración.

**9 ·** `feat(datos): relacionar la base con sus ingredientes`

`BaseIngrediente` con la cantidad **de la tanda entera** — 1 kg de harina, 700 g de agua, 100 g de masa madre, 30 g de sal. Quien lea la tabla tiene que ver la receta como él la dice; dividir por el rinde es problema del cálculo.
→ *Verificar:* cargar la receta del bollo a mano y que queden los cuatro renglones.

**10 ·** `feat(datos): modelar el pedido`

Cliente, teléfono, cuándo se hizo, cuándo se entrega y el estado. Todavía sin ítems.

**11 ·** `feat(datos): guardar los ítems con su precio`

`ItemPedido` **copia el precio** en vez de leerlo del producto: si en marzo la muzza salía $9.000 y hoy sale $11.000, el pedido de marzo tiene que seguir diciendo $9.000.

**12 ·** `feat(datos): guardar los ingredientes que se sacan`

El «sin» del ítem, en una tabla chica colgada de él. Copiado igual que el precio: un pedido viejo tiene que poder leerse aunque el ingrediente ya no esté en la carta.

**13 ·** `feat(datos): sembrar datos de prueba`

Sin datos no se prueba ninguna pantalla. Entra la carta de la maqueta **marcada como de prueba y solo en Development**, para que nunca se confunda con la real (ver `CLAUDE.md`).

---

## Fase 2 · La tienda

Contra `diseño/la-tienda.html`. Mobile-first, midiendo en 390 px.

**14 ·** `feat(tienda): armar el armazón de la tienda`

La cabecera, las tres solapas y el hueco de la lista, sin datos. Primero la caja, después lo que va adentro.
→ *Verificar:* en 390 px entra sin scroll horizontal.

**15 ·** `feat(tienda): mostrar las pizzas y las focaccias`

Salen de la base y comparten renglón porque se piden igual: nombre, ingredientes y precio.
→ *Verificar:* las siete pizzas entran sin scroll y los cuatro ingredientes en una fila.

**16 ·** `feat(tienda): mostrar las empanadas por pack`

Van aparte porque no tienen precio propio: se cobran por pack de 6 o de 12, de un solo gusto.

**17 ·** `feat(tienda): contar y sumar productos al pedido`

Los contadores. El carrito vive en el navegador: un pedido a medio armar no es un pedido, no tiene por qué ocupar una fila ni avisarle nada a nadie.

**18 ·** `feat(tienda): sacar ingredientes de una pizza`

Solo los marcados como quitables en el commit 8. Los fijos se ven igual pero no se tocan.

**19 ·** `feat(tienda): mostrar la barra del pedido`

El total y el desglose — «1 pizza · 2 packs». La focaccia cuenta como pieza y no como pizza.

**20 ·** `feat(tienda): guardar el pedido en el navegador`

Para que no se pierda al recargar ni al volver atrás.
→ *Verificar:* sumar, sacar un ingrediente, recargar y que el pedido siga.

**21 ·** `feat(tienda): mostrar el resumen del checkout`

Agrupado por nombre **y por lo que se sacó**: dos margaritas iguales son un renglón, una sin albahaca es otro.

**22 ·** `feat(tienda): pedir los datos del cliente`

Nombre, teléfono y dirección.
→ **Falta decidir:** cómo se pide la dirección — hay tres versiones en la maqueta.

**23 ·** `feat(tienda): guardar el pedido confirmado`

Acá el pedido pasa del navegador a la base y deja de ser reversible.
→ *Verificar:* confirmar y encontrarlo en `Pedidos` con sus ítems y sus precios copiados.

**24 ·** `feat(tienda): mostrar la confirmación`

Qué pidió, cuánto sale y qué pasa ahora — que él escribe por WhatsApp.

**25 ·** `feat(tienda): mostrar la tienda cerrada`

Con el interruptor en cerrado, el que entra tiene que entender qué pasa y cuándo volver, en vez de encontrar una carta que no lo deja pedir.
→ **Falta decidir:** cuál de las tres.

---

## Fase 3 · El circuito

**26 ·** `feat(panel): entrar al panel con una clave`

Una clave y una cookie, sin Identity: lo usa una sola persona, y armar usuarios, roles y recupero de contraseña para uno solo es trabajo que no rinde. Va primero para que ninguna pantalla del panel nazca abierta.
→ *Verificar:* sin cookie, cualquier ruta del panel redirige al login.

**27 ·** `feat(panel): armar la barra lateral y la cabecera`

Las seis secciones y el pie con el estado; las pantallas todavía vacías.
→ *Verificar:* la marca y el título caen en la misma línea de base en las seis.

**28 ·** `feat(panel): listar los pedidos`

La pantalla que va a mirar todos los días.

**29 ·** `feat(panel): ver el detalle de un pedido`

Los ítems con su «sin», el total y el enlace `wa.me`: el chat lo sigue haciendo él desde el celular, la app solo abre la conversación con el número ya puesto.

**30 ·** `feat(panel): avanzar y cancelar el estado del pedido`

Los cuatro estados. Cancelar no borra: sale de los cálculos y queda en el historial.
→ *Verificar:* cancelar uno y que los números bajen.

**31 ·** `feat(app): avisar el pedido nuevo por Telegram`

Sin esto tiene que entrar al panel a ver si entró algo. Un POST a la Bot API, al chat privado, apenas se confirma.
→ *Verificar:* hacer un pedido y que llegue el mensaje.

**32 ·** `feat(panel): contar la producción del finde`

Los tres titulares: cuántos bollos, cuántas empanadas, cuántos pedidos.

**33 ·** `feat(panel): desglosar la producción por producto`

En pizzas importa el «sin», porque son piezas distintas; en empanadas se cuenta por unidad y no por pack, porque el pack es cómo se vende y no cómo se arma.

**Con este commit el circuito está cerrado: se puede tomar un pedido de verdad un sábado.**

---

## Fase 4 · La administración

Se puede hacer a mano en la base al principio, por eso va después del circuito.

**34 ·** `feat(panel): listar los productos`

Hasta acá la carta se toca por SQL. Esta pantalla empieza a devolvérsela.

**35 ·** `feat(panel): editar un producto`

La ficha en renglón etiquetado, con el precio y el estado. **Agotar en vez de borrar**: un producto borrado se lleva puestos los pedidos que lo nombran.

**36 ·** `feat(panel): dar de alta un producto`

El alta al pie de la lista, con el botón redondo.

**37 ·** `feat(panel): cargar la receta de un producto`

Los ingredientes con su cantidad, elegidos **de los guardados y nunca escritos libres**: si no, la misma cebolla entra tres veces escrita distinto y los costos dejan de cerrar.

**38 ·** `feat(panel): elegir qué ingredientes se pueden sacar`

El interruptor por par — el que alimenta el commit 18.

**39 ·** `feat(panel): listar los ingredientes con su stock`

Con las cantidades legibles: 42 kg y no 42000 g.

**40 ·** `feat(panel): editar y dar de alta un ingrediente`

Nombre, unidad y la bandera de lo que no se compra.

**41 ·** `feat(panel): cargar la compra del ingrediente`

Cuánto trae y cuánto sale. De ahí sale el precio por gramo, que es lo que después usan los costos.

**42 ·** `feat(panel): cargar las bases por tanda`

La receta del bollo como se amasa —1 kg de harina para 6 bollos— y el panel divide.
→ *Verificar:* la harina de una pizza da 166,7 g.

**43 ·** `feat(panel): armar la lista de compras`

El stock contra lo que hace falta para los pedidos del finde. El agua y la masa madre no aparecen: no se compran.

**44 ·** `feat(panel): calcular el costo de cada producto`

Receta × precio de compra, con la base desglosada.
→ *Verificar:* cambiar el precio de una compra y que el costo se mueva.

**45 ·** `feat(panel): mostrar el margen y avisar los datos incompletos`

Si a un producto le falta cargar un ingrediente, el margen sale marcado como sin confirmar: incompleto se ve mejor de lo que es, y es el número con el que se ponen los precios.

**46 ·** `feat(panel): mostrar las cifras del inicio`

Pedidos nuevos, falta comprar, producción y total. Van últimas porque salen de pantallas que recién ahora existen.

**47 ·** `feat(panel): sumar las dos listas del inicio`

Hornear y Comprar, renglón a renglón y al mismo paso.

---

## Fase 5 · El cierre

**48 ·** `feat(panel): abrir y cerrar la tienda`

El interruptor que enciende el commit 25. Vive en la cabecera de Inicio, con el estado repetido en el pie de la barra lateral para que se vea desde cualquier pantalla.

**49 ·** `chore(app): preparar el deploy en Railway`

Sacarla de la máquina. La cadena de conexión pasa a variable de entorno y las migraciones corren al arrancar, así publicar es un push y no un trámite.

**50 ·** `docs(app): escribir el README`

Va al final porque recién ahí se sabe qué se hizo de verdad. Qué es, cómo se levanta, capturas.

---

## Lo que no depende del código

El **logo**, las **fotos cenitales** (centradas, mismo fondo, misma altura, misma luz), los **precios reales** y el **dominio** — los `.com.ar` estaban libres el 2026-08-25 y eso cambia.

La **carta de verdad** hace falta antes de la fase 4: nombres, precios, gustos, ingredientes de cada producto, cuáles se pueden sacar y cómo se compra cada uno con su precio.
