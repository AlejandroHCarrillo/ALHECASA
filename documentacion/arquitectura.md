# Arquitectura

Este documento describe las capas del sistema, las decisiones tomadas y sus razones. Para instalar y ejecutar, consulta [README.md](README.md); para los endpoints, [api.md](api.md).

## Visión general

```mermaid
flowchart LR
    Web["web/ (Angular 22)"] -- "HTTP + JWT" --> Api
    subgraph api["api/ (.NET 11)"]
        Api["Alhecasa.Api"] --> Infra["Alhecasa.Infrastructure"]
        Api --> App["Alhecasa.Application"]
        Infra --> App
        App --> Domain["Alhecasa.Domain"]
    end
    Infra -- "EF Core 11" --> DB[("SQL Server (LocalDB)")]
```

## Backend: Clean Architecture

Cada capa es un proyecto separado de la solución `api/Alhecasa.sln`. **Las dependencias solo apuntan hacia adentro**: `Api → Infrastructure → Application → Domain`. Domain no depende de ningún proyecto ni paquete.

### Alhecasa.Domain

Reglas de negocio y entidades, sin dependencias externas.

- `BaseEntity`: `Id` (Guid versión 7, generado en código), auditoría (`CreatedAt`, `UpdatedAt`) y borrado lógico (`IsDeleted`, `DeletedAt`).
- `User`: persona con acceso. Tiene rol, inmobiliaria opcional, estado activo y último acceso.
- `Agency`: inmobiliaria. Es el límite de aislamiento de datos (tenant).
- `AgencyKind` (`Company` \| `Individual`): una cuenta gratuita de particular es una `Agency` de tipo `Individual` con un solo usuario. Así sus propiedades y solicitudes quedan aisladas con el mismo filtro de tenant, sin un modelo paralelo (ver «Cuentas gratuitas de particulares»).
- `Property`: propiedad del inventario de una inmobiliaria (tenant). Sus invariantes (título, ciudad y estado obligatorios, precio mayor a cero, sin cuartos ni áreas negativos) se validan en el dominio. Por ahora lleva también precio, operación y estado; la estrategia (F0-07) los separa en una publicación (*Listing*).
- `PropertyImage`: imagen de una propiedad (tenant). Guarda solo metadatos (clave de almacenamiento, tipo, tamaño, orden); el archivo vive en el almacenamiento. `Property` controla la galería (`AddImage`, `RemoveImage`) y su tope de 10.
- `Inquiry`: solicitud de informes de una propiedad (tenant, agregado). Guarda los datos del prospecto, su pregunta, el estado (`New`, `InProgress`, `Closed`) y el título de la propiedad al momento de la solicitud. Controla su historial (`InquiryFollowUp`: respuesta o nota, con el nombre del autor al momento de escribirla) con tope de 200 registros; una respuesta pasa el estado a `InProgress`.
- `PropertyType`, `OperationType`, `PropertyStatus`, `CurrencyCode`: enumeraciones guardadas como texto.
- `UserRole`: `PlatformAdmin`, `AgencyAdmin`, `Supervisor`, `Seller` (vendedor), `Client`. El rol se guarda como texto, así que añadir valores no requirió migración.
- `DomainException`: violación de una regla del dominio.

**Invariante del dominio** (en `User`): `PlatformAdmin` no pertenece a ninguna inmobiliaria; `AgencyAdmin`, `Supervisor` y `Seller` (el personal de la inmobiliaria) siempre pertenecen a una; `Client` puede pertenecer a una o ser un anunciante particular (sin inmobiliaria).

Las entidades tienen constructores y setters privados: el estado solo cambia mediante métodos que validan la regla anterior.

### Alhecasa.Application

Casos de uso y contratos. Depende solo de Domain (y de FluentValidation).

