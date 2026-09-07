// Las solapas cambian qué familia se ve. Las tres listas ya vienen en el HTML:
// se muestran y se esconden, no se piden de nuevo al servidor, así que cambiar
// de solapa es instantáneo y funciona con la carta ya cargada.
(function () {
  var solapas = document.getElementById("solapas");
  var lista = document.getElementById("lista");
  if (!solapas || !lista) return;

  var tam = document.getElementById("tam");

  // El pedido vive acá y en ningún otro lado. Un pedido a medio armar no es un
  // pedido: no tiene por qué ocupar una fila en la base ni avisarle nada a
  // nadie. Guardarlo entre recargas es otro commit; por ahora dura lo que dure
  // la pestaña abierta.
  var carrito = {};

  // La clave de una pizza o una focaccia la escribe el servidor con el id del
  // producto. La de un pack no se puede escribir de antemano: el mismo gusto es
  // una cosa distinta en x6 que en x12, y el tamaño se elige en la pantalla.
  function clavePack(idGusto, unidades) { return "e" + idGusto + "x" + unidades; }

  function tamanoElegido() {
    var b = tam && tam.querySelector("button[aria-pressed='true']");
    return b ? b.dataset.unidades : null;
  }

  function mover(clave, paso) {
    var cuantos = (carrito[clave] || 0) + paso;
    // el cero se borra en vez de guardarse: el carrito es lo que se pidió, y un
    // producto en cero no se pidió
    if (cuantos > 0) { carrito[clave] = cuantos; } else { delete carrito[clave]; }
    pintar();
  }

  function pintarContador(contador, clave) {
    var cuantos = carrito[clave] || 0;
    contador.dataset.clave = clave;
    contador.classList.toggle("cero", cuantos === 0);
    contador.querySelector(".n").textContent = cuantos;
    // en cero no hay de dónde sacar
    contador.querySelector("[data-menos]").disabled = cuantos === 0;
  }

  function pintar() {
    lista.querySelectorAll("ol.carta .contador").forEach(function (c) {
      pintarContador(c, c.dataset.clave);
    });

    var unidades = tamanoElegido();
    if (!unidades) return;

    lista.querySelectorAll("ol.gustos li[data-gusto]").forEach(function (li) {
      var contador = li.querySelector(".contador");
      if (!contador) return;
      pintarContador(contador, clavePack(li.dataset.gusto, unidades));

      // Cuántos lleva ese gusto en el otro tamaño. Sin esto, cambiar de tamaño
      // parece haber borrado lo que ya había sumado.
      var otro = li.querySelector(".otro");
      var dice = [];
      tam.querySelectorAll("button[data-unidades]").forEach(function (b) {
        if (b.dataset.unidades === unidades) return;
        var cuantos = carrito[clavePack(li.dataset.gusto, b.dataset.unidades)] || 0;
        if (cuantos) dice.push(cuantos + " en x" + b.dataset.unidades);
      });
      otro.textContent = dice.join(" · ");
      otro.hidden = dice.length === 0;
    });
  }

  // un solo escucha para toda la lista: los contadores son muchos y se pintan
  // todo el tiempo, así que colgarle uno a cada botón sería trabajo repetido
  lista.addEventListener("click", function (e) {
    var boton = e.target.closest(".contador button");
    if (!boton) return;
    mover(boton.closest(".contador").dataset.clave, "menos" in boton.dataset ? -1 : 1);
  });

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

  // el tamaño del pack cambia a qué clave le suma cada contador de gusto, así
  // que después de elegirlo hay que volver a pintarlos
  if (tam) {
    tam.addEventListener("click", function (e) {
      var boton = e.target.closest("button[data-unidades]");
      if (!boton) return;
      tam.querySelectorAll("button").forEach(function (b) {
        b.setAttribute("aria-pressed", String(b === boton));
      });
      pintar();
    });
  }

  pintar();
})();
