# API

Referencia de los endpoints de la API de ALHECASA. Para instalar y ejecutar, consulta [README.md](README.md).

- **URL base (desarrollo):** `http://localhost:5156`
- **Documentación interactiva (solo Desarrollo):** `http://localhost:5156/scalar/v1`. El documento OpenAPI está en `/openapi/v1.json`.
- **Formato:** JSON (`Content-Type: application/json`). Los nombres de las propiedades van en `camelCase`.
- **Enumeraciones:** se envían y reciben como texto (por ejemplo `"AgencyAdmin"`), no como número.
- **Fechas:** ISO 8601 con zona horaria (por ejemplo `2026-11-23T19:44:26.6512771+00:00`).
- **Identificadores:** Guid.

## Resumen

| Método | Ruta | Acceso | Descripción |
|---|---|---|---|
| `GET` | `/health` | Público | Estado del servicio |
| `POST` | `/api/auth/login` | Público (10 intentos por minuto por IP) | Iniciar sesión |
| `POST` | `/api/auth/register` | Público (5 por hora por IP) | Crear una cuenta gratuita de particular para publicar anuncios; deja la sesión iniciada |
| `GET` | `/api/auth/me` | Autenticado | Usuario de la sesión |
| `GET` | `/api/public/properties` | Público (120 solicitudes por minuto por IP) | Propiedades publicadas, para la portada de visitantes |
| `GET` | `/api/users` | `PlatformAdmin`, `AgencyAdmin` | Lista paginada con búsqueda y filtros |
| `GET` | `/api/users/{id}` | `PlatformAdmin`, `AgencyAdmin` | Detalle de un usuario |
| `POST` | `/api/users` | `PlatformAdmin`, `AgencyAdmin` | Crear usuario |
| `PUT` | `/api/users/{id}` | `PlatformAdmin`, `AgencyAdmin` | Actualizar usuario |
| `DELETE` | `/api/users/{id}` | `PlatformAdmin`, `AgencyAdmin` | Eliminar usuario (borrado lógico) |
| `GET` | `/api/agencies` | `PlatformAdmin`, `AgencyAdmin` | Inmobiliarias visibles para el usuario |
| `GET` | `/api/agencies/me` | `AgencyAdmin` (inmobiliaria, no particular) | Perfil de la propia inmobiliaria |
| `PUT` | `/api/agencies/me` | `AgencyAdmin` (inmobiliaria, no particular) | Editar el perfil de la propia inmobiliaria |
| `PUT` | `/api/agencies/me/logo` | `AgencyAdmin` (inmobiliaria, no particular) | Subir o reemplazar el logotipo |
| `DELETE` | `/api/agencies/me/logo` | `AgencyAdmin` (inmobiliaria, no particular) | Quitar el logotipo |
| `GET` | `/api/public/agencies/{id}` | Público (120 solicitudes por minuto por IP) | Información pública de una inmobiliaria |
| `GET` | `/api/public/agencies/{id}/logo` | Público (120 solicitudes por minuto por IP) | Logotipo de una inmobiliaria |
| `GET` | `/api/properties` | `PlatformAdmin`, `AgencyAdmin`, `Supervisor`, `Seller` | Inventario paginado con búsqueda |
| `GET` | `/api/properties/gallery` | `AgencyAdmin`, `Supervisor`, `Seller` | Galería: las propiedades publicadas de la inmobiliaria del usuario, tal como las ve un visitante |
| `GET` | `/api/properties/{id}` | Los mismos | Detalle de una propiedad |
| `POST` | `/api/properties` | Los mismos | Crear propiedad |
| `PUT` | `/api/properties/{id}` | Los mismos | Actualizar propiedad |
| `GET` | `/api/public/properties/{id}` | Público (120 solicitudes por minuto por IP) | Detalle de una propiedad publicada, con todas sus imágenes |
| `GET` | `/api/public/zones`, `/api/public/zones/{citySlug}[/{neighborhoodSlug}]` | Público (120 solicitudes por minuto por IP) | Zonas (ciudades y colonias) con propiedades publicadas |
| `GET` | `/api/public/sitemap.xml` | Público (120 solicitudes por minuto por IP) | Mapa del sitio para buscadores |
| `GET` | `/api/locations/states`, `/cities`, `/neighborhoods` | Cualquier rol | Sugerencias de ubicación para el formulario de propiedades |
| `POST` | `/api/properties/{id}/images` | Los mismos | Cambiar la galería: subir y quitar imágenes (1 a 10 por propiedad) |
| `PUT` | `/api/properties/{id}/featured` | Los mismos | Destacar una propiedad publicada (usa un lugar del paquete de destacados) o quitarle el destacado |
| `DELETE` | `/api/properties/{id}` | Los mismos | Eliminar propiedad (borrado lógico) |
| `GET` | `/api/plans` | `PlatformAdmin` | Catálogo de planes y paquetes |
| `GET` | `/api/plans/{id}` | `PlatformAdmin` | Detalle de un plan |
| `POST` | `/api/plans` | `PlatformAdmin` | Crear un plan |
| `PUT` | `/api/plans/{id}` | `PlatformAdmin` | Editar un plan |
| `DELETE` | `/api/plans/{id}` | `PlatformAdmin` | Eliminar un plan (borrado lógico) |
| `GET` | `/api/dashboard` | `AgencyAdmin`, `Supervisor`, `Seller` | Resumen de la inmobiliaria: propiedades por estado y solicitudes por etapa |
| `GET` | `/api/billing/me` | `AgencyAdmin`, `Supervisor`, `Seller` | Plan de la cuenta y lo que lleva usado |
| `GET` | `/api/billing/plans` | `AgencyAdmin` | Planes que la cuenta puede comprar |
| `POST` | `/api/billing/checkout` | `AgencyAdmin` | Comprar un plan con tarjeta (pasarela simulada) |
| `GET` | `/api/billing/accounts` | `PlatformAdmin` | Lo contratado y lo usado por todas las cuentas |
| `GET` | `/api/billing/accounts/{agencyId}/subscriptions` | `PlatformAdmin` | Historial de contrataciones de una cuenta |
| `POST` | `/api/billing/accounts/{agencyId}/subscriptions` | `PlatformAdmin` | Registrar que una cuenta contrató un plan |
| `POST` | `/api/billing/accounts/{agencyId}/subscriptions/{id}/cancel` | `PlatformAdmin` | Cancelar una contratación vigente |
| `GET` | `/api/images/{id}` | Público (600 solicitudes por minuto por IP) | Archivo de una imagen de propiedad |
| `POST` | `/api/public/properties/{propertyId}/inquiries` | Público (10 solicitudes cada 10 minutos por IP) | Pedir informes de una propiedad publicada |
| `GET` | `/api/inquiries` | `AgencyAdmin`, `Supervisor`, `Seller` | Solicitudes de informes de la inmobiliaria, paginadas, con filtro por estado y búsqueda |
| `GET` | `/api/inquiries/{id}` | Los mismos | Detalle de una solicitud con su seguimiento |
| `POST` | `/api/inquiries/{id}/follow-ups` | Los mismos | Registrar una respuesta o una nota interna |
| `PUT` | `/api/inquiries/{id}/status` | Los mismos | Cambiar el estado de la solicitud |

## Autenticación

Todos los endpoints, salvo `/health`, `/api/auth/login` y `/api/public/*`, exigen el encabezado:

```
Authorization: Bearer <accessToken>
```

El token se obtiene en el login y vence a los 60 minutos. Sin token, con token inválido o vencido, la API responde `401`. Con un token válido pero sin el rol requerido, responde `403`.