- `Abstractions/`: interfaces que Infrastructure implementa (`IUserRepository`, `IAgencyRepository`, `IUnitOfWork`, `IPasswordHasher`, `IJwtTokenGenerator`) y la interfaz `ICurrentUser`, que Api implementa a partir del JWT.
- `Auth/`, `Users/`, `Agencies/`: DTOs (`record`), validadores y servicios de aplicación (`AuthService`, `UserService`, `AgencyService`).
- `Common/`: `PagedResult<T>` y las excepciones de aplicación (`NotFoundException`, `ConflictException`, `ForbiddenException`, `AuthenticationFailedException`).

Aquí viven las **reglas de autorización** (quién puede hacer qué con los usuarios), de modo que no dependen de HTTP ni de la base de datos.

### Alhecasa.Infrastructure

Detalles técnicos. Depende de Application.

- `Persistence/`: `AlhecasaDbContext` (implementa `IUnitOfWork`), `Configurations/` (una clase por entidad), `Repositories/`, `Migrations/` y `Seeding/`.
- `Identity/`: `Pbkdf2PasswordHasher`, `JwtTokenGenerator` y `JwtOptions`.
- `DependencyInjection.cs`: registra todo con `AddInfrastructure(configuration)`.

### Alhecasa.Api

Capa de entrega. Depende de Application e Infrastructure (esta última solo para registrar servicios en `Program.cs`).

- `Controllers/`: `AuthController`, `UsersController`, `AgenciesController`. Son delgados: delegan en los servicios de aplicación.
- `Middleware/GlobalExceptionHandler`: convierte excepciones en `ProblemDetails`.
- `Services/CurrentUser`: implementa `ICurrentUser` leyendo los claims.
- `OpenApi/`: esquema Bearer en el documento OpenAPI.
- `Program.cs`: composición (autenticación, autorización, CORS, límite de intentos, OpenAPI, salud).

## Decisiones de diseño

### Multiinmobiliaria con filtros globales de EF Core

Cada consulta a `Users` y `Agencies` pasa por un **filtro global** que aplica el borrado lógico y el aislamiento:

- `PlatformAdmin`: ve todo.
- Cualquier otro usuario: ve los registros de su inmobiliaria y, siempre, su propio usuario.
- Sin usuario autenticado: no ve nada.

Los valores del usuario actual vienen de `ICurrentUser`, que el `DbContext` consulta en cada consulta. Esto es **defensa en profundidad**: aun si un servicio olvidara filtrar, la base no devuelve datos de otra inmobiliaria. Un `AgencyAdmin` que pide un usuario de otra inmobiliaria recibe `404`, no `403`, para no revelar que existe.

`IgnoreQueryFilters()` se usa en tres lugares, todos justificados:
1. **Login:** todavía no hay usuario autenticado.
2. **Unicidad del correo:** debe ser global, no por inmobiliaria.
3. **Datos iniciales (seeding).**

Cualquier nuevo uso de `IgnoreQueryFilters()` debe revisarse con cuidado.

### Usuarios y contraseñas propios (sin ASP.NET Identity)

Se eligió una entidad `User` propia para mantener **Domain limpio**; ASP.NET Identity acoplaría Domain e Infrastructure a su modelo. Las contraseñas se guardan con **PBKDF2-HMAC-SHA512** (incluido en .NET): 210 000 iteraciones (recomendación de OWASP), sal aleatoria de 16 bytes por contraseña y comparación en tiempo constante. El formato almacenado es `PBKDF2$SHA512$iteraciones$sal$hash`, lo que permite subir las iteraciones en el futuro sin romper los hashes existentes.

Para no revelar qué correos están registrados, el login responde siempre con el mismo mensaje y, si el usuario no existe, ejecuta una verificación contra un hash ficticio para igualar el tiempo de respuesta.

### Cuentas gratuitas de particulares

