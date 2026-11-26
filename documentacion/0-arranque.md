# Arranque rápido (frontend en el puerto 4221)

Instrucciones para levantar la API y el frontend con el frontend en **http://localhost:4221**.

| Servicio | URL | Puerto |
|---|---|---|
| API (.NET) | http://localhost:5156 | 5156 |
| Frontend (Angular) | http://localhost:4221 | 4221 |

> La API y el frontend son dos procesos distintos y no pueden compartir puerto. El puerto 4221 es el del **frontend**; la API sigue en el 5156.

Para la instalación completa (requisitos, secretos, migraciones), consulta [README.md](README.md). Para conocer hacia dónde evoluciona el proyecto, consulta [estrategia.md](estrategia.md). Esta guía asume que ya la hiciste al menos una vez: `dotnet tool restore`, los secretos con `dotnet user-secrets` y `npm install`.

## Por qué hace falta un paso extra: CORS

La API solo acepta peticiones del navegador desde los orígenes de `Cors:AllowedOrigins`, que por defecto es `http://localhost:4200`. Si abres el frontend en el 4221 sin autorizarlo, el navegador bloquea las llamadas a la API y el login falla con un error de CORS.

Se autoriza el origen del 4221 con una variable de entorno al iniciar la API. No hace falta editar ningún archivo.

## 1. Arrancar la API

En una terminal de PowerShell:

```powershell
cd C:\Repos\ALHECASA\api
$env:Cors__AllowedOrigins__1 = "http://localhost:4221"
dotnet run --project src/Alhecasa.Api --launch-profile http
```

- La variable `Cors__AllowedOrigins__1` **agrega** el origen del 4221 a la lista (el 4200 sigue permitido). Solo vive en esa terminal.
- En Desarrollo, la API aplica las migraciones y crea los usuarios iniciales al arrancar.
- Comprueba que responde: abre http://localhost:5156/health (debe mostrar `Healthy`).
- Documentación interactiva: http://localhost:5156/scalar/v1

## 2. Arrancar el frontend en el puerto 4221

En **otra** terminal:

```powershell
cd C:\Repos\ALHECASA\web
npm start -- --port 4221
```

Abre http://localhost:4221 e inicia sesión con `admin@alhecasa.local` y la contraseña que definiste en user-secrets (`Seed:PlatformAdmin:Password`).

El frontend en desarrollo apunta a la API en `http://localhost:5156/api` (`web/src/environments/environment.development.ts`), así que no necesita cambios por usar otro puerto.

### Catálogo de ubicaciones y mapa del sitio