**El token se revalida contra la base de datos en cada petición.** Aunque la firma y la vigencia sean correctas, la API responde `401` si la cuenta ya no existe, fue eliminada o desactivada, cambió de rol o de inmobiliaria, o si su inmobiliaria fue eliminada o desactivada. En ese caso hay que iniciar sesión de nuevo (el frontend ya redirige al login ante un `401`).

**Claims del token:** `sub` (id del usuario), `email`, `name`, `role`, `agency_id` (solo si pertenece a una inmobiliaria), `jti` e `iat`.

### Roles

| Rol | Descripción |
|---|---|
| `PlatformAdmin` | Administrador de la plataforma. No pertenece a ninguna inmobiliaria. Ve y gestiona todo. |
| `AgencyAdmin` | Administrador de una inmobiliaria. Siempre pertenece a una. Solo ve y gestiona la suya. |
| `Supervisor` | Empleado de una inmobiliaria que coordina al equipo de vendedores. Siempre pertenece a una. Por ahora no administra usuarios; sí gestiona propiedades. |
| `Seller` | Vendedor: empleado de una inmobiliaria. Siempre pertenece a una. Por ahora no administra usuarios; sí gestiona propiedades. |
| `Client` | Usuario sin acceso a los endpoints de administración. (Los particulares que publican **no** usan este rol: se registran con `POST /api/auth/register` y quedan como `AgencyAdmin` de un espacio `Individual` propio.) |

`Supervisor` y `Seller` los da de alta el `AgencyAdmin` (o el `PlatformAdmin`) con `POST /api/users`. Inician sesión como cualquier otro usuario y tienen acceso a los endpoints de propiedades y a los que no exigen rol (por ejemplo `GET /api/auth/me`); los permisos de leads y de supervisión del equipo se definirán cuando existan esos módulos.

---

## Autenticación

### `POST /api/auth/login`

Inicia sesión. Es público y tiene un límite de **10 intentos por minuto por IP**; al superarlo responde `429`.

**Cuerpo**

| Campo | Tipo | Reglas |
|---|---|---|
| `email` | string | Obligatorio, máximo 256 caracteres |
| `password` | string | Obligatorio, máximo 128 caracteres |

```json
{
  "email": "admin@alhecasa.local",
  "password": "<contraseña>"
}
```