Quien vende o renta por su cuenta se registra solo (`POST /api/auth/register`, pantalla `/registro`) y publica sin costo. **Decisión:** en lugar de crear un modelo paralelo para el particular, el registro crea un espacio propio —una `Agency` de tipo `Individual`, con el nombre de la persona— y un único usuario `AgencyAdmin` en él, todo en una transacción. Así propiedades, imágenes, galería, solicitudes de informes y aislamiento entre cuentas funcionan sin cambios, porque ya dependen de `AgencyId`.

Reglas propias de la cuenta gratuita:

- **Tope de propiedades** (`AccountLimits:IndividualMaxProperties`, por defecto 3; cuentan los borradores y no las eliminadas). Se revisa en `PropertyService.CreateAsync` y se responde `422` (`PlanLimitException`, con un mensaje en español listo para mostrar). Las inmobiliarias (`Company`) no tienen tope.
- **No administra usuarios**: `UserService` le niega `/api/users` (`403`), y el menú y la ruta `/usuarios` se ocultan con `isIndividual` (que viaja en `CurrentUserDto` y en `GET /api/agencies`).
- **El servidor fija el rol y el espacio**; el cuerpo del registro no puede elegirlos, así que no hay escalada de privilegios.
- Alta limitada por IP (5 por hora) porque cada alta crea un espacio y un usuario.

Es el puente hacia el modelo `Membership` de la estrategia (particular = usuario estándar sin membresía): cuando exista, estos espacios `Individual` se migran a usuarios sin organización.

**Lo que esto no incluye todavía** (ver «Limitaciones»): verificación de correo, moderación de anuncios y planes de pago.

### JWT

- Algoritmo HMAC-SHA256. Claims: `sub`, `email`, `name`, `role`, `agency_id`, `jti` e `iat`.
- Vigencia de 60 minutos, con tolerancia de reloj de 30 segundos.
- La clave de firma no está en el código ni en `appsettings.json`: se obtiene de user-secrets o variables de entorno, y se valida al iniciar (mínimo 32 caracteres).
- Se desactiva el mapeo automático de claims (`MapInboundClaims = false`) para usar nombres cortos y estables.
- **Revalidación en cada petición:** los permisos se toman de los claims del token, pero tras verificar la firma, `JwtBearerEvents.OnTokenValidated` (`Api/Authentication/SessionRevalidation.cs`) consulta la base mediante `ISessionValidator` y rechaza con `401` si el usuario ya no existe, está desactivado o eliminado, cambió de rol o de inmobiliaria, o su inmobiliaria fue eliminada o desactivada. El costo es una consulta ligera por petición autenticada (proyección de tres columnas con `IgnoreQueryFilters`, porque en ese momento el usuario de la solicitud todavía no está establecido). A cambio, el acceso se revoca en la siguiente petición y no al vencer el token.

### Seguridad por defecto