- La migración `AddLocationCatalog` crea las tablas de estados, ciudades y colonias. En Desarrollo se aplica sola al arrancar la API (`Database:MigrateOnStartup`), que además **liga las propiedades que ya existían** al catálogo; si la base no se migra sola, aplícala con `dotnet ef database update` (ver [README.md](README.md#migraciones-de-base-de-datos)). Una API iniciada antes de este cambio necesita reiniciarse para tener los endpoints nuevos.
- Las páginas por zona (`/venta/<ciudad>/<colonia>`, `/renta/<ciudad>`) salen de ese catálogo. Con los datos de demostración, prueba por ejemplo `http://localhost:4221/venta/` más el identificador de una ciudad (se ve en los enlaces de «Explora por zona» de la portada).
- El mapa del sitio está en `GET /api/public/sitemap.xml` y usa `Seo:SiteUrl` para las direcciones. Para que sean las del puerto o la IP con que abres la web, inicia la API con `$env:Seo__SiteUrl = "http://localhost:4221"` (o `http://<IP>:4221` desde la red). En producción, ponla con tu dominio, descomenta la línea `Sitemap:` de `web/public/robots.txt` y envía el mapa a Google Search Console.

### Catálogo de demostración (Goldita y Osito)

Con la clave `Seed:DemoCatalog:Password` configurada, la API crea al arrancar dos inmobiliarias ficticias con su personal y 10 anuncios publicados (5 cada una, 2 fotos por anuncio). Sin la clave no se siembra nada. Es idempotente: no duplica ni resucita lo que se haya borrado.

```powershell
cd C:\Repos\ALHECASA\api
dotnet user-secrets set "Seed:DemoCatalog:Password" "<la contraseña de los usuarios demo>" --project src/Alhecasa.Api
```

| Inmobiliaria | Administrador | Supervisor | Vendedor |
|---|---|---|---|
| Goldita Bienes Raíces | `admin@goldita.local` | `supervisor@goldita.local` | `vendedor@goldita.local` |
| Osito Real State | `admin@osito.local` | `supervisor@osito.local` | `vendedor@osito.local` |

- Los nombres, correos `.local`, direcciones y teléfonos son ficticios; las coordenadas son aproximadas de la zona.
- Las fotos están en `api/src/Alhecasa.Api/SeedData/catalog-images/01…10` y **fueron generadas con IA**, no son de inmuebles reales. Cámbialas por fotos propias antes de mostrar el sistema a terceros.
- El código está en `Infrastructure/Persistence/Seeding/DemoCatalog.cs`.

## Acceso desde otros dispositivos de la red

Para abrir la aplicación desde un celular, una tableta u otra computadora de la misma red (Wi-Fi o LAN), la API y el frontend deben escuchar en **todas las interfaces** (`0.0.0.0`) y no solo en `localhost`. Los puertos son los mismos: **4221** (frontend) y **5156** (API).

| Servicio | Desde esta PC | Desde otro dispositivo |
|---|---|---|
| Frontend (Angular) | http://localhost:4221 | `http://<IP-de-esta-PC>:4221` |
| API (.NET) | http://localhost:5156 | `http://<IP-de-esta-PC>:5156` |

### a) Averiguar la IP de esta PC

```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254*' } | Select-Object InterfaceAlias, IPAddress
```

En los ejemplos se usa `192.168.1.69`; sustitúyela por la tuya. Esa IP la asigna el router (DHCP) y puede cambiar al reconectarte: si deja de funcionar, vuelve a consultarla. `ng serve` también la imprime como `Network:` al arrancar.

### b) Arrancar la API en todas las interfaces

En una terminal de PowerShell (sustituye la IP):

```powershell
cd C:\Repos\ALHECASA\api
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Cors__AllowedOrigins__1 = "http://localhost:4221"
$env:Cors__AllowedOrigins__2 = "http://192.168.1.69:4221"
dotnet run --project src/Alhecasa.Api --no-launch-profile --urls http://0.0.0.0:5156
```

- `--urls http://0.0.0.0:5156` hace que escuche en todas las interfaces. Con `--no-launch-profile` se ignora el perfil `http`, que solo escucha en `localhost`; por eso hay que poner `ASPNETCORE_ENVIRONMENT` a mano (migraciones y usuarios iniciales al arrancar, Scalar).
- Cada origen desde el que se abrirá el frontend debe estar en `Cors:AllowedOrigins` (el 4200 de `appsettings.json` sigue permitido). Si el origen no está, el login falla con un error de CORS. Para autorizar varias IPs, agrega `Cors__AllowedOrigins__3`, `__4`, etc.
- Las variables solo viven en esa terminal.

### c) Arrancar el frontend en todas las interfaces

En **otra** terminal:

```powershell
cd C:\Repos\ALHECASA\web
npm start -- --host 0.0.0.0 --port 4221
```

Al terminar de compilar muestra `Local: http://localhost:4221/` y `Network: http://192.168.1.69:4221/`. Abre la dirección `Network` desde el otro dispositivo.

En desarrollo, el frontend calcula la URL de la API con el host con que se abrió la página (`web/src/environments/environment.development.ts`): si entras por `http://192.168.1.69:4221`, llama a `http://192.168.1.69:5156/api`; si entras por `localhost`, a `localhost`. No hay que cambiar ninguna IP en el código.

### d) Firewall de Windows

Windows bloquea las conexiones entrantes en las redes marcadas como **Públicas**. La primera vez que `node.exe` o la API escuchan en la red, Windows pregunta si permitir el acceso: acepta. Si no apareció o lo negaste, crea las reglas con una terminal de PowerShell **como administrador**:

```powershell
New-NetFirewallRule -DisplayName "ALHECASA Web 4221" -Direction Inbound -Protocol TCP -LocalPort 4221 -Action Allow -Profile Private,Public
New-NetFirewallRule -DisplayName "ALHECASA API 5156" -Direction Inbound -Protocol TCP -LocalPort 5156 -Action Allow -Profile Private,Public
```

Al terminar de trabajar, quítalas:

```powershell
Remove-NetFirewallRule -DisplayName "ALHECASA Web 4221", "ALHECASA API 5156"
```

Lo más seguro es marcar tu red de casa u oficina como **Privada** (Configuración > Red e Internet > propiedades de la red) y crear las reglas solo para el perfil `Private`.

### e) Verificar

Desde esta PC:

```powershell
Invoke-WebRequest http://192.168.1.69:5156/health -UseBasicParsing   # 200 y "Healthy"
Invoke-WebRequest http://192.168.1.69:4221/ -UseBasicParsing         # 200
```

Desde el otro dispositivo (en la misma red), abre `http://192.168.1.69:4221` e inicia sesión. Si ese dispositivo no carga la página, revisa el firewall y que ambos estén en la misma red (algunos routers aíslan los dispositivos Wi-Fi entre sí: "aislamiento de clientes" o "red de invitados").

### Advertencias

- **Solo HTTP.** Las funciones del navegador que exigen un contexto seguro (por ejemplo, geolocalización o cámara) pueden no funcionar al entrar por una IP; sí funcionan en `localhost`.
- **Es un servidor de desarrollo.** Quien esté en tu red puede abrir la aplicación, la API y la documentación Scalar, y probar los usuarios sembrados con las contraseñas de tus user-secrets. Úsalo solo en redes de confianza, nunca en una Wi-Fi pública, y detén los servicios al terminar.
- Para volver al modo local, detén ambos procesos (paso 3) y arranca como en las secciones 1 y 2.

## 3. Detener los servicios

Pulsa `Ctrl + C` en cada terminal.

Si un proceso quedó en segundo plano, localízalo por puerto y detenlo:

```powershell
Get-NetTCPConnection -LocalPort 4221 -State Listen | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
Get-NetTCPConnection -LocalPort 5156 -State Listen | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
```

## Solución de problemas

| Síntoma | Causa y solución |
|---|---|
| El login falla y la consola del navegador muestra un error de CORS | La API se inició sin la variable `Cors__AllowedOrigins__1`, o en otra terminal. Detén la API y vuelve a iniciarla con el paso 1. |
| `Port 4221 is already in use` | Otro proceso usa ese puerto. Averigua cuál con `Get-NetTCPConnection -LocalPort 4221 -State Listen` y deténlo, o elige otro puerto y autorízalo en la API. |
| La página no carga desde otro dispositivo (tiempo de espera agotado) | El firewall bloquea el puerto (red marcada como Pública), los servicios se iniciaron sin `0.0.0.0` o el dispositivo está en otra red. Consulta "Acceso desde otros dispositivos de la red". Comprueba con `Get-NetTCPConnection -LocalPort 4221,5156 -State Listen` que la dirección local sea `0.0.0.0`. |
| Desde otro dispositivo la página carga pero el login falla con un error de CORS | Falta el origen con la IP en `Cors__AllowedOrigins__N` al iniciar la API, o la IP cambió. Reinicia la API con la IP actual. |
| `Invalid host header` al entrar por el nombre del equipo | `ng serve` solo acepta `localhost` y direcciones IP. Entra por la IP o inicia con `npm start -- --host 0.0.0.0 --port 4221 --allowed-hosts <nombre-del-equipo>`. |
| `Port 5156 is already in use` | Ya hay una instancia de la API en marcha. Reutilízala o detenla (ver el paso 3). |
| La API termina al iniciar con un error de opciones `Jwt` | Falta `Jwt:SigningKey` en user-secrets o tiene menos de 32 caracteres. Consulta [README.md](README.md). |
| La API no encuentra la base de datos | Verifica LocalDB con `sqllocaldb info MSSQLLocalDB`. Consulta [README.md](README.md). |

## Hacer permanente el puerto 4221

Si siempre vas a usar el 4221 y no quieres definir la variable cada vez, tienes dos opciones:

1. **Autorizarlo en la configuración de la API:** agrega `"http://localhost:4221"` a `Cors:AllowedOrigins` en `api/src/Alhecasa.Api/appsettings.Development.json`.
2. **Fijar el puerto en el frontend:** agrega `--port 4221` al script `start` de `web/package.json` (`"start": "ng serve --port 4221"`) para poder usar simplemente `npm start`.
