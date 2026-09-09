// El resumen del pedido. Lo arma acá y no el servidor porque el pedido vive en
// el navegador hasta que se confirma: mandarlo para que lo devuelva escrito
// sería un viaje de ida y vuelta para mostrar algo que ya está en la máquina.
(function () {
  var items = document.getElementById("items");
  var dato = document.getElementById("carta");
  if (!items || !dato) return;

  // la misma llave que usa tienda.js para guardarlo
  var LLAVE = "4872.pedido";
  // Los datos del cliente se guardan aparte y por el mismo motivo que el
  // pedido: desde acá se vuelve a la carta a sumar algo, y perder lo tipeado
  // en ese viaje es la forma más fácil de que alguien abandone el pedido.
  var LLAVE_DATOS = "4872.datos";
  var CAMPOS = ["nom", "dir", "wa"];
  var CARTA = JSON.parse(dato.textContent);

  function plata(n) { return "$" + n.toLocaleString("es-AR"); }

  function escapar(t) {
    return String(t).replace(/[&<>"]/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c];
    });
  }

  function leer() {
    try {
      var guardado = JSON.parse(localStorage.getItem(LLAVE) || "{}");
      return guardado && typeof guardado === "object" && !Array.isArray(guardado) ? guardado : {};
    } catch (e) { return {}; }
  }

  // Una clave ya es un renglón del resumen: lleva el producto y lo que se le
  // sacó pegados. Dos margaritas iguales comparten clave y son un renglón; una
  // sin albahaca tiene otra clave y es otro. No hay nada que agrupar acá, el
  // agrupado lo hizo la clave cuando se armó el pedido.
  function renglon(clave, cuantos) {
    if (clave.charAt(0) === "e") {
      var partes = clave.slice(1).split("x");
      var gusto = CARTA.Gustos[partes[0]];
      var precio = CARTA.Packs[partes[1]];
      if (gusto === undefined || precio === undefined) return null;
      return { nombre: gusto + " · x" + partes[1], sin: [], cuantos: cuantos,
               precio: precio, orden: 10000 + Number(partes[0]) };
    }

    var trozos = clave.split("|");
    var producto = CARTA.Productos[trozos[0].slice(1)];
    if (!producto) return null;

    var sin = (trozos[1] ? trozos[1].split(",") : [])
      .map(function (id) { return CARTA.Ingredientes[id]; })
      .filter(Boolean);

    return { nombre: producto.Nombre, sin: sin, cuantos: cuantos,
             precio: producto.Precio, orden: producto.Orden };
  }

  function pintar() {
    var carrito = leer();
    var filas = Object.keys(carrito)
      .map(function (k) { return renglon(k, carrito[k]); })
      .filter(Boolean)
      // en el orden de la carta y no en el que se fueron agregando: el cliente
      // lo va a comparar con la pantalla anterior
      .sort(function (a, b) { return a.orden - b.orden || a.sin.length - b.sin.length; });

    if (!filas.length) {
      items.innerHTML = '<li><p class="item-nombre vacio-pedido">Sumá algo en la carta primero.</p></li>';
      document.getElementById("total").textContent = plata(0);
      return;
    }

    items.innerHTML = filas.map(function (f) {
      var sin = f.sin.length
        ? '<span class="sin">sin ' + escapar(f.sin.join(", ")) + "</span>"
        : "";
      return '<li><span class="cant">' + f.cuantos + "×</span>" +
        '<p class="item-nombre">' + escapar(f.nombre) + sin + "</p>" +
        '<p class="item-plata">' + plata(f.cuantos * f.precio) + "</p></li>";
    }).join("");

    document.getElementById("total").textContent = plata(
      filas.reduce(function (suma, f) { return suma + f.cuantos * f.precio; }, 0));
  }

  // ---------- los datos del cliente ----------

  function leerDatos() {
    try {
      var guardado = JSON.parse(localStorage.getItem(LLAVE_DATOS) || "{}");
      return guardado && typeof guardado === "object" ? guardado : {};
    } catch (e) { return {}; }
  }

  function guardarDatos() {
    var datos = {};
    CAMPOS.forEach(function (c) { datos[c] = document.getElementById(c).value; });
    try { localStorage.setItem(LLAVE_DATOS, JSON.stringify(datos)); } catch (e) { /* sin guardar */ }
  }

  // La regla sale de la maqueta. El teléfono se mide en dígitos y no en
  // caracteres, porque cada uno lo escribe como quiere: con guiones, con
  // paréntesis, con el 15 adelante o sin nada.
  function revisar() {
    var valor = {};
    CAMPOS.forEach(function (c) { valor[c] = document.getElementById(c).value; });
    document.getElementById("confirmar").disabled =
      valor.nom.trim().length < 2 ||
      valor.dir.trim().length < 5 ||
      valor.wa.replace(/\D/g, "").length < 8 ||
      !Object.keys(leer()).length;
  }

  function engancharDatos() {
    var guardado = leerDatos();
    CAMPOS.forEach(function (c) {
      var campo = document.getElementById(c);
      if (!campo) return;
      if (typeof guardado[c] === "string") campo.value = guardado[c];
      campo.addEventListener("input", function () { guardarDatos(); revisar(); });
    });
    revisar();
  }

  pintar();
  engancharDatos();
})();