- **Política de autorización por defecto:** todo endpoint exige autenticación, salvo los marcados con `[AllowAnonymous]` (login, `/health` y el catálogo público `/api/public/properties`).
- **Catálogo público:** es el único lugar donde se lee el inventario sin el filtro por inmobiliaria (`IgnoreQueryFilters`), porque el visitante no tiene una. La visibilidad se define de forma explícita en `PropertyRepository.GetPublishedPagedAsync` (publicada, no eliminada, inmobiliaria activa y no eliminada), el contrato `PublicPropertyDto` omite calle y código postal y solo trae la ubicación redondeada a unos 110 m (`PublicPropertyService.ApproximateCoordinate`), y el endpoint tiene límite de 120 solicitudes por minuto por IP.
- **Roles en los controladores:** `UsersController` y `AgenciesController` exigen `PlatformAdmin` o `AgencyAdmin`.
- **Límite de intentos:** 10 por minuto por IP en el login.
- **Imágenes de propiedades (1 a 10, hasta 5 MB cada una):**
  - *Almacenamiento:* la abstracción `IImageStorage` (Application) se implementa con `LocalImageStorage`, que guarda archivos en `App_Data/images` (opción `Storage:ImagesPath`, fuera de git y fuera de la raíz web, así que nada se sirve directo del disco). Solo acepta las claves que genera el sistema (32 hexadecimales y extensión), de modo que una clave manipulada no puede salir de la carpeta. Para producción se cambia por otra implementación (por ejemplo almacenamiento de objetos) sin tocar el resto.
  - *Validación:* el formato (JPEG, PNG o WebP) se decide por los primeros bytes del archivo, no por el nombre ni por el `Content-Type` declarado. Se valida todo antes de guardar nada y, si algo falla antes de confirmar en la base, se borran los archivos recién guardados.
  - *Galería atómica:* `POST /api/properties/{id}/images` agrega y quita en una sola operación, para poder cambiar una foto con la galería llena.
  - *Regla de publicación:* una propiedad `Published` necesita al menos una imagen. Por eso una propiedad nueva se crea como borrador y se publica después de subir imágenes; el formulario Angular lo encadena (crear, subir, publicar) y, si un paso falla, conserva lo ya guardado para reintentar.
  - *Entrega pública:* `GET /api/images/{id}` no pide token (las etiquetas `<img>` no lo envían). Es un **enlace secreto**: el guid no se puede adivinar, pero quien lo conoce ve la imagen aunque la propiedad sea un borrador. Es una decisión consciente; si se necesitara privacidad estricta de borradores, habría que servir con URL firmadas o con token. Tiene límite de 600 solicitudes por minuto por IP y caché de un año (`immutable`).
  - *Pendiente:* no se redimensionan las imágenes ni se les quitan los metadatos EXIF (por ejemplo la ubicación GPS de la foto); no hay reordenamiento (la primera es la portada).
- **CORS:** lista explícita de orígenes desde configuración; solo los encabezados y métodos necesarios.
- **Cabeceras:** `X-Content-Type-Options`, `X-Frame-Options` y `Referrer-Policy`. En producción se activan HSTS y la redirección a HTTPS.
- **Errores:** `ProblemDetails` (RFC 9457). El detalle de un error interno (500) nunca se envía al cliente; solo se registra.
- **Búsqueda:** los caracteres comodín de `LIKE` se escapan para que el texto del usuario no altere la consulta.
- **Validación:** FluentValidation en cada solicitud de entrada; paginación acotada (máximo 100 por página).

### Persistencia

- **Borrado lógico** en todas las entidades. El correo es único solo entre usuarios no eliminados (índice filtrado `[IsDeleted] = 0`), de modo que se puede reutilizar tras un borrado.
- **Identificadores Guid v7:** únicos y ordenables por tiempo, y se generan en código.
- **Rol como texto** (`nvarchar(30)`): la base es legible y estable ante cambios del orden del enum.
- **Fechas** como `datetimeoffset`, con un `TimeProvider` inyectable (facilita las pruebas).
- **Auditoría** automática en `SaveChanges` (`CreatedAt`, `UpdatedAt`).
- **Resiliencia:** reintentos ante fallos transitorios de SQL Server.
- **Migraciones** en `Infrastructure/Persistence/Migrations`, aplicadas con `dotnet ef database update`. En Desarrollo también se pueden aplicar al arrancar.

### Sin MediatR ni mapeadores

Se evitó MediatR 15 porque requiere licencia comercial, y para este alcance bastan servicios de aplicación por caso de uso. El mapeo entre entidades y DTOs es manual: es explícito y no añade dependencias.

### Convenciones

- Nullable habilitado y advertencias como errores en toda la solución (`Directory.Build.props`).
- Versiones de paquetes centralizadas en `Directory.Packages.props`.
- Se desactivan las reglas CA1848 y CA1873 (obligan a usar `LoggerMessage`), por ser excesivas para este alcance.
- Código en inglés; interfaz de usuario en español.

## Frontend: Angular por funcionalidades

Angular 22 con **componentes standalone**, sin zone.js, con detección de cambios `OnPush` y estado con signals.

