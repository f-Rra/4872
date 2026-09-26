// Lo único que el panel le pide al navegador.
//
// Cancelar un pedido no se deshace: no hay camino de vuelta desde cancelado, así
// que un click de más en el enlace equivocado es un pedido perdido. Sin
// JavaScript el formulario manda igual, que es lo correcto: la pregunta es una
// red y no un candado.
(function () {
  document.addEventListener("submit", function (e) {
    var boton = e.submitter;
    if (!boton || !boton.dataset.confirmar) return;
    if (!window.confirm(boton.dataset.confirmar)) e.preventDefault();
  });
})();

// El buscador de ingredientes de la ficha de producto.
//
// El campo y la lista van dibujados y visibles en el HTML, y esto los esconde al
// cargar: sin JavaScript el buscador queda siempre a la vista y la pantalla
// funciona igual, en dos viajes al servidor en vez de uno. Con JavaScript se
// comporta como se diseno: en reposo un (+), y al tocarlo aparecen los dos.
(function () {
  var caja = document.querySelector("[data-candidatos]");
  var buscar = document.querySelector("[data-buscar]");
  var abren = [].slice.call(document.querySelectorAll("[data-abrir-ingrediente]"));
  if (!caja || !buscar || !abren.length) return;

  var nada = caja.querySelector(".nada");
  var opciones = [].slice.call(caja.querySelectorAll("[data-elegir]"));
  var cuanto = null;

  function mostrar(abierto) {
    // el (+) y su texto van juntos: los dos abren y los dos se van
    abren.forEach(function (b) { b.hidden = abierto; });
    buscar.hidden = !abierto;
    caja.hidden = !abierto;
    if (abierto) buscar.focus();
  }

  // sin acentos y sin mayusculas: buscar "oregano" tiene que encontrar
  // "Oregano" con acento, que es justo el par que se duplica al escribir libre
  function pelado(t) {
    return t.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase().trim();
  }

  function filtrar() {
    var texto = pelado(buscar.value);
    var quedan = 0;

    opciones.forEach(function (b) {
      var entra = pelado(b.dataset.elegir).indexOf(texto) >= 0;
      b.parentElement.hidden = !entra;
      if (entra) quedan++;
    });

    nada.hidden = quedan > 0;
  }

  // Elegir uno no manda el formulario: completa el nombre y abre el campo de
  // cuanto en el mismo renglon. Sin JavaScript el boton lo manda y el servidor
  // devuelve la pantalla con el ingrediente ya elegido, que es el mismo paso.
  function elegir(nombre) {
    buscar.value = nombre;
    caja.hidden = true;

    if (!cuanto) {
      cuanto = document.createElement("span");
      cuanto.className = "rcaja";
      cuanto.innerHTML =
        '<input class="rc" type="text" name="cantidad" form="sumar-ingrediente" ' +
        'inputmode="decimal" placeholder="0"><span class="ru"></span>';
      buscar.parentElement.insertBefore(cuanto, buscar.nextSibling);

      // El Enter hay que mandarlo a mano. Por este camino el buscador se queda
      // en el formulario con su name, asi que son dos campos de texto, y con
      // mas de uno el navegador no manda solo: busca un boton de envio, y el
      // primero que encuentra es un candidato de la lista —escondido, y con su
      // propio nombre— que sumaria el ingrediente equivocado.
      //
      // requestSubmit() sin boton manda el formulario sin el valor de ninguno,
      // asi que el nombre sale del buscador, que es el que el cliente eligio.
      cuanto.querySelector(".rc").addEventListener("keydown", function (e) {
        if (e.key !== "Enter") return;
        e.preventDefault();
        document.getElementById("sumar-ingrediente").requestSubmit();
      });
    }

    var unidad = opciones.filter(function (b) { return b.dataset.elegir === nombre; })[0];
    cuanto.querySelector(".ru").textContent = unidad ? unidad.dataset.unidad || "" : "";
    cuanto.hidden = false;
    cuanto.querySelector(".rc").focus();
  }

  abren.forEach(function (b) {
    b.addEventListener("click", function () { mostrar(true); });
  });
  buscar.addEventListener("input", function () {
    if (cuanto) cuanto.hidden = true;
    caja.hidden = false;
    filtrar();
  });

  caja.addEventListener("click", function (e) {
    var b = e.target.closest("[data-elegir]");
    if (!b) return;
    e.preventDefault();
    elegir(b.dataset.elegir);
  });

  buscar.addEventListener("keydown", function (e) {
    if (e.key === "Escape") { buscar.value = ""; if (cuanto) cuanto.hidden = true; mostrar(false); }
  });

  mostrar(false);
})();

