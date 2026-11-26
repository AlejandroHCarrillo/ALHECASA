# ALHECASA

Plataforma web para la gestión de compra, venta y renta de bienes raíces.

Este repositorio contiene la **base de la aplicación**: estructura, autenticación con JWT, aislamiento por inmobiliaria y un CRUD completo de **Usuarios**.

| Carpeta | Contenido |
|---|---|
| `api/` | Backend en .NET 11 con Clean Architecture (Domain, Application, Infrastructure, Api) |
| `web/` | Frontend en Angular 22 (componentes standalone), Tailwind CSS, DaisyUI y Font Awesome Free |
| `documentacion/` | Esta documentación |

Documentos relacionados: [0-arranque.md](0-arranque.md) (arranque rápido con el frontend en el puerto 4221) · [arquitectura.md](arquitectura.md) · [api.md](api.md) · [estrategia.md](estrategia.md) (estrategia unificada y roadmap)

---

## Requisitos

| Herramienta | Versión verificada | Notas |
|---|---|---|
| .NET SDK | 11.0.100 | Fijado en `api/global.json` |
| Node.js | 24.15.0 | Angular 22 requiere `^22.22.3`, `^24.15.0` o `>=26` |
| npm | 11.x | |
| SQL Server | LocalDB (`MSSQLLocalDB`) | Se instala con Visual Studio o con SQL Server Express |
| EF Core CLI | 11.0.0 | Se restaura como herramienta local (`dotnet tool restore`) |

Para comprobar LocalDB: `sqllocaldb info MSSQLLocalDB`. La instancia se crea y arranca sola al conectarse.

### Versiones de las dependencias principales

- Backend: EF Core 11.0.0 (`Microsoft.EntityFrameworkCore.SqlServer`), `Microsoft.AspNetCore.Authentication.JwtBearer` 11.0.0, FluentValidation 12.2.0, Scalar.AspNetCore 3.2.0.
- Frontend: Angular 22.3.0, Tailwind CSS 4.3.5, DaisyUI 5.8.2, Font Awesome Free 7.3.1.

---

## Instalación y ejecución

Todos los comandos son para PowerShell desde la raíz del repositorio.

### 1. Backend

```powershell
cd api

# Herramientas locales (dotnet-ef 11.0.0)
dotnet tool restore

# Secretos de desarrollo (se guardan fuera del repositorio con user-secrets)
cd src/Alhecasa.Api
dotnet user-secrets set "Jwt:SigningKey" "<cadena-aleatoria-de-64-o-mas-caracteres>"
dotnet user-secrets set "Seed:PlatformAdmin:Password" "<contraseña-del-administrador>"
dotnet user-secrets set "Seed:DemoAgency:Admin:Password" "<contraseña-del-admin-de-la-inmobiliaria-demo>"
cd ../..

# Compilar
dotnet build Alhecasa.sln

# Aplicar migraciones en LocalDB (crea la base "Alhecasa")
dotnet ef database update --project src/Alhecasa.Infrastructure --startup-project src/Alhecasa.Api

# Ejecutar la API
dotnet run --project src/Alhecasa.Api --launch-profile http
```

- La API queda en **http://localhost:5156**.
- Documentación interactiva (solo en Desarrollo): **http://localhost:5156/scalar/v1**. El documento OpenAPI está en `/openapi/v1.json`.
- Comprobación de salud: `GET http://localhost:5156/health`.

**Reglas de los secretos**

- `Jwt:SigningKey` debe tener **al menos 32 caracteres**. La API no arranca si falta o es corta (se valida al iniciar).
- Las contraseñas deben cumplir la política: mínimo 10 caracteres, con mayúscula, minúscula y número.
- Nunca se escriben en `appsettings*.json`. En otros entornos se pasan por variables de entorno, por ejemplo `Jwt__SigningKey`.

**Datos iniciales (solo en Desarrollo)**

Al arrancar en Desarrollo, la API aplica las migraciones pendientes y crea, si no existen:

| Usuario | Rol | Inmobiliaria |
|---|---|---|
| `admin@alhecasa.local` | `PlatformAdmin` | Ninguna |
| `agency@demo.alhecasa.local` | `AgencyAdmin` | Demo Real Estate |

Sus contraseñas son las que definiste en user-secrets. Se pueden cambiar los datos en la sección `Seed` de `appsettings.Development.json`.

Este comportamiento lo controlan `Database:MigrateOnStartup` y `Database:SeedOnStartup`, que valen `false` en `appsettings.json` (producción) y `true` en `appsettings.Development.json`.

### 2. Frontend

```powershell
cd web
npm install
npm start
```

