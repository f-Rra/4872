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
