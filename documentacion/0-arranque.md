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
| `Port 5156 is already in use` | Ya hay una instancia de la API en marcha. Reutilízala o detenla (ver el paso 3). |
| La API termina al iniciar con un error de opciones `Jwt` | Falta `Jwt:SigningKey` en user-secrets o tiene menos de 32 caracteres. Consulta [README.md](README.md). |
| La API no encuentra la base de datos | Verifica LocalDB con `sqllocaldb info MSSQLLocalDB`. Consulta [README.md](README.md). |

## Hacer permanente el puerto 4221

Si siempre vas a usar el 4221 y no quieres definir la variable cada vez, tienes dos opciones:

1. **Autorizarlo en la configuración de la API:** agrega `"http://localhost:4221"` a `Cors:AllowedOrigins` en `api/src/Alhecasa.Api/appsettings.Development.json`.
2. **Fijar el puerto en el frontend:** agrega `--port 4221` al script `start` de `web/package.json` (`"start": "ng serve --port 4221"`) para poder usar simplemente `npm start`.