```
web/src/app/
├── core/                  Autenticación (servicio, interceptor, guards) y modelos globales
├── shared/                Componentes reutilizables (modal, paginador, toast, alerta de error) y utilidades
├── layout/                Marco de la aplicación con el menú lateral
└── features/              Una carpeta por funcionalidad, con carga diferida (lazy)
    ├── auth/login/
    ├── home/
    ├── landing/           Portada pública con el catálogo de propiedades publicadas
    ├── properties/        Inventario de propiedades (lista, formulario, detalle)
    └── users/             Lista, modal de alta y edición, modal de detalle, servicios y rutas
```

### Decisiones del frontend

- **Estilos:** Tailwind CSS 4 con DaisyUI 5, e iconos de Font Awesome Free. No se usan otras librerías de UI. La paleta, las fuentes (Manrope e Inter, servidas desde la propia app) y el logo salen de la [identidad visual](identidad-visual.md), implementada como tema `alhecasa` en `styles.css`.
- **Rutas con carga diferida** y guards: `authGuard` (exige sesión), `guestGuard` (solo visitantes) y `roleGuard` (exige rol). El menú muestra un ítem solo si el usuario puede abrir esa ruta.
- **Portada pública en `/`:** la ruta usa `canMatch: [guestMatch]`, así que solo coincide sin sesión; con sesión el enrutador sigue a la ruta siguiente y `/` abre el área de trabajo (`/inicio`). Lista las propiedades publicadas con `GET /api/public/properties` (sin token) y enlaza al login.
- **Mapa en el detalle público:** `app-location-map` (`shared/components/location-map`) usa Leaflet con mosaicos de OpenStreetMap y dibuja un **círculo de 250 m**, no un marcador, porque el producto no publica la dirección exacta: el personal captura la ubicación exacta (campo «latitud, longitud» del formulario, copiada de Google Maps; no hay geocodificación), pero el visitante solo recibe coordenadas redondeadas, y el círculo es mayor que el redondeo. Sin ubicación, el detalle no muestra la sección. Leaflet se carga de forma diferida (`LEAFLET_LOADER`, un `InjectionToken` que las pruebas sustituyen), así que solo lo descarga quien abre un detalle con mapa; si falla la carga se avisa en el recuadro. La rueda del ratón no hace zoom para no atrapar el scroll. **Uso de OpenStreetMap:** los mosaicos públicos son gratuitos pero con política de uso razonable y atribución obligatoria (ya se muestra); para un tráfico alto en producción hay que contratar un proveedor de mosaicos o alojar los propios y cambiar `TILE_URL`. Las propiedades de ejemplo reciben sus coordenadas al iniciar (`EnsureSampleCoordinatesAsync`, solo si no tienen).
- **Galería de la inmobiliaria (`/galeria`):** opción del menú para `AgencyAdmin`, `Supervisor` y `Seller` (no para el administrador de plataforma, que no pertenece a una inmobiliaria). Muestra las propiedades publicadas de la inmobiliaria del usuario con la misma tarjeta que la portada pública (`app-public-property-card`, compartida: así no pueden diverger) y datos que vienen de `GET /api/properties/gallery`, que devuelve el `PublicPropertyDto` y reutiliza las reglas de visibilidad pública (`PropertyRepository.GetPublishedPagedAsync` con el filtro opcional `agencyId`, que solo usa ese endpoint y no el catálogo público). Es de solo lectura; las tarjetas enlazan al detalle público. Los corazones funcionan igual que afuera (`localStorage`).
- **Solicitudes de informes:** en el detalle público (`/publicaciones/:id`) el botón «Pedir informes» abre un modal (`app-inquiry-form-modal`) con nombre, teléfono, correo y pregunta, con las mismas reglas que la API; si hay sesión, sugiere nombre y correo. Envía `POST /api/public/properties/{id}/inquiries`, que no devuelve datos (`201` vacío) y tiene su propio límite por IP porque es una escritura anónima. El personal (`AgencyAdmin`, `Supervisor`, `Seller`) las atiende en «Solicitudes de informes» (`/solicitudes`): lista con filtro por estado y búsqueda; al abrir una fila ve los datos del prospecto (con enlaces `tel:` y `mailto:`), su pregunta, el historial y un formulario para registrar una respuesta o una nota y cambiar el estado. **No se envía correo desde el sistema** (no hay infraestructura de correo todavía): el historial es constancia de lo que el personal contestó por su cuenta; el modal ofrece un enlace `mailto:` ya redactado. La solicitud pertenece a la inmobiliaria de la propiedad (filtro global de tenant), así que el aislamiento entre inmobiliarias es el mismo que el de las propiedades; el administrador de plataforma no las ve.
- **Cuenta gratuita (`/registro`):** formulario de nombre, apellidos, correo y contraseña (con las mismas reglas que la API y confirmación). Al terminar, la API ya devuelve la sesión y se abre `/propiedades` para publicar la primera propiedad. El registro se ofrece en la portada («Publica gratis» en la cabecera y en la presentación), en el detalle público y en el login. Con `isIndividual`, el menú oculta «Usuarios» y la barra muestra «Cuenta gratuita» en lugar del rol; la ruta `/usuarios` se protege con `companyGuard`. El `422` del tope de propiedades se muestra con el mensaje de la API (`resolveApiError`).
- **«Me gusta» en publicaciones:** `LikesService` (`core/likes`) guarda en `localStorage` (clave `alhecasa.likes`) la lista de identificadores de las publicaciones que le gustan al visitante, y `app-like-button` es el corazón que se usa en la lista y en el detalle. Es **solo del navegador**, sin backend: funciona igual con o sin sesión y cerrar sesión no lo borra (la clave es independiente de la de sesión), pero no viaja entre dispositivos ni navegadores, y se pierde si el visitante borra los datos del sitio. Guarda como máximo 100 (descarta los más antiguos), el mismo tope de ids que acepta el catálogo público por solicitud, ignora datos dañados y, si el navegador no deja escribir, el corazón sigue marcado solo mientras la pestaña esté abierta. Se sincroniza entre pestañas con el evento `storage`. El filtro **«Mis favoritas»** de la portada pide a la API solo esas publicaciones con `GET /api/public/properties?ids=…` (las mismas reglas de visibilidad: una favorita que ya no está publicada deja de aparecer); sin ninguna favorita no se llama a la API, porque una lista vacía de ids significa «sin filtro». Si más adelante se quisiera conservar entre dispositivos, habría que moverlo al servidor asociado a la cuenta.
- **Interceptor HTTP:** añade el token solo a peticiones hacia la API propia (nunca a terceros) y cierra la sesión ante un 401, excepto en el login. Cuando el usuario pulsa «Cerrar sesión» vuelve a la portada pública (`/`); cuando la sesión vence (401) va al login, con `returnUrl` para regresar a la pantalla en la que estaba.
- **Redirección segura tras el login:** solo se aceptan rutas internas (`/ruta`); se rechaza `//host` para evitar redirecciones abiertas.
- **Catálogo de usuarios:** lista paginada de 10 con búsqueda con debounce de 300 ms. El mismo modal sirve para alta y edición; un clic en la fila abre un modal de detalle de solo lectura. El éxito se muestra en un toast de 3 segundos y los errores en una alerta a todo el ancho con una X.
- **Modales** con `<dialog>` nativo: el navegador aporta el bloqueo de foco, la tecla Esc y el fondo inerte.
- **Mensajes de error en español:** `resolveApiError` traduce el código HTTP; en 400 añade los detalles de validación del servidor.
- **Entornos:** `environment.development.ts` apunta a `http://localhost:5156/api`; el de producción usa `/api` (la API detrás del mismo dominio).
- **Sesión en `localStorage`:** el token se guarda en el almacenamiento local, de modo que persiste entre pestañas y reinicios. Ten en cuenta el compromiso: es cómodo, pero un ataque XSS podría leerlo. Mitigaciones actuales: vigencia corta (60 minutos) y plantillas de Angular que sanean por defecto (no se inserta HTML dinámico). Si el riesgo no es aceptable, la alternativa es una cookie `HttpOnly` con protección CSRF.