// El campo de stock, renglon por renglon.
(function () {
  document.querySelectorAll("[data-medida]").forEach(function (c) {
    // crece y se achica con lo que se escribe adentro. Sin esto el ancho lo fija
    // el servidor con el valor guardado y tipear «11,543 kg» sobre «0 g»
    // escribe afuera de la pastilla
    c.addEventListener("input", function () {
      c.style.width = Math.max(6, c.value.length) + "ch";
    });

    // Se guarda al salir del campo y no solo con Enter: cargar una compra es
    // escribir un numero y seguir al de abajo, y quedarse pensando cual es la
    // tecla que guarda no es parte de eso. Sin JavaScript el Enter igual manda
    // el formulario, que es un post comun.
    var mandado = false;
    c.addEventListener("change", function () {
      if (mandado) return;
      mandado = true;
      c.form.submit();
    });
  });
})();

// La unidad que va al lado del bulto en la ficha del ingrediente. Sale de la
// medida elegida arriba, asi que tiene que cambiar al tocar otro chip y no
// recien al guardar. Sin script la dibuja el servidor con la medida guardada.
(function () {
  var uni = document.querySelector("[data-uni]");
  if (!uni) return;

  var corto = { Gramo: "g", Mililitro: "ml", Unidad: "u" };

  document.querySelectorAll("[name='Ficha.Unidad']").forEach(function (r) {
    r.addEventListener("change", function () {
      uni.textContent = corto[r.value] || r.value;
    });
  });
})();


// En que se cuenta el rinde de una receta sigue al tipo elegido, como el precio
// de un producto sigue a su familia: pasar una salsa a relleno cambia «pizzas»
// por «empanadas» ahi mismo, y no recien al guardar. En una receta nueva cambia
// tambien el rinde, si todavia es el que vino puesto: una salsa rinde 6 y un
// relleno 12.
(function () {
  var chips = [].slice.call(document.querySelectorAll("input[name='Ficha.Tipo'][type='radio']"));
  var rinde = document.querySelector("[data-rinde]");
  if (!chips.length || !rinde) return;

  var pieza = { Salsa: ["pizza", "pizzas"], Relleno: ["empanada", "empanadas"] };
  var deFabrica = { Salsa: "6", Relleno: "12" };
  var nueva = rinde.dataset.nueva === "true";

  function elegido() {
    return (chips.filter(function (c) { return c.checked; })[0] || chips[0]).value;
  }

  var antes = elegido();

  function nombrar() {
    var tipo = elegido();
    if (nueva && rinde.value.trim() === deFabrica[antes]) rinde.value = deFabrica[tipo];
    antes = tipo;

    var p = pieza[tipo];
    document.querySelectorAll("[data-cual]").forEach(function (e) {
      e.textContent = rinde.value.trim() === "1" ? p[0] : p[1];
    });
    document.querySelectorAll("[data-pieza]").forEach(function (e) { e.textContent = p[0]; });
  }

  chips.forEach(function (c) { c.addEventListener("change", nombrar); });
  rinde.addEventListener("input", nombrar);
})();

// El renglon del precio cambia con el tipo, igual que la medida de aca arriba.
// Las empanadas no tienen precio propio: se cobran por pack, y esos dos precios
// son del tamano y no del gusto. El servidor dibuja los dos renglones y esconde
// el que no va, pero decide con la familia GUARDADA: al dar de alta una
// empanada todavia la cree una pizza, asi que el cambio lo hace el chip.
//
// La salsa va con el precio suelto: es de las pizzas y las focaccias, y una
// empanada lleva relleno.
(function () {
  var chips = [].slice.call(document.querySelectorAll("input[name='Ficha.Familia']"));
  var sueltos = [].slice.call(document.querySelectorAll("[data-precio='suelto'], [data-salsa]"));
  var packs = [].slice.call(document.querySelectorAll("[data-precio='pack']"));
  if (!chips.length || !sueltos.length) return;

  function acomodar() {
    var elegido = chips.filter(function (c) { return c.checked; })[0];
    var porPack = !!elegido && elegido.value === "Empanada";
    sueltos.forEach(function (e) { e.hidden = porPack; });
    packs.forEach(function (e) { e.hidden = !porPack; });
  }

  chips.forEach(function (c) { c.addEventListener("change", acomodar); });
})();

// Lo que lleva la salsa elegida, abierto. Vienen dibujadas todas y escondidas
// menos la guardada, asi que al tocar otro chip se abre la suya sin esperar a
// guardar. «Sin salsa» no abre nada.
(function () {
  var chips = [].slice.call(document.querySelectorAll("input[name='Ficha.IdSalsa']"));
  var listas = [].slice.call(document.querySelectorAll("[data-salsa] [data-lectura]"));
  if (!chips.length) return;

  chips.forEach(function (c) {
    c.addEventListener("change", function () {
      listas.forEach(function (l) { l.hidden = l.dataset.lectura !== c.value; });
    });
  });
})();
