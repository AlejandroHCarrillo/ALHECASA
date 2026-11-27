// Desarrollo: la API es la del mismo equipo que sirve la página (puerto 5156, perfil "http" de launchSettings).
// Se calcula con el host con que se abrió la página: con localhost apunta a localhost y, al abrirla desde otro
// dispositivo de la red (http://<IP-del-equipo>:4221), apunta a la API de ese mismo equipo.
// La API debe autorizar ese origen en Cors:AllowedOrigins (ver documentacion/0-arranque.md).
export const environment = {
  production: false,
  apiUrl: `${location.protocol}//${location.hostname}:5156/api`,
};
