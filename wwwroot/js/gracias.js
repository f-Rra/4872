// El saludo lo escribe el navegador y no el servidor.
//
// El número de pedido es correlativo y está en la dirección, así que cualquiera
// puede ir probando /gracias/1, /gracias/2. Si el nombre viniera de la base,
// eso sería una lista de clientes. Acá sale de lo que tipeó esta misma persona
// en esta misma máquina: en cualquier otra, la pantalla dice «Gracias.» y listo.
(function () {
  var saludo = document.getElementById("saludo");
  if (!saludo) return;

  // la misma llave donde el checkout guarda lo tipeado
  var LLAVE_DATOS = "4872.datos";

  var nombre = "";
  try {
    var guardado = JSON.parse(localStorage.getItem(LLAVE_DATOS) || "{}");
    // solo el primer nombre: «Gracias, Facundo» y no «Gracias, Facundo Rivarola»
    if (guardado && typeof guardado.nom === "string") {
      nombre = guardado.nom.trim().split(" ")[0];
    }
  } catch (e) { /* sin nombre, queda el texto que ya estaba */ }

  if (!nombre) return;

  saludo.textContent = "Gracias, " + nombre + ". Te escribimos por WhatsApp para coordinar la entrega.";
})();
