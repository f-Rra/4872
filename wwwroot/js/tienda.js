// Las solapas cambian qué familia se ve. Las tres listas ya vienen en el HTML:
// se muestran y se esconden, no se piden de nuevo al servidor, así que cambiar
// de solapa es instantáneo y funciona con la carta ya cargada.
(function () {
  var solapas = document.getElementById("solapas");
  var lista = document.getElementById("lista");
  if (!solapas || !lista) return;

  solapas.addEventListener("click", function (e) {
    var boton = e.target.closest("button[data-solapa]");
    if (!boton) return;

    solapas.querySelectorAll("button").forEach(function (b) {
      b.setAttribute("aria-pressed", String(b === boton));
    });

    lista.querySelectorAll("[data-familia]").forEach(function (g) {
      g.hidden = g.dataset.familia !== boton.dataset.solapa;
    });

    lista.scrollTop = 0;
  });

  // el tamaño del pack: por ahora solo se elige. Empieza a servir de verdad en
  // el commit 20, cuando el contador tenga que saber a qué pack suma
  var tam = document.getElementById("tam");
  if (tam) {
    tam.addEventListener("click", function (e) {
      var boton = e.target.closest("button[data-unidades]");
      if (!boton) return;
      tam.querySelectorAll("button").forEach(function (b) {
        b.setAttribute("aria-pressed", String(b === boton));
      });
    });
  }
})();
