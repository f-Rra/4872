// La entrada de la pantalla dura lo que dura y después se corta. Si la clase
// quedara puesta, cada cambio de solapa volvería a dibujar los renglones de esa
// familia: una familia escondida está en display:none, y al mostrarla el
// navegador le arranca las animaciones de cero.
(function () {
  var marco = document.querySelector(".dibujando");
  if (!marco) return;
  // 1,2 s es un poco más que la más tardía de todas: la línea de arriba del
  // botón del inicio, que empieza a los 0,56 y tarda medio segundo
  window.setTimeout(function () { marco.classList.remove("dibujando"); }, 1200);
})();

// Las solapas cambian qué familia se ve. Las tres listas ya vienen en el HTML:
// se muestran y se esconden, no se piden de nuevo al servidor, así que cambiar
// de solapa es instantáneo y funciona con la carta ya cargada.
(function () {
  var solapas = document.getElementById("solapas");
  var lista = document.getElementById("lista");
  if (!solapas || !lista) return;

  var tam = document.getElementById("tam");

  // El pedido vive en el navegador y en ningún otro lado. Un pedido a medio
  // armar no es un pedido: no tiene por qué ocupar una fila en la base ni
  // avisarle nada a nadie. Pero sí tiene que sobrevivir a una recarga o a un
  // «volver atrás», que en un teléfono pasa todo el tiempo.
  var LLAVE = "4872.pedido";
  // una sola vez por dispositivo: mostrarlo en cada visita cansa a la segunda
  // semana, y el que ya sabe que los ingredientes se tocan no necesita que se
  // lo repitan todos los martes
  var LLAVE_PISTA = "4872.pista";
  var carrito = {};

  // localStorage tira excepción en modo privado y con las cookies bloqueadas.
  // Si falla, el pedido sigue funcionando: se pierde al recargar, nada más.
  function guardar() {
    try { localStorage.setItem(LLAVE, JSON.stringify(carrito)); } catch (e) { /* sin guardar */ }
  }

  function leer() {
    try {
      var guardado = JSON.parse(localStorage.getItem(LLAVE) || "{}");
      return guardado && typeof guardado === "object" && !Array.isArray(guardado) ? guardado : {};
    } catch (e) { return {}; }
  }

  // La clave de una pizza o una focaccia la escribe el servidor con el id del
  // producto. La de un pack no se puede escribir de antemano: el mismo gusto es
  // una cosa distinta en x6 que en x12, y el tamaño se elige en la pantalla.
  function clavePack(idGusto, unidades) { return "e" + idGusto + "x" + unidades; }

  // La combinacion es parte de lo pedido: dos margaritas con todo y una sin
  // albahaca son dos cosas distintas, con su propio contador. Por eso la clave
  // del renglon lleva pegados los ingredientes sacados.
  function claveRenglon(li) {
    var sin = [];
    li.querySelectorAll(".ing[aria-pressed='false']").forEach(function (b) {
      sin.push(Number(b.dataset.ing));
    });
    sin.sort(function (a, b) { return a - b; });
    return li.dataset.base + (sin.length ? "|" + sin.join(",") : "");
  }

  // El precio y la familia no se guardan en el carrito: se sacan de la clave y
  // del renglón que la generó. Así el carrito sigue siendo un mapa de claves a
  // cantidades, que es lo único que hay que guardar entre recargas.
  function precioDe(clave) {
    if (clave.charAt(0) === "e") {
      var b = tam && tam.querySelector("button[data-unidades='" + clave.split("x")[1] + "']");
      return b ? parseFloat(b.dataset.precio) : 0;
    }
    var li = lista.querySelector("li[data-base='" + clave.split("|")[0] + "']");
    return li ? parseFloat(li.dataset.precio) : 0;
  }

  function familiaDe(clave) {
    if (clave.charAt(0) === "e") return "pack";
    var li = lista.querySelector("li[data-base='" + clave.split("|")[0] + "']");
    return li && li.closest("[data-familia]").dataset.familia === "focaccias" ? "focaccia" : "pizza";
  }

  // la focaccia cuenta como pieza y no como pizza: son dos cosas distintas
  // aunque se pidan igual
  var NOMBRES = {
    pizza: ["pizza", "pizzas"],
    focaccia: ["focaccia", "focaccias"],
    pack: ["pack", "packs"]
  };

  function barra() {
    var pedido = document.getElementById("pedido");
    if (!pedido) return;

    var total = 0, cuantas = { pizza: 0, focaccia: 0, pack: 0 };
    Object.keys(carrito).forEach(function (k) {
      cuantas[familiaDe(k)] += carrito[k];
      total += carrito[k] * precioDe(k);
    });

    var partes = [];
    ["pizza", "focaccia", "pack"].forEach(function (f) {
      if (cuantas[f]) partes.push(cuantas[f] + " " + NOMBRES[f][cuantas[f] === 1 ? 0 : 1]);
    });

    // Con una sola familia el desglose entra siempre. Con dos ya no entra en un
    // telefono de 360 y con tres no entra en ninguno: al rotulo le quedan 118 px
    // y «3 pizzas · 2 focaccias · 2 packs» pide 192, asi que se cortaba por los
    // dos lados. Cuando hay mas de una se dice cuantos productos son; el detalle
    // esta una pantalla mas adelante, que es la que existe para mirarlo.
    //
    // Siempre plural: si hay dos familias hay dos productos por lo menos.
    var piezas = cuantas.pizza + cuantas.focaccia + cuantas.pack;
    var rotulo = partes.length > 1 ? piezas + " productos" : partes.join("");

    document.getElementById("cuenta-n").textContent = rotulo || "0 productos";
    document.getElementById("cuenta-t").textContent = "$" + total.toLocaleString("es-AR");
    pedido.classList.toggle("visible", partes.length > 0);
  }

  function tamanoElegido() {
    var b = tam && tam.querySelector("button[aria-pressed='true']");
    return b ? b.dataset.unidades : null;
  }

  // Hay que sacar la clase y forzar un reflujo antes de volver a ponerla: si se
  // pone sobre una animación que ya está corriendo, el navegador no la reinicia
  // y el segundo toque seguido no late.
  function latir(elemento) {
    if (!elemento) return;
    elemento.classList.remove("late");
    void elemento.offsetWidth;
    elemento.classList.add("late");
  }

  function mover(clave, paso) {
    var cuantos = (carrito[clave] || 0) + paso;
    // el cero se borra en vez de guardarse: el carrito es lo que se pidió, y un
    // producto en cero no se pidió
    if (cuantos > 0) { carrito[clave] = cuantos; } else { delete carrito[clave]; }
    guardar();
    pintar();

    var contador = lista.querySelector('.contador[data-clave="' + clave + '"] .n');
    latir(contador);
    latir(document.getElementById("cuenta-t"));
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
    lista.querySelectorAll("ol.carta li[data-base]").forEach(function (li) {
      var contador = li.querySelector(".contador");
      if (!contador) return;
      var clave = claveRenglon(li);
      pintarContador(contador, clave);

      // Cuantas lleva del mismo producto con otra combinacion. Sin esto, tachar
      // un ingrediente pone el contador en cero y parece que se borro el pedido.
      var otras = 0, todasConTodo = true;
      Object.keys(carrito).forEach(function (k) {
        if (k === clave) return;
        if (k !== li.dataset.base && k.indexOf(li.dataset.base + "|") !== 0) return;
        otras += carrito[k];
        if (k !== li.dataset.base) todasConTodo = false;
      });
      var otro = li.querySelector(".otro");
      // pizzas y focaccias son las dos femeninas, asi que alcanza con una forma
      otro.textContent = otras
        ? otras + (todasConTodo ? " con todo" : (otras === 1 ? " modificada" : " modificadas"))
        : "";
      otro.hidden = otras === 0;
    });

    var unidades = tamanoElegido();
    if (unidades) pintarGustos(unidades);
    barra();
  }

  function pintarGustos(unidades) {
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
        if (cuantos) {
          dice.push(cuantos + (cuantos === 1 ? " pack x" : " packs x") + b.dataset.unidades);
        }
      });
      otro.textContent = dice.join(" · ");
      otro.hidden = dice.length === 0;
    });
  }


  // La carta pudo cambiar entre una visita y la otra: un producto que ya no
  // está, un gusto dado de baja, un ingrediente que salió de la receta. Lo
  // guardado se contrasta contra lo que hay en pantalla y lo que no existe se
  // tira. Sin esto, una clave vieja sumaría al total un producto sin precio.
  function existe(clave) {
    if (clave.charAt(0) === "e") {
      var partes = clave.slice(1).split("x");
      return !!(lista.querySelector("ol.gustos li[data-gusto='" + partes[0] + "']") &&
                tam && tam.querySelector("button[data-unidades='" + partes[1] + "']"));
    }
    var trozos = clave.split("|");
    var li = lista.querySelector("li[data-base='" + trozos[0] + "']");
    if (!li) return false;
    if (!trozos[1]) return true;
    return trozos[1].split(",").every(function (id) {
      return li.querySelector(".ing[data-ing='" + id + "']");
    });
  }

  function recuperar() {
    var guardado = leer();
    Object.keys(guardado).forEach(function (k) {
      var cuantos = Math.floor(Number(guardado[k]));
      if (cuantos > 0 && existe(k)) carrito[k] = cuantos;
    });
    guardar();
  }

  // Si a un producto le quedó una sola combinación, se le marcan los
  // ingredientes como estaban. Sin esto la pantalla vuelve con el contador en
  // cero y un «2 modificadas» al costado, que se lee como que se perdió algo.
  // Con dos combinaciones o más no hay una sola respuesta, así que no se toca.
  function recuperarIngredientes() {
    lista.querySelectorAll("ol.carta li[data-base]").forEach(function (li) {
      var suyas = Object.keys(carrito).filter(function (k) {
        return k === li.dataset.base || k.indexOf(li.dataset.base + "|") === 0;
      });
      if (suyas.length !== 1) return;
      var sin = suyas[0].split("|")[1];
      var sacados = sin ? sin.split(",") : [];
      li.querySelectorAll(".ing[data-ing]").forEach(function (b) {
        b.setAttribute("aria-pressed", String(sacados.indexOf(b.dataset.ing) < 0));
      });
    });
  }

  // un solo escucha para toda la lista: los contadores son muchos y se pintan
  // todo el tiempo, así que colgarle uno a cada botón sería trabajo repetido
  lista.addEventListener("click", function (e) {
    var boton = e.target.closest(".contador button");
    if (boton) {
      mover(boton.closest(".contador").dataset.clave, "menos" in boton.dataset ? -1 : 1);
      return;
    }

    // tachar o destachar un ingrediente cambia la clave del renglon, asi que el
    // contador tiene que volver a leer cuantas hay de esa combinacion
    var chip = e.target.closest(".ing:not(.fijo)");
    if (!chip || chip.disabled) return;
    chip.setAttribute("aria-pressed", chip.getAttribute("aria-pressed") === "true" ? "false" : "true");
    irsePista();   // ya entendió: el cartel no hace falta más
    pintar();
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

    // en empanadas no hay ingredientes que sacar, así que el cartel sobra
    if (boton.dataset.solapa === "empanadas") irsePista();

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

  // ---------- el cartel de los ingredientes ----------
  // No se borra de golpe: se apaga y el hueco se cierra, así la lista sube sola
  // en vez de saltar. Un alto "auto" no transiciona, por eso se fija el que
  // tiene antes de llevarlo a cero.
  var pista = document.getElementById("pista");
  var relojPista = 0;

  function irsePista() {
    if (!pista || pista.hidden) return;
    window.clearTimeout(relojPista);
    try { localStorage.setItem(LLAVE_PISTA, "1"); } catch (e) { /* sin guardar */ }

    pista.style.height = pista.scrollHeight + "px";
    void pista.offsetWidth;
    pista.classList.add("ido");
    pista.style.height = "0px";
    window.setTimeout(function () { if (pista) pista.hidden = true; }, 460);
  }

  function asomarPista() {
    if (!pista) return;
    var visto = true;
    try { visto = localStorage.getItem(LLAVE_PISTA) === "1"; } catch (e) { /* sin guardar */ }
    // sin renglones que tocar no hay nada que explicar
    var hay = lista.querySelector("[data-familia]:not([hidden]) .ing:not(.fijo)");
    if (visto || !hay) return;

    pista.hidden = false;
    relojPista = window.setTimeout(irsePista, 4000);
  }

  recuperar();
  recuperarIngredientes();
  pintar();
  asomarPista();
})();