**Respuesta `200`**

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresAt": "2026-11-23T21:29:23.2401558+00:00",
  "user": {
    "id": "01a209f0-0697-7968-975b-a1ffc8cd58e0",
    "fullName": "Platform Admin",
    "email": "admin@alhecasa.local",
    "role": "PlatformAdmin",
    "agencyId": null,
    "agencyName": null
  }
}
```

**Errores:** `400` (campos vacíos o demasiado largos), `401` (credenciales inválidas), `429` (demasiados intentos).

El `401` responde siempre igual (`"Invalid email or password."`) si el correo no existe, la contraseña es incorrecta, la cuenta está inactiva o la inmobiliaria está inactiva. Es intencional, para no revelar qué correos están registrados.

### `POST /api/auth/register`

Crea una **cuenta gratuita de particular** y devuelve la sesión iniciada (mismo objeto que el login, con `201`). Es público y tiene un límite de **5 altas por hora por IP** (configurable con `RateLimiting:Register:PermitLimit`); al superarlo responde `429`.

| Campo | Tipo | Reglas |
|---|---|---|
| `firstName` | string | Obligatorio, máximo 100 caracteres |
| `lastName` | string | Obligatorio, máximo 100 caracteres |
| `email` | string | Obligatorio, correo válido, máximo 256 (se guarda en minúsculas; único entre usuarios) |
| `password` | string | De 10 a 128 caracteres, con mayúscula, minúscula y número (las mismas reglas que al crear un usuario) |

**El rol y el espacio los fija el servidor:** cualquier campo extra (`role`, `agencyId`, `isActive`…) se ignora, así que por aquí jamás se obtiene `PlatformAdmin`. La cuenta nace como un espacio propio de tipo `Individual` (una «inmobiliaria» con el nombre de la persona) con un único usuario `AgencyAdmin`, todo en una sola transacción. En la respuesta, `user.isIndividual` es `true`.

**Errores:** `400` (datos inválidos), `409` (el correo ya está registrado), `429`. **El `409` revela que un correo ya tiene cuenta**: sin verificación de correo no hay forma de evitarlo; el límite por IP lo frena.

Qué puede hacer una cuenta gratuita:

- Todo lo de propiedades, imágenes, galería y solicitudes de informes, **solo con su propio espacio** (mismo aislamiento que una inmobiliaria).
- **Tope de propiedades:** `AccountLimits:IndividualMaxProperties` (por defecto 3, borradores incluidos; las eliminadas no cuentan). Al excederlo, `POST /api/properties` responde `422` con un mensaje listo para mostrar. Una inmobiliaria no tiene tope.
- **No administra usuarios:** `/api/users` responde `403` (tiene un solo usuario). `GET /api/agencies` devuelve su espacio con `isIndividual: true`.
- En el catálogo público aparece como «Publica: Particular»: **nunca se publica su nombre real** (ni en la lista ni en el detalle).

### `GET /api/auth/me`

Devuelve el usuario de la sesión, volviendo a consultarlo en la base de datos.

**Respuesta `200`:** el mismo objeto `user` del login. `isIndividual` es `true` solo para las cuentas gratuitas de particular (puede venir ausente o `false` en las demás).

**Errores:** `401` (sin token, o el usuario ya no existe o está inactivo).

---

## Usuarios

### Objeto `User`

```json
{
  "id": "01a20a13-f2fc-7dbb-87fa-10d28da9da96",
  "firstName": "Laura",
  "lastName": "Prueba",
  "fullName": "Laura Prueba",
  "email": "laura@example.com",
  "role": "Client",
  "agencyId": null,
  "agencyName": null,
  "isActive": true,
  "createdAt": "2026-11-23T19:44:26.6512771+00:00",
  "lastLoginAt": null
}
```

`agencyId` y `agencyName` son `null` cuando el usuario no pertenece a ninguna inmobiliaria (`PlatformAdmin` o un `Client` particular). El correo se guarda siempre en minúsculas.

### `GET /api/users`

Lista usuarios con paginación. Ordena por nombre, apellidos e identificador. Excluye los eliminados. Un `AgencyAdmin` solo recibe usuarios de su inmobiliaria.

**Parámetros de consulta (todos opcionales)**

| Parámetro | Tipo | Por defecto | Descripción |
|---|---|---|---|
| `search` | string | | Busca en nombre, apellidos y correo (coincidencia parcial) |
| `role` | `PlatformAdmin` \| `AgencyAdmin` \| `Supervisor` \| `Seller` \| `Client` | | Filtra por rol. Un valor inválido responde `400`. |
| `isActive` | boolean | | Filtra por estado |
| `page` | entero | `1` | Página (valores menores de 1 se tratan como 1) |
| `pageSize` | entero | `10` | Tamaño de página, entre 1 y **100** (valores mayores se limitan a 100) |

**Ejemplo:** `GET /api/users?search=laura&role=Client&page=1&pageSize=10`

**Respuesta `200`**

```json
{
  "items": [ { "id": "...", "fullName": "Laura Prueba", "...": "..." } ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

### `GET /api/users/{id}`

Devuelve un usuario.

**Respuesta `200`:** objeto `User`.

**Errores:** `404` si no existe, si está eliminado o si pertenece a otra inmobiliaria (para un `AgencyAdmin` es indistinguible de que no exista). Un `{id}` que no sea un Guid también responde `404`.

### `POST /api/users`

Crea un usuario.

**Cuerpo**

| Campo | Tipo | Reglas |
|---|---|---|
| `firstName` | string | Obligatorio, máximo 100 |
| `lastName` | string | Obligatorio, máximo 100 |
| `email` | string | Obligatorio, formato válido, máximo 256, único entre usuarios no eliminados |
| `password` | string | Obligatorio, de 10 a 128 caracteres, con al menos una mayúscula, una minúscula y un número |
| `role` | `PlatformAdmin` \| `AgencyAdmin` \| `Supervisor` \| `Seller` \| `Client` | Obligatorio |
| `agencyId` | Guid \| null | Ver reglas de inmobiliaria |

```json
{
  "firstName": "Laura",
  "lastName": "Prueba",
  "email": "laura@example.com",
  "password": "<contraseña>",
  "role": "Client",
  "agencyId": null
}
```

**Reglas de inmobiliaria**

| Quien crea | Comportamiento |
|---|---|
| `PlatformAdmin` | Elige libremente. `AgencyAdmin`, `Supervisor` y `Seller` exigen `agencyId` (sin él, `400`); `Client` puede ir sin `agencyId` (particular); `PlatformAdmin` ignora `agencyId` y queda sin inmobiliaria. |
| `AgencyAdmin` | El usuario (incluidos sus empleados `Supervisor` y `Seller`) queda siempre en la inmobiliaria del que crea, aunque no envíe `agencyId`. No puede crear un `PlatformAdmin` ni un `Client` (`403`), ni indicar otra inmobiliaria (`403`). Los clientes los da de alta solo el `PlatformAdmin`. |

**Respuesta `201`:** objeto `User`, con el encabezado `Location` apuntando a `GET /api/users/{id}`.

**Errores:** `400` (validación o regla de negocio), `403`, `404` (la inmobiliaria indicada no existe), `409` (correo ya registrado).

### `PUT /api/users/{id}`

Actualiza un usuario. Reemplaza todos los campos listados; no cambia la contraseña.

**Cuerpo**

| Campo | Tipo | Reglas |
|---|---|---|
| `firstName` | string | Obligatorio, máximo 100 |
| `lastName` | string | Obligatorio, máximo 100 |
| `email` | string | Obligatorio, formato válido, máximo 256, único |
| `role` | `PlatformAdmin` \| `AgencyAdmin` \| `Supervisor` \| `Seller` \| `Client` | Obligatorio |
| `agencyId` | Guid \| null | Mismas reglas que al crear |
| `isActive` | boolean | Estado del usuario |

**Restricciones**

- Nadie puede cambiar **su propio rol** ni **desactivar su propia cuenta** (`403`).
- Un `AgencyAdmin` no puede mover usuarios a otra inmobiliaria (`403`) ni asignar el rol `PlatformAdmin` (`403`). Tampoco puede convertir a un usuario en `Client` (`403`); sí puede seguir editando (o desactivar) a un `Client` que ya existe, siempre que no le cambie el rol. Los usuarios de otras inmobiliarias y los `PlatformAdmin` no le son visibles, así que recibe `404`.

**Respuesta `200`:** objeto `User` actualizado.

**Errores:** `400`, `403`, `404`, `409`.

### `DELETE /api/users/{id}`

Eliminación **lógica**: el usuario deja de aparecer y de poder iniciar sesión, y su correo queda libre para reutilizarse.

**Respuesta `204`** sin cuerpo.

**Errores:** `403` (nadie puede eliminar su propia cuenta), `404` (no existe o no es visible; un `AgencyAdmin` no ve a los `PlatformAdmin` ni a usuarios de otras inmobiliarias).

---

## Inmobiliarias

### `GET /api/agencies`

Lista las inmobiliarias visibles para el usuario: todas para `PlatformAdmin`, solo la propia para `AgencyAdmin`. Ordenadas por nombre. Es de solo lectura.

**Respuesta `200`**

```json
[
  {
    "id": "01a209f0-0799-7366-912e-21440683214d",
    "name": "Demo Real Estate",
    "email": "contact@demo.alhecasa.local",
    "phone": "5555550100",
    "isActive": true,
    "isIndividual": false
  }
]
```

---

### Perfil de la propia inmobiliaria

El administrador de una inmobiliaria configura la información que se muestra en su página pública. Solo el rol `AgencyAdmin` de una **inmobiliaria**: un particular (`isIndividual`), el resto del personal, los clientes y el administrador de plataforma reciben `403`. Un `AgencyAdmin` solo alcanza su propia inmobiliaria: no hay forma de indicar otra.

### Objeto `AgencyProfile`

| Campo | Tipo | Notas |
|---|---|---|
| `id` | guid | |
| `name` | string | Obligatorio, máximo 200 |
| `email` | string | Obligatorio, correo válido, máximo 256. **Se publica** |
| `phone` | string \| null | De 7 en adelante: solo dígitos, espacios y `+ ( ) - .`; máximo 30. **Se publica** |
| `description` | string \| null | Máximo 1000 |
| `website`, `facebookUrl`, `instagramUrl` | string \| null | Dirección `http://` o `https://`, máximo 200. Cualquier otro esquema (`javascript:`, `data:`, `ftp:`…) responde `400`: estos enlaces se publican |
| `openingHours` | string \| null | Texto libre, máximo 200 |
| `street` | string \| null | Máximo 200. **Se publica** |
| `city`, `state` | string \| null | Máximo 100 |
| `postalCode` | string \| null | Máximo 10 |
| `hasLogo` | bool | Solo lectura: se cambia con los endpoints del logotipo |

Los textos se recortan y un texto vacío se guarda como `null`.

### `GET /api/agencies/me`

Respuesta `200` con el `AgencyProfile` de la inmobiliaria del usuario.

### `PUT /api/agencies/me`

Reemplaza los datos del perfil (todos los campos salvo `id` y `hasLogo`). Respuesta `200` con el perfil guardado; `400` si algún dato es inválido, con el detalle por campo. Cambiar el `name` cambia también el nombre con el que se publican sus propiedades en el catálogo.

### `PUT /api/agencies/me/logo`

`multipart/form-data` con el archivo en el campo `file`: JPEG, PNG o WebP de hasta 5 MB. El formato se reconoce por el contenido del archivo, no por su nombre ni por el tipo que declara el cliente (un SVG, que puede llevar scripts, se rechaza). Reemplaza el logotipo anterior y borra su archivo. Respuesta `200` con el perfil; `400` si falta el archivo o no es una imagen aceptada.

### `DELETE /api/agencies/me/logo`

Quita el logotipo y borra su archivo. Si no había, no hace nada. Respuesta `200` con el perfil.

### `GET /api/public/agencies/{id}`

Información de una inmobiliaria para su página pública (`/inmobiliarias/:id` en la web). **No requiere token.** Devuelve los campos del `AgencyProfile` salvo `isActive` y datos internos. **Solo existe para inmobiliarias activas y no eliminadas**: un particular es anónimo y nunca tiene página, así que su id, una inmobiliaria inactiva o eliminada y un id desconocido responden `404` sin distinguir el motivo.

### `GET /api/public/agencies/{id}/logo`

La imagen del logotipo, con las mismas reglas de visibilidad (`404` también si no tiene logotipo). Se entrega con `ETag` y `Cache-Control: public, no-cache`: el navegador la revalida en cada visita y recibe `304` si no cambió; al cambiar el logotipo cambia el `ETag`.

---
## Propiedades

Inventario de propiedades que administra una inmobiliaria. Acceso: `PlatformAdmin`, `AgencyAdmin`, `Supervisor` y `Seller`. Cada propiedad pertenece a una inmobiliaria y el personal solo ve las suyas; una propiedad de otra inmobiliaria responde `404`, no `403`.

> **Simplificación del MVP:** `price`, `currency`, `operation` y `status` viven en la propiedad. La estrategia (F0-07) los separa en una entidad de publicación (*Listing*) para poder tener varias operaciones por propiedad; cuando llegue, este contrato cambiará.

### Objeto `Property`

| Campo | Tipo | Notas |
|---|---|---|
| `id`, `agencyId`, `agencyName` | guid, guid, texto o `null` | |
| `title` | texto | Obligatorio, máx. 150 |
| `description` | texto o `null` | Máx. 2000 |
| `type` | texto | `House`, `Apartment`, `Land`, `Commercial`, `Office` |
| `operation` | texto | `Sale`, `Rent` |
| `status` | texto | `Draft` (por defecto), `Published`, `Paused`, `Closed` |
| `price` | número | Obligatorio, mayor a 0, máx. 9,999,999,999,999.99 |
| `currency` | texto | `MXN` (por defecto), `USD` |
| `street`, `neighborhood`, `city`, `state`, `postalCode` | texto | `city` y `state` obligatorios. Máx. 200, 100, 100, 100 y 10 |
| `bedrooms`, `bathrooms` | entero o `null` | 0 a 50 |
| `parkingSpaces` | entero o `null` | Lugares de estacionamiento, 0 a 50. `null` = no se capturó |
| `constructionArea`, `landArea` | número o `null` | m², 0 a 99,999,999.99 |
| `petsAllowed`, `furnished` | booleano | Aceptan mascotas / amueblada. `false` por defecto |
| `amenities` | lista de texto | Amenidades: `Pool`, `Gym`, `Security24h`, `Garden`, `Terrace`, `Elevator`, `Storage`, `ServiceRoom`, `AirConditioning`, `CommonAreas`, `Fireplace`, `Playground`. Sin repetidos; un nombre desconocido responde `400`. Al guardarse es un campo de bits, pero la API siempre habla en nombres (es la única enumeración que viaja como lista) |
| `latitude`, `longitude` | número o `null` | Ubicación exacta para el mapa. Van **juntas** (las dos o ninguna; una sola responde `400`). Latitud -90 a 90 y longitud -180 a 180; se guardan con 6 decimales. Al editar, omitirlas o enviar `null` quita la ubicación |
| `imageIds` | lista de guid | Imágenes en orden de galería; la primera es la portada. Se piden con `GET /api/images/{id}` |
| `isFeatured`, `featuredUntil` | booleano, fecha o `null` | Si está destacada **ahora** y hasta cuándo (el fin del paquete que la destacó). Solo lectura: se cambia con `PUT /api/properties/{id}/featured`. Un destacado vencido se informa como `false` / `null` |
| `createdAt`, `updatedAt` | fecha | |

### `GET /api/properties`

Parámetros, todos opcionales y combinables con «y»: `search` (busca en título, ciudad, colonia y calle), `status` (`Draft`, `Published`, `Paused`, `Closed`), `type`, `operation`, los **criterios avanzados** (los mismos que el catálogo público; ver la tabla de [`GET /api/public/properties`](#get-apipublicproperties): `minPrice`, `maxPrice`, `currency`, `minBedrooms`, `minBathrooms`, `minParking`, `minArea`, `maxArea`, `petsAllowed`, `furnished`, `amenities` y `sort`), `page` (por defecto 1) y `pageSize` (por defecto 10, máximo 100). Sin `sort`, las más recientes primero. A diferencia del catálogo público, aquí aparecen también borradores, pausadas y cerradas. Un valor inválido responde `400`. Respuesta `200` con la misma forma paginada que `/api/users` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`).

### `GET /api/properties/gallery`

Las propiedades **publicadas de la inmobiliaria del usuario**, con el mismo contenido que el catálogo público (objeto `PublicPropertyDto`: sin calle ni código postal y con la ubicación aproximada) y el mismo orden (más recientes primero, o el de `sort`). Sirve para que el personal vea cómo se publican. Parámetros: `page` (por defecto 1), `pageSize` (por defecto 12, máximo 100) y los mismos criterios de búsqueda que el catálogo público (`operation`, `type`, `location`, criterios avanzados y `sort`; `ids` se ignora). La pantalla de la galería aún no ofrece panel de filtros, pero la API ya los acepta. Respuesta `200` paginada como las demás listas. Aplica las mismas reglas de visibilidad que el sitio público: una propiedad que allá no se vería (borrador, pausada, cerrada, eliminada o de una inmobiliaria inactiva) tampoco aparece aquí. **Errores:** `401`; `403` para el administrador de plataforma (no pertenece a una inmobiliaria) y para clientes.

### `GET /api/properties/{id}`

Respuesta `200` con el objeto `Property`. `404` si no existe o es de otra inmobiliaria.

### `POST /api/properties`

Cuerpo: todos los campos editables del objeto `Property` más `agencyId`. **No se puede crear una propiedad ya `Published`** (`400`): aún no tiene imágenes. El flujo es crearla como `Draft`, subir las imágenes y después publicarla con `PUT`.

- Personal de inmobiliaria: `agencyId` se omite (se usa la suya); si envía otra inmobiliaria, `403`.
- `PlatformAdmin`: `agencyId` es obligatorio (`400` si falta) y la inmobiliaria debe existir (`404`).

**Respuesta `201`:** objeto `Property`, con `Location` hacia `GET /api/properties/{id}`. **Errores:** `400` (validación), `403`, `404`.

### `PUT /api/properties/{id}`

Mismos campos que el alta, sin `agencyId`: una propiedad no cambia de inmobiliaria. Pasar a `Published` una propiedad sin imágenes responde `400`. Si una propiedad deja de estar `Published` (se pausa, se cierra o vuelve a borrador), **pierde el destacado**. **Errores:** `400`, `404`.

### `PUT /api/properties/{id}/featured`

Cuerpo: `{ "featured": true }` para destacar o `{ "featured": false }` para quitar el destacado. Destacar usa **un lugar del paquete de destacados vigente** de la cuenta de la propiedad y dura hasta que el paquete vence (`featuredUntil`). Quitar el destacado libera el lugar y siempre es posible.

Reglas: solo se destaca una propiedad `Published` (`400` si no). Destacar una ya destacada no hace nada. Si la cuenta no tiene un paquete vigente, o ya tiene destacadas tantas como permite (`limit`), responde `422` con un mensaje listo para mostrar.

**Respuesta `200`:** objeto `Property`. **Errores:** `400`, `401`, `403`, `404`, `422`.

### `POST /api/properties/{id}/images`

Cambia la galería en **una sola operación**: agrega imágenes nuevas y quita otras, para poder reemplazar una foto aunque la galería ya tenga 10. Cuerpo `multipart/form-data`:

| Campo | Descripción |
|---|---|
| `files` | Cero o más archivos (repetir el campo). Se agregan al final de la galería, en el orden recibido. |
| `removeImageIds` | Cero o más guid de imágenes actuales de esa propiedad que se quitan (repetir el campo). |

Reglas:

- Al terminar, la propiedad debe tener **como máximo 10** imágenes. Una propiedad `Published` debe conservar **al menos 1**.
- Cada archivo: no vacío, de **hasta 5 MB**, y **JPEG, PNG o WebP**. El formato se detecta por el contenido del archivo (sus primeros bytes), no por el nombre ni por el `Content-Type` que declare el cliente: un archivo renombrado a `.jpg` se rechaza.
- Es todo o nada: si un archivo no es válido o se rebasa algún límite, no se guarda nada.
- La solicitud completa admite hasta unos 51 MB.

**Respuesta `200`:** objeto `Property` con `imageIds` actualizado. **Errores:** `400` (límites, formato o imagen que no es de la propiedad), `401`, `403`, `404` (propiedad inexistente o de otra inmobiliaria).

### `GET /api/images/{id}`

Entrega el archivo de una imagen. **No requiere token**: las etiquetas `<img>` del navegador no lo envían. La dirección es un guid que no se puede adivinar, así que funciona como un enlace secreto: quien la conoce puede ver la imagen **aunque la propiedad aún sea un borrador** o esté pausada. Si la propiedad se elimina, responde `404`.

Respuesta `200` con el tipo de la imagen, `Cache-Control: public, max-age=31536000, immutable` (una imagen nunca cambia: reemplazarla crea otra con otro id) y `X-Content-Type-Options: nosniff`. `404` si no existe. Límite: 600 solicitudes por minuto por IP.

### `DELETE /api/properties/{id}`

Eliminación **lógica**. **Respuesta `204`.** **Error:** `404`.

---

## Catálogo público

### `GET /api/public/properties`

Alimenta la portada que ve quien no ha iniciado sesión. **No requiere token.** Muestra las propiedades de **todas** las inmobiliarias que cumplen estas reglas: estado `Published`, no eliminadas y de una inmobiliaria activa y no eliminada. Los borradores, pausadas y cerradas nunca aparecen.

Parámetros, todos opcionales y combinables (se aplican con «y»):

| Parámetro | Valores | Descripción |
|---|---|---|
| `operation` | `Sale` \| `Rent` | Solo venta o solo renta. Un valor inválido responde `400`. |
| `type` | `House` \| `Apartment` \| `Land` \| `Commercial` \| `Office` | Tipo de inmueble. Un valor inválido responde `400`. |
| `location` | texto | Debe aparecer en la colonia, la ciudad o el estado (sin distinguir mayúsculas). No busca en el título ni en la calle. |
| `agencyId` | guid | Solo las publicaciones activas de esa inmobiliaria. Es lo que abre el enlace «Publica: *nombre*» de las tarjetas (`/inmobiliarias/{id}` en la web). Un particular no se puede listar así: su `agencyId` no se publica y, si alguien lo conociera, tampoco devuelve nada. Un valor que no sea guid responde `400`; una inmobiliaria sin publicaciones activas, una página vacía. Se combina con los demás filtros. |
| `citySlug` | texto | Solo las propiedades de esa ciudad del catálogo (por su identificador de dirección web, sin distinguir mayúsculas; ver «Zonas públicas»). Una ciudad desconocida da una página vacía. |
| `neighborhoodSlug` | texto | Solo las de esa colonia. **Requiere `citySlug`** (la colonia es única solo dentro de su ciudad): sin él responde `400`. |
| `ids` | guid, repetible | Solo estas propiedades (`?ids=<guid>&ids=<guid>`), siempre que además estén publicadas: un id de un borrador, de una propiedad pausada o eliminada, o desconocido simplemente no aparece. Lo usa «Mis favoritas» de la portada, cuyos `me gusta` se guardan en el navegador. Máximo **100** ids (`400` si se envían más); un valor que no sea guid responde `400`. Se combina con los demás filtros y con la paginación. |
| `minPrice`, `maxPrice` | número | Rango de precio, en la moneda de `currency`. **No hay conversión** entre `MXN` y `USD`. Si se envía uno, `currency` es obligatoria. Deben ser ≥ 0, no pasar de 9,999,999,999,999.99 y `minPrice` ≤ `maxPrice` (`400` si no). |
| `currency` | `MXN` \| `USD` | Moneda de los límites de precio. Sin límites, no filtra. |
| `minBedrooms`, `minBathrooms`, `minParking` | entero 0 a 50 | «Al menos» esa cantidad. Una propiedad con el dato en blanco (`null`) no cumple si el mínimo es mayor que 0. |
| `minArea`, `maxArea` | número | m². Se compara la superficie construida y, si no tiene (un terreno), la del terreno. Mismas reglas de rango que el precio. |
| `petsAllowed`, `furnished` | booleano | `true` deja solo las que aceptan mascotas / las amuebladas. `false` o ausente no filtra. |
| `amenities` | texto, repetible | `?amenities=Pool&amenities=Gym`: debe tener **todas** las indicadas. Un nombre desconocido responde `400`. |
| `sort` | `Recent` \| `PriceAsc` \| `PriceDesc` \| `AreaDesc` | Por defecto `Recent`. Los empates se resuelven por antigüedad y luego por id, así el orden es estable al paginar. El precio se compara tal cual, sin convertir monedas (para ordenar con sentido, combínalo con `currency`). |
| `page` | entero | Por defecto 1 |
| `pageSize` | entero | Por defecto 10, máximo 100 |

Orden: el de `sort` (más recientes primero por defecto). Con el orden por defecto (`Recent`, o sin `sort`) los **anuncios destacados van primero**, y dentro de cada grupo los más recientes primero; con los demás órdenes (`PriceAsc`, `PriceDesc`, `AreaDesc`) no se anteponen. La respuesta tiene la misma forma paginada que las demás listas.

**Ejemplos:** `GET /api/public/properties?operation=Rent&type=Apartment&location=roma` y `GET /api/public/properties?currency=MXN&maxPrice=3000000&minBedrooms=2&petsAllowed=true&amenities=Pool&sort=PriceAsc`

Cada elemento de `items` trae solo datos públicos: `id`, `title`, `description`, `type`, `operation`, `price`, `currency`, `neighborhood`, `city`, `state`, `bedrooms`, `bathrooms`, `parkingSpaces`, `constructionArea`, `landArea`, `petsAllowed`, `furnished`, `amenities`, `approximateLatitude`, `approximateLongitude`, `imageIds` (la primera es la portada), `agencyName`, `agencyId`, `citySlug` y `neighborhoodSlug` (los identificadores de las páginas de su ciudad y su colonia; `null` si la propiedad aún no está ligada al catálogo de ubicaciones) e `isFeatured` (si el anuncio está destacado ahora; la portada le pone la etiqueta «Destacado», sin importar el orden elegido). **No incluye** la calle ni el código postal (la dirección exacta se da al contactar), ni el estado. El único identificador de la inmobiliaria es `agencyId`, que sirve para listar sus publicaciones; es `null` cuando publica un particular, que es anónimo. La ubicación es **aproximada a propósito**: las coordenadas públicas se redondean a 3 decimales (unos 110 m) y son `null` si la propiedad no tiene ubicación; las exactas (`latitude`, `longitude`) solo las devuelve la API autenticada del personal.

Límite: 120 solicitudes por minuto por IP (`429` al excederlo).

### `GET /api/public/properties/{id}`

Detalle de una propiedad para la página de la publicación. **No requiere token.** Devuelve el mismo objeto público que cada elemento de la lista (con `imageIds` completo, en orden de galería) y aplica las **mismas reglas de visibilidad**: solo `Published`, no eliminada y de una inmobiliaria activa. Un borrador, una propiedad pausada, cerrada o eliminada, o un id desconocido responden `404` (no se distingue el motivo). Mismo límite de solicitudes que el listado.

## Ubicaciones y zonas públicas

Las propiedades se ligan a un **catálogo de ubicaciones** (estados, ciudades y colonias; ver [arquitectura](arquitectura.md#catálogo-de-ubicaciones-y-páginas-por-zona-seo)). Al crear o editar una propiedad, los textos `state`, `city` y `neighborhood` se **reemplazan por los nombres del catálogo** (por ejemplo `cdmx` queda como `Ciudad de México`) y, si la ciudad o la colonia no existen, se dan de alta. Un nombre sin letras ni números responde `400`. La respuesta de la propiedad ya trae los nombres canónicos.

### `GET /api/locations/states`, `/cities`, `/neighborhoods`

Sugerencias para el formulario de propiedades. **Requieren sesión** (cualquier rol). Devuelven un arreglo de nombres (texto), ordenados, **máximo 50**.

| Ruta | Parámetros | Descripción |
|---|---|---|
| `GET /api/locations/states` | — | Los 32 estados y los que se hayan dado de alta. |
| `GET /api/locations/cities` | `state` (nombre o abreviatura, opcional), `q` (texto contenido, opcional) | Ciudades del estado, o de todos si `state` va vacío. |
| `GET /api/locations/neighborhoods` | `state`, `city`, `q` | Colonias de esa ciudad. Sin ciudad (o si no existe) responde un arreglo vacío. |

### `GET /api/public/zones`

Las ciudades y colonias con más propiedades publicadas, mezcladas, para los enlaces «Explora por zona». **No requiere token.** Parámetro `limit` (por omisión 30, entre 1 y 100). Cada elemento: `name`, `cityName`, `citySlug`, `neighborhoodSlug` (`null` si es una ciudad), `saleCount` y `rentCount`. Solo cuentan las propiedades visibles al público (mismas reglas que el catálogo público).

### `GET /api/public/zones/{citySlug}` y `GET /api/public/zones/{citySlug}/{neighborhoodSlug}`

Una ciudad o una colonia de ella. **No requiere token.** Devuelve `cityName`, `citySlug`, `stateName`, `neighborhoodName` y `neighborhoodSlug` (`null` en una ciudad), `saleCount`, `rentCount` y `neighborhoods` (las colonias de la ciudad con propiedades, con más propiedades primero). Responde `404` si la zona no existe, no tiene ninguna propiedad publicada o la colonia es de otra ciudad. Es lo que usan las páginas `/venta/:ciudad[/:colonia]` y `/renta/:ciudad[/:colonia]` de la web; sus propiedades se piden al catálogo con `citySlug`, `neighborhoodSlug` y `operation`.

### `GET /api/public/sitemap.xml`

El mapa del sitio (formato sitemaps.org, `application/xml`, UTF-8): la portada, la calculadora hipotecaria, las zonas con propiedades en cada operación, las inmobiliarias con nombre y cada publicación. Las direcciones usan el dominio configurado en `Seo:SiteUrl` (el del **frontend**) y hay un máximo de 50,000. **No requiere token.** Los tres endpoints de zonas y el mapa comparten el límite de 120 solicitudes por minuto por IP.

---

## Solicitudes de informes

Un visitante pide informes de una propiedad publicada y el personal de la inmobiliaria dueña de esa propiedad la atiende. **Una solicitud solo la ve la inmobiliaria de la propiedad**; el administrador de plataforma no tiene acceso (no pertenece a ninguna inmobiliaria).

### `POST /api/public/properties/{propertyId}/inquiries`

**No requiere token.** Aplica las mismas reglas de visibilidad que el detalle público: si la propiedad no está publicada, está eliminada, o su inmobiliaria está inactiva o eliminada (o el id no existe), responde `404`.

```json
{ "name": "Ana López", "phone": "55 1234 5678", "email": "ana@demo.com", "message": "¿Sigue disponible?" }
```

| Campo | Regla |
|---|---|
| `name` | Obligatorio, máximo 100 caracteres |
| `phone` | Obligatorio, máximo 30; solo dígitos, espacios, `+ ( ) - .` y de 7 a 15 dígitos |
| `email` | Obligatorio, correo válido, máximo 256 (se guarda en minúsculas) |
| `message` | Obligatorio, máximo 2000 |

Responde `201` **sin cuerpo**: quien no ha iniciado sesión no recibe ningún dato de lo que se guardó. Datos inválidos responden `400`. Límite: **10 solicitudes cada 10 minutos por IP** (`429` al excederlo); es configurable con `RateLimiting:Inquiries:PermitLimit`. La solicitud guarda el id de la propiedad, la inmobiliaria dueña y el título de la propiedad en ese momento (para que la bandeja siga siendo legible si luego se renombra o elimina).

### Objeto `Inquiry`

| Campo | Descripción |
|---|---|
| `id`, `propertyId`, `propertyTitle` | Identificadores y título de la propiedad al momento de la solicitud |
| `name`, `phone`, `email`, `message` | Datos del prospecto y su pregunta |
| `status` | `New` (nueva), `InProgress` (en seguimiento) o `Closed` (cerrada) |
| `followUps` | Historial, del más antiguo al más reciente: `id`, `kind` (`Reply` o `Note`), `text`, `authorName` (el nombre al momento de escribirlo), `createdAt` |
| `createdAt`, `updatedAt` | Auditoría |

### `GET /api/inquiries`

Solo `AgencyAdmin`, `Supervisor` y `Seller`, y solo ven las de su inmobiliaria. Parámetros opcionales: `status` (`New` \| `InProgress` \| `Closed`), `search` (nombre, correo, teléfono o título de la propiedad), `page` (por defecto 1) y `pageSize` (por defecto 10, máximo 100). Orden: más recientes primero. Misma forma paginada que las demás listas.

### `GET /api/inquiries/{id}`

Devuelve un `Inquiry`. Una solicitud de otra inmobiliaria responde `404`.

### `POST /api/inquiries/{id}/follow-ups`

```json
{ "kind": "Reply", "text": "Le llamé y quedamos de visitarla el sábado." }
```

`kind` es `Reply` (lo que se le respondió al prospecto; por defecto) o `Note` (nota interna). `text` es obligatorio, máximo 2000. Devuelve el `Inquiry` actualizado. Una respuesta pasa la solicitud a `InProgress` (también reabre una `Closed`); una nota no cambia el estado. Máximo 200 registros por solicitud (`400` después). **El sistema no envía correos ni mensajes**: solo deja constancia; el personal contacta al prospecto por teléfono o correo.

### `PUT /api/inquiries/{id}/status`

```json
{ "status": "Closed" }
```

Devuelve el `Inquiry` actualizado. Un estado inválido responde `400`.

---

## Resumen de la inmobiliaria

### `GET /api/dashboard`

Lo que muestra la página de inicio del personal de una inmobiliaria (y del particular en su espacio). Solo cuenta lo de **su** inmobiliaria; el administrador de plataforma y los clientes reciben `403`.

```json
{
  "properties": { "total": 12, "published": 6, "draft": 2, "paused": 1, "closed": 3, "featured": 2 },
  "inquiries": { "new": 4, "inProgress": 2, "closed": 5, "receivedRecently": 3, "total": 11 },
  "recentDays": 7,
  "latestNewInquiries": [
    { "id": "<guid>", "propertyId": "<guid>", "propertyTitle": "Casa en Polanco", "name": "Ana López", "createdAt": "2026-11-25T17:30:00Z" }
  ]
}
```

- `properties`: no cuenta las eliminadas. `published` son las **activas** (visibles al público), `paused` las retiradas temporalmente y `closed` las vendidas o rentadas (el sistema no tiene un estado «cancelada»). `featured` son las destacadas en este momento y coincide con lo usado del paquete de destacados.
- `inquiries`: por etapa; `receivedRecently` son las recibidas en los últimos `recentDays` días (7), sin importar su etapa.
- `latestNewInquiries`: hasta 5 solicitudes **nuevas** (sin atender), la más reciente primero. No trae el mensaje ni los datos de contacto del prospecto: para eso está `GET /api/inquiries`.

**Errores:** `401`, `403`.
## Planes y suscripciones

Con esto se monetiza el portal. Los endpoints de planes y de cuentas son del administrador de plataforma; `GET /api/billing/me` es del personal de la inmobiliaria y `GET /api/billing/plans` y `POST /api/billing/checkout` del administrador de la inmobiliaria. Hay dos tipos de plan (`kind`):

- **`Listings`**: cuántas propiedades puede tener la cuenta. El plan **gratuito** es de este tipo, es para particulares y permite 3.
- **`Featured`**: un **paquete de destacados**. Su `limit` es cuántos anuncios pueden estar destacados **a la vez** mientras dura el paquete.

Un plan se contrata de dos maneras: la propia cuenta lo **compra con tarjeta** (`POST /api/billing/checkout`, pantalla `/mi-plan`) o el administrador de plataforma **registra el pago** que recibió fuera del sistema (`POST /api/billing/accounts/{agencyId}/subscriptions`). **La pasarela de pagos es una simulación**: no mueve dinero (ver [Compra de un plan](#compra-de-un-plan-con-tarjeta)).

En la aplicación web, lo contratado y lo usado (publicaciones y destacados, con su vencimiento) se muestra en la página de **Inicio** de todo el personal de una inmobiliaria, y solo el administrador ve el botón para comprar o cambiar de plan.

### Objeto `Plan`

| Campo | Tipo | Notas |
|---|---|---|
| `id` | guid | |
| `name` | texto | Obligatorio, máx. 100. Único entre los planes no eliminados (`409` si se repite) |
| `description` | texto o `null` | Máx. 500 |
| `kind` | texto | `Listings` (por defecto) o `Featured` |
| `audience` | texto | A quién se ofrece: `Individual` (particulares), `Company` (inmobiliarias, por defecto) o `Any` |
| `limit` | entero o `null` | 1 a 100,000. `null` = ilimitado. Valores usuales: publicaciones 3, 100, 200, 300, 500 o ilimitadas; destacados 1, 3, 5, 10, 50, 100, 200, 300, 500, 1000 o ilimitados |
| `period` | texto o `null` | Cuánto dura cada contratación: `Day`, `Week`, `Month` o `Year`. Obligatorio en un plan de pago; `null` en el gratuito, que no vence |
| `price`, `currency` | número, texto | `price` de 0 a 9,999,999,999.99; `currency` `MXN` (por defecto) o `USD` |
| `isActive` | booleano | Un plan inactivo no se puede contratar. `true` por defecto |
| `isFree` | booleano | Solo lectura: `price` es 0 |
| `createdAt`, `updatedAt` | fecha | |

**El plan gratuito** es el plan con `price` 0. Debe ser de tipo `Listings`, para `Individual`, con `limit` y activo, y no lleva `period`. **Solo puede haber uno** (`409` si se intenta crear otro), no se puede eliminar (`400`) ni desactivar, y no puede pasar a ser de pago (`400`). Es el que tienen por omisión los particulares sin contratación.

### `GET /api/plans`, `GET /api/plans/{id}`

Solo `PlatformAdmin`. `GET /api/plans` acepta `kind` (`Listings` o `Featured`; otro valor responde `400`) y devuelve la lista **sin paginar**, con el gratuito primero y después por tipo, tope y nombre. `404` si el plan no existe.

### `POST /api/plans`, `PUT /api/plans/{id}`

Solo `PlatformAdmin`. Cuerpo: los campos editables del objeto `Plan` (todos menos `id`, `isFree` y las fechas). **Respuesta `201` / `200`:** objeto `Plan`. **Errores:** `400` (validación, o reglas del plan gratuito y del periodo), `403`, `404`, `409` (nombre repetido, o un segundo plan gratuito).

Editar un plan **no cambia las contrataciones que ya existen**: cada una guarda una copia de lo que incluía y costaba.

### `DELETE /api/plans/{id}`

Solo `PlatformAdmin`. Eliminación **lógica**: el plan deja de ofrecerse, y las cuentas que ya lo contrataron lo conservan hasta que venza. **Respuesta `204`.** **Errores:** `400` (el plan gratuito no se elimina), `403`, `404`.

### Objeto `AccountBilling`

Lo que tiene contratado una cuenta y lo que usa.

| Campo | Tipo | Notas |
|---|---|---|
| `agencyId`, `agencyName` | guid, texto | |
| `isIndividual` | booleano | Cuenta gratuita de un particular |
| `listings.planName` | texto o `null` | Plan de publicaciones vigente. `null` si una **inmobiliaria** no tiene ninguno (sin tope: así funcionan las que existían antes de los planes) |
| `listings.limit` | entero o `null` | `null` = ilimitado |
| `listings.used` | entero | Propiedades que tiene la cuenta: **los borradores cuentan y las eliminadas no** |
| `listings.endsAt` | fecha o `null` | Cuándo vence el plan contratado. `null` en el gratuito y sin plan |
| `listings.isFree` | booleano | Si es el plan gratuito |
| `featured` | objeto o `null` | Paquete de destacados vigente: `planName`, `limit` (`null` = ilimitado), `used` (cuántas propiedades destacadas tiene ahora) y `endsAt`. `null` si no tiene |

### `GET /api/billing/me`

`AgencyAdmin`, `Supervisor` y `Seller` (también los particulares, que son `AgencyAdmin` de su espacio). Devuelve el `AccountBilling` de la propia cuenta. `403` para el administrador de plataforma y los clientes.

### `GET /api/billing/accounts`

Solo `PlatformAdmin`. Lista, sin paginar, el `AccountBilling` de **todas** las cuentas (inmobiliarias y particulares).

### Objeto `Subscription`

Una contratación: guarda una copia del plan al momento de registrarse.

| Campo | Tipo | Notas |
|---|---|---|
| `id`, `agencyId`, `planId` | guid | |
| `planName`, `kind`, `limit`, `period` | texto, texto, entero o `null`, texto | Copia de lo que incluía el plan |
| `amountPaid`, `currency` | número, texto | Lo que se pagó |
| `paymentReference` | texto o `null` | Folio, transferencia o nota. Máx. 200 |
| `startsAt`, `endsAt` | fecha | Dura exactamente un `period` desde el registro |
| `cancelledAt` | fecha o `null` | |
| `isActive` | booleano | Vigente ahora: no cancelada y antes de `endsAt` |
| `createdAt` | fecha | |

### `GET /api/billing/accounts/{agencyId}/subscriptions`

Solo `PlatformAdmin`. Historial de la cuenta, de la más reciente a la más antigua. `404` si la cuenta no existe.

### `POST /api/billing/accounts/{agencyId}/subscriptions`

Solo `PlatformAdmin`. Cuerpo: `{ "planId": "<guid>", "amountPaid": 799.00, "paymentReference": "SPEI-123" }`. `amountPaid` (de 0 a 9,999,999,999.99) y `paymentReference` son opcionales; sin `amountPaid` se toma el precio del plan.

- El plan debe estar activo, no ser el gratuito y ser para ese tipo de cuenta (`Any`, o `Individual` / `Company` según la cuenta): si no, `400`.
- **Reemplaza** la contratación vigente del **mismo tipo**: contratar otro plan de publicaciones cancela el anterior; contratar otro paquete de destacados cancela el anterior.
- Al reemplazarse o cancelarse un paquete de destacados, **las propiedades de la cuenta dejan de estar destacadas** y hay que elegirlas de nuevo con `PUT /api/properties/{id}/featured`.
- Al vencer un plan de publicaciones, la cuenta vuelve a su plan base: el gratuito si es particular; una inmobiliaria sin plan vuelve a no tener tope. Al vencer un paquete de destacados, las propiedades dejan de mostrarse destacadas.

**Respuesta `201`:** objeto `Subscription`. **Errores:** `400`, `403`, `404` (cuenta o plan inexistentes).

### `POST /api/billing/accounts/{agencyId}/subscriptions/{subscriptionId}/cancel`

Solo `PlatformAdmin`. Cancela de inmediato una contratación vigente (si era un paquete de destacados, quita el destacado de las propiedades de la cuenta). Es idempotente: cancelar una que ya estaba cancelada o vencida no cambia nada. **Respuesta `200`:** objeto `Subscription`. **Errores:** `403`, `404` (la contratación no existe o es de otra cuenta).

### Compra de un plan con tarjeta

El administrador de la inmobiliaria (`AgencyAdmin`, que incluye al particular en su espacio gratuito) compra un plan para **su propia cuenta**. `Supervisor`, `Seller` y `PlatformAdmin` reciben `403`.

#### `GET /api/billing/plans`

Los planes que esa cuenta puede comprar: **activos, de pago y para su tipo de cuenta** (`Individual`, `Company` o `Any`). El plan gratuito nunca aparece. Devuelve la lista de objetos `Plan`, sin paginar.

#### `POST /api/billing/checkout`

Cuerpo:

```json
{
  "planId": "<guid>",
  "card": { "number": "4242 4242 4242 4242", "holderName": "ANA LOPEZ", "expiryMonth": 12, "expiryYear": 2028, "cvv": "123" }
}
```

- **El precio sale del plan**, nunca del cliente: el cuerpo no tiene un campo de monto.
- Validación de la tarjeta (`400`): número de 12 a 19 dígitos con dígito de control correcto (Luhn; acepta espacios y guiones), titular obligatorio (máx. 100), mes 1 a 12, año de cuatro cifras y CVV de 3 o 4 dígitos. Los mensajes de error **no repiten** lo capturado.
- Un plan inexistente responde `404`; uno inactivo, el gratuito o de otro tipo de cuenta, `400`. Ninguno cobra.
- Una tarjeta **vencida** (vence al terminar el mes indicado) o **rechazada** por la pasarela responde **`402`** con un `detail` en español para mostrar, y **no cambia nada**: la contratación vigente se conserva.
- Si el cobro se aprueba, se crea la contratación con las mismas reglas que al registrarla el administrador de plataforma (empieza ahora, dura un periodo, **reemplaza** a la vigente del mismo tipo y, si es un paquete de destacados, quita el destacado a los anuncios). `amountPaid` es el precio del plan y `paymentReference` el identificador de la operación (`SIM-…`).

**Respuesta `201`:** objeto `Subscription`. **Errores:** `400`, `401`, `402`, `403`, `404`.

**La tarjeta no se guarda ni se escribe en registros** (los objetos que la llevan ocultan sus datos al imprimirse). Con una pasarela real lo normal es no recibirla nunca y usar un token del proveedor.

#### Pasarela simulada

`SimulatedPaymentGateway` implementa el puerto `IPaymentGateway`; cuando exista la pasarela real solo se reemplaza esa clase. Resultado según el número de tarjeta, como las tarjetas de prueba de Stripe:

| Tarjeta | Resultado |
|---|---|
| `4242 4242 4242 4242` (y cualquier otra con dígito de control válido) | Aprobada |
| `4000 0000 0000 0002` | Rechazada |
| `4000 0000 0000 9995` | Fondos insuficientes |
| `4000 0000 0000 0069` | Vencida |

**Seguridad:** la simulación aprueba sin cobrar, así que **fuera de Desarrollo la API no arranca** a menos que se configure `Payments:AllowSimulated=true`. Es una protección para que nadie despliegue la simulación por descuido y deje que cualquier administrador contrate planes gratis.
### Topes de publicaciones

`POST /api/properties` responde `422` cuando la cuenta ya tiene tantas propiedades como permite su plan de publicaciones vigente (`listings.limit`). Sin contratación vigente, el tope de un particular es el del plan gratuito (3 por omisión; `AccountLimits:IndividualMaxProperties` solo se usa si no existe un plan gratuito) y una inmobiliaria no tiene tope. El mensaje de `detail` está en español y sirve para mostrarse.

---
## Salud

### `GET /health`

Público. Responde `200` con el texto `Healthy` cuando el servicio está en marcha. No comprueba la base de datos.

---

## Errores

Los errores siguen el formato `ProblemDetails` (RFC 9457).

| Código | Cuándo ocurre |
|---|---|
| `400` | Datos inválidos o regla de negocio violada |
| `401` | Sin token, token inválido o vencido, o credenciales incorrectas en el login |
| `403` | El rol no permite la operación, o una restricción de negocio (por ejemplo, eliminar la propia cuenta) |
| `404` | El recurso no existe o no es visible para el usuario |
| `409` | Conflicto, por ejemplo correo duplicado, nombre de plan repetido o un segundo plan gratuito |
| `402` | La pasarela rechazó el pago (tarjeta rechazada, sin fondos o vencida); `detail` trae el motivo para mostrar |
| `422` | Se alcanzó un tope del plan de la cuenta (propiedades, o anuncios destacados a la vez); `detail` trae un mensaje para mostrar |
| `429` | Límite de solicitudes superado (login, alta de cuenta, solicitudes de informes o catálogo público) |
| `500` | Error interno. El detalle no se expone; solo se registra en el servidor. |

**Error general**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You cannot assign the platform administrator role.",
  "traceId": "00-ea8b21acc83dbc7908542f6e9f35027e-50749f4d7c46ac7c-02"
}
```

**Error de validación (`400`):** agrupa los mensajes por campo.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "errors": {
    "FirstName": ["'First Name' must not be empty."],
    "Email": ["'Email' is not a valid email address."],
    "Password": [
      "The password must be at least 10 characters long.",
      "The password must contain an uppercase letter."
    ]
  },
  "traceId": "00-9fc0e3cb5f15e868575db979864b6acf-46fcc5a918ad7d10-02"
}
```

> Los mensajes de la API están en inglés. El frontend los traduce según el código HTTP y muestra los detalles de validación del servidor tal como llegan.

## CORS

La API solo acepta peticiones del navegador desde los orígenes de `Cors:AllowedOrigins` (por defecto `http://localhost:4200`), con los encabezados `Authorization` y `Content-Type` y los métodos `GET`, `POST`, `PUT`, `DELETE` y `OPTIONS`.

## Ejemplo con curl

```powershell
# 1. Iniciar sesión
$body = @{ email = "admin@alhecasa.local"; password = "<contraseña>" } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri http://localhost:5156/api/auth/login `
  -ContentType "application/json" -Body $body

# 2. Listar usuarios
Invoke-RestMethod -Uri "http://localhost:5156/api/users?pageSize=5" `
  -Headers @{ Authorization = "Bearer $($login.accessToken)" }
```
