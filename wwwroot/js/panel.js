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
// El campo y la lista van dibujados y visibles en el HTML, y esto los esconde
// al cargar: sin JavaScript queda el campo siempre a la vista, que funciona
// igual porque el servidor valida el nombre. Con JavaScript se comporta como la
// maqueta: en reposo un (+), y al tocarlo aparecen el campo y la lista.
(function () {
  var caja = document.querySelector("[data-candidatos]");
  var buscar = document.querySelector("[data-buscar]");
  var mas = document.querySelector("[data-abrir-ingrediente]");
  if (!caja || !buscar || !mas) return;

  var cuanto = document.querySelector(".ingr .cuanto");
  var nada = caja.querySelector(".nada");
  var opciones = [].slice.call(caja.querySelectorAll("[data-elegir]"));

  function mostrar(abierto) {
    mas.hidden = abierto;
    buscar.hidden = !abierto;
    if (cuanto) cuanto.hidden = !abierto;
    caja.hidden = !abierto;
    if (abierto) buscar.focus();
  }

  function filtrar() {
    // sin acentos y sin mayúsculas: buscar "oregano" tiene que encontrar
    // "Orégano", que es justo el par que se duplica cuando se escribe libre
    var texto = pelado(buscar.value);
    var quedan = 0;

    opciones.forEach(function (b) {
      var entra = pelado(b.dataset.elegir).indexOf(texto) >= 0;
      b.parentElement.hidden = !entra;
      if (entra) quedan++;
    });

    nada.hidden = quedan > 0;
  }

  function pelado(t) {
    return t.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().trim();
  }

  mas.addEventListener("click", function () { mostrar(true); });
  buscar.addEventListener("input", filtrar);

  caja.addEventListener("click", function (e) {
    var b = e.target.closest("[data-elegir]");
    if (!b) return;
    buscar.value = b.dataset.elegir;
    caja.hidden = true;
    // elegido el ingrediente, lo único que falta es cuánto lleva
    if (cuanto) cuanto.focus();
  });

  buscar.addEventListener("keydown", function (e) {
    if (e.key === "Escape") { buscar.value = ""; mostrar(false); }
  });

  mostrar(false);
})();