## Permisos

| Operación | `PlatformAdmin` | `AgencyAdmin` | `Supervisor`, `Seller`, `Client` |
|---|---|---|---|
| Ver y listar usuarios | Todos | Solo los de su inmobiliaria | Sin acceso |
| Crear usuario | Cualquier rol e inmobiliaria | `AgencyAdmin`, `Supervisor` o `Seller`, siempre en su inmobiliaria. No puede crear `Client` ni convertir a alguien en `Client` | Sin acceso |
| Editar usuario | Cualquiera | Solo de su inmobiliaria, nunca un `PlatformAdmin` | Sin acceso |
| Eliminar usuario | Cualquiera (menos a sí mismo) | Solo de su inmobiliaria (menos a sí mismo) | Sin acceso |
| Ver inmobiliarias | Todas | Solo la suya | Sin acceso |
| Ver, crear, editar y eliminar propiedades | Todas (al crear debe elegir inmobiliaria) | Solo las de su inmobiliaria | `Supervisor` y `Seller`: solo las de su inmobiliaria. `Client`: sin acceso |
| Ver y atender solicitudes de informes | Sin acceso | Solo las de su inmobiliaria | `Supervisor` y `Seller`: solo las de su inmobiliaria. `Client`: sin acceso |
| Registrarse y publicar gratis | Sin acceso (no aplica) | Público, sin sesión: `POST /api/auth/register` crea un espacio `Individual` con un usuario `AgencyAdmin` (tope de propiedades; no administra usuarios) | Público, sin sesión (igual) |