- La aplicación queda en **http://localhost:4200**.
- En desarrollo consume la API en `http://localhost:5156/api` (`src/environments/environment.development.ts`).
- La API debe estar en ejecución antes de iniciar sesión.

### 3. Primer recorrido

1. Abre http://localhost:4200 y entra con `admin@alhecasa.local`.
2. Ve a **Usuarios** en el menú lateral.
3. Usa **Agregar** para crear un usuario, haz clic en una fila para ver el detalle, y usa los botones de editar y eliminar.

---

## Compilar y probar

```powershell
# Backend
cd api
dotnet build Alhecasa.sln
dotnet test --solution Alhecasa.sln   # pruebas unitarias y de integración (xUnit v3)

# Frontend
cd web
npm run build      # compilación de producción en web/dist/web
npm test           # pruebas unitarias (Vitest)
```

> Estado de las pruebas: el frontend tiene pruebas unitarias del núcleo de seguridad (sesión, interceptor y manejo de errores). El backend tiene pruebas unitarias y de integración (aislamiento por inmobiliaria, permisos y revalidación del token); detalle y salvedades en [arquitectura.md](arquitectura.md#pruebas-del-backend). Para ejecutarlas hay que **detener la API** si está en marcha, porque bloquea los archivos compilados.

---

## Migraciones de base de datos

```powershell
cd api

# Crear una migración nueva
dotnet ef migrations add NombreDeLaMigracion --project src/Alhecasa.Infrastructure --startup-project src/Alhecasa.Api --output-dir Persistence/Migrations

# Aplicarla
dotnet ef database update --project src/Alhecasa.Infrastructure --startup-project src/Alhecasa.Api

# Deshacer la última (si aún no se aplicó)
dotnet ef migrations remove --project src/Alhecasa.Infrastructure --startup-project src/Alhecasa.Api
```

La cadena de conexión está en `api/src/Alhecasa.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`) y usa autenticación de Windows contra `(localdb)\MSSQLLocalDB`. No contiene credenciales. Para usar SQL Server Express u otra instancia, cámbiala en user-secrets o con la variable `ConnectionStrings__DefaultConnection`.

Para consultar la base con `sqlcmd`, usa la opción `-I` (QUOTED_IDENTIFIER), necesaria por el índice filtrado de `Users`:

```powershell
sqlcmd -I -S "(localdb)\MSSQLLocalDB" -d Alhecasa -Q "SELECT Email, Role FROM Users"
```

---

## Configuración

| Clave | Dónde | Descripción |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `appsettings.json` | Conexión a SQL Server |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` | `appsettings.json` | Parámetros del token (60 minutos por defecto) |
| `Jwt:SigningKey` | **user-secrets / entorno** | Clave de firma HMAC (obligatoria, mínimo 32 caracteres) |
| `Cors:AllowedOrigins` | `appsettings.json` | Orígenes permitidos (por defecto `http://localhost:4200`) |
| `Payments:AllowSimulated` | variable de entorno | Obligatoria fuera de Desarrollo (`true`): la pasarela de pagos es una simulación que no cobra |
| `Database:MigrateOnStartup` | `appsettings*.json` | Aplica migraciones al iniciar |
| `Database:SeedOnStartup` | `appsettings*.json` | Crea los datos iniciales al iniciar |
| `Seed:*` | `appsettings.Development.json` y user-secrets | Datos del administrador y la inmobiliaria demo |

Las variables de entorno usan `__` como separador: `Cors__AllowedOrigins__1=http://localhost:4201`.

---

## Solución de problemas

| Síntoma | Causa y solución |
|---|---|
| La API termina al iniciar con un error de validación de opciones `Jwt` | Falta `Jwt:SigningKey` o tiene menos de 32 caracteres. Configúrala con `dotnet user-secrets set`. |
| `Port 4200 is already in use` | Otra aplicación usa ese puerto. Deténla, o usa `npm start -- --port 4201` **y** agrega ese origen en `Cors:AllowedOrigins` (por ejemplo, con `Cors__AllowedOrigins__1`). |
| El navegador muestra errores de CORS | El origen del frontend no está en `Cors:AllowedOrigins`, o la API no está en ejecución. |
| Login responde "Correo o contraseña incorrectos." | Credenciales inválidas, cuenta inactiva o inmobiliaria inactiva. La API no distingue el motivo a propósito. |
| Login responde "Demasiados intentos" | Límite de 10 intentos por minuto por IP en el login. Espera un minuto. |
| `dotnet ef` no se reconoce | Ejecuta `dotnet tool restore` dentro de `api/`. |
| No se conecta a LocalDB | Verifica con `sqllocaldb info MSSQLLocalDB`; si no existe, créala con `sqllocaldb create MSSQLLocalDB`. |
