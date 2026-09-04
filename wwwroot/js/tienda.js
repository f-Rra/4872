// El armazón: por ahora las solapas solo cambian de estado, porque todavía no
// hay carta que mostrar. En el commit 15 esto pasa a filtrar la lista.
(function () {
  var solapas = document.getElementById("solapas");
  if (!solapas) return;

  solapas.addEventListener("click", function (e) {
    var boton = e.target.closest("button[data-solapa]");
    if (!boton) return;

    solapas.querySelectorAll("button").forEach(function (b) {
      b.setAttribute("aria-pressed", String(b === boton));
    });
  });
})();