Nadie puede cambiar su propio rol, desactivarse ni eliminarse.

**Supervisor y Seller:** el administrador de la inmobiliaria los da de alta como empleados. Hoy gestionan por completo las propiedades de su inmobiliaria (un `Seller` también puede editar y eliminar las de sus compañeros: aún no existe la noción de "propiedad asignada a un vendedor"); sus permisos sobre leads y supervisión del equipo se definirán cuando existan esos módulos. Dejar la administración de usuarios solo en `AgencyAdmin` es una decisión deliberada; si un `Supervisor` debe poder dar de alta vendedores, hay que ampliar `RoleNames.UserManagers` y las reglas de `UserService` (que hoy permitirían a un supervisor crear otro `AgencyAdmin`).

## Pruebas del backend

Dos proyectos en `api/tests`, con xUnit v3 sobre Microsoft.Testing.Platform (activado en `global.json`, porque el SDK de .NET 10 en adelante ya no admite VSTest con ese corredor):

| Proyecto | Qué prueba | Cómo |
|---|---|---|
| `Alhecasa.UnitTests` | Invariantes de `User`, reglas de permisos de `UserService`, `AuthService`, `SessionValidator` y `Pbkdf2PasswordHasher` | Dobles escritos a mano (sin librería de mocks); el hasher real se prueba aparte |
| `Alhecasa.IntegrationTests` | La API completa con `WebApplicationFactory`: login, aislamiento por inmobiliaria, permisos `401`/`403`/`404`, errores `ProblemDetails`, límite de intentos de login y revalidación del token | Base SQLite en memoria, una por clase de pruebas |

Decisiones y salvedades:

- **SQLite y no SQL Server** en las pruebas de integración: no dependen de LocalDB ni de una instancia instalada. El esquema se crea desde el modelo de EF (`EnsureCreated`), por lo que **las migraciones de SQL Server no se ejercitan** aquí. La Fase 0 (F0-03) añade pruebas de contrato contra más de un motor. SQLite no sabe ordenar por `DateTimeOffset`, así que un personalizador de modelo exclusivo de las pruebas (`SqliteModelCustomizer`) lo convierte a binario; el modelo de producción no se toca.
- Los tokens de prueba se emiten con el generador real (`IJwtTokenGenerator`) sin pasar por el login, que tiene límite de intentos por IP, y el hash de contraseña (PBKDF2, lento a propósito) se calcula una sola vez.
- Las pruebas de F0-02 se verificaron desactivando la revalidación: fallan las que corresponden (rol, inmobiliaria, desactivación, claims inválidos).

## Limitaciones y pendientes

Lo que este alcance **no** incluye, para que nadie lo dé por hecho:

- **Pruebas del backend:** existen, pero no cubren todo. Faltan pruebas de contrato de repositorios sobre SQL Server real y de las migraciones (las pruebas de integración usan SQLite; ver [Pruebas del backend](#pruebas-del-backend)).
- **Recuperar y cambiar contraseña**, verificación de correo y bloqueo de cuenta tras intentos fallidos. Solo existe el límite de intentos por IP.
- **Control de concurrencia optimista** (dos ediciones simultáneas del mismo usuario: gana la última).
- **Cierre de sesión en el servidor:** el acceso se revoca cuando la cuenta cambia, pero no hay lista de tokens revocados: un token de una cuenta sana sigue siendo válido hasta que vence, aunque el usuario "cierre sesión" en el navegador. Requiere un identificador de sesión o una lista de `jti` revocados.
- **Registro de inmobiliarias:** hoy las inmobiliarias solo se crean con el seeding o directamente en la base (`GET /api/agencies` es de solo lectura). El registro público existe solo para particulares (cuenta gratuita).
- **Cuenta gratuita sin verificación ni moderación:** el correo no se verifica (no hay infraestructura de correo) y los anuncios se publican de inmediato, sin revisión. El `409` del registro revela que un correo ya tiene cuenta. Un particular se publica como «Particular»: el catálogo público nunca muestra su nombre real (`PublicPropertyService.IndividualPublisherName`); el nombre queda solo en su cuenta.
- **Entidades `Client`, `Plan`, `Property` y `Listing`:** forman parte del propósito del proyecto pero aún no están implementadas.
- **Despliegue:** no hay Dockerfile ni pipeline de CI.

## Evolución prevista

Lo descrito en este documento es el **estado actual**. La dirección futura (roadmap por fases, modelo de organizaciones, publicación multiportal y backlog de la Fase 0) está en [estrategia.md](estrategia.md). Estas son las decisiones de arquitectura que ya se adoptaron para el siguiente paso:

- **Persistencia intercambiable.** Hoy `Alhecasa.Infrastructure` mezcla la parte agnóstica de EF Core (DbContext, configuraciones y repositorios) con lo específico de SQL Server (`UseSqlServer`, las migraciones y el filtro del índice `[IsDeleted] = 0` en `UserConfiguration.cs`). El objetivo es separar un proyecto de proveedor, `Alhecasa.Infrastructure.SqlServer`, elegido con `Database:Provider`, de modo que cambiar de motor no toque Domain ni Application. Application ya depende solo de interfaces, así que el trabajo se concentra en Infrastructure.
- **Todas las dependencias externas detrás de puertos** definidos en Application: archivos, correo, pagos, mapas, búsqueda y portales inmobiliarios.
- **Búsqueda detrás de `IListingSearch`**, con una primera implementación en SQL y sustituible por OpenSearch cuando el volumen lo justifique.
- **Modelo de usuarios:** pasar de `User` con un solo `AgencyId` y un solo rol a `User` + `Organization` + `Membership` (roles por organización), y separar `Property` de `Listing`.
- **Monolito modular** con los módulos Accounts, Catalog, Search, Leads, Billing e Integrations.

Las limitaciones actuales de la sección anterior se abordan en la Fase 0 de [estrategia.md](estrategia.md#9-backlog-de-la-fase-0).
