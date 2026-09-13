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
