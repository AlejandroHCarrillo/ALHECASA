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
| `GET` | `/api/auth/me` | Autenticado | Usuario de la sesión |
| `GET` | `/api/users` | `PlatformAdmin`, `AgencyAdmin` | Lista paginada con búsqueda y filtros |
| `GET` | `/api/users/{id}` | `PlatformAdmin`, `AgencyAdmin` | Detalle de un usuario |
| `POST` | `/api/users` | `PlatformAdmin`, `AgencyAdmin` | Crear usuario |
| `PUT` | `/api/users/{id}` | `PlatformAdmin`, `AgencyAdmin` | Actualizar usuario |
| `DELETE` | `/api/users/{id}` | `PlatformAdmin`, `AgencyAdmin` | Eliminar usuario (borrado lógico) |
| `GET` | `/api/agencies` | `PlatformAdmin`, `AgencyAdmin` | Inmobiliarias visibles para el usuario |

## Autenticación

Todos los endpoints, salvo `/health` y `/api/auth/login`, exigen el encabezado:

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
| `Client` | Anunciante particular (sin inmobiliaria) o usuario de una inmobiliaria. Sin acceso a los endpoints de administración. |

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

### `GET /api/auth/me`

Devuelve el usuario de la sesión, volviendo a consultarlo en la base de datos.

**Respuesta `200`:** el mismo objeto `user` del login.

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
| `role` | `PlatformAdmin` \| `AgencyAdmin` \| `Client` | | Filtra por rol. Un valor inválido responde `400`. |
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
| `role` | `PlatformAdmin` \| `AgencyAdmin` \| `Client` | Obligatorio |
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
| `PlatformAdmin` | Elige libremente. `AgencyAdmin` exige `agencyId`; `Client` puede ir sin `agencyId` (particular); `PlatformAdmin` ignora `agencyId` y queda sin inmobiliaria. |
| `AgencyAdmin` | El usuario queda siempre en la inmobiliaria del que crea. No puede crear un `PlatformAdmin` (`403`) ni indicar otra inmobiliaria (`403`). |

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
| `role` | `PlatformAdmin` \| `AgencyAdmin` \| `Client` | Obligatorio |
| `agencyId` | Guid \| null | Mismas reglas que al crear |
| `isActive` | boolean | Estado del usuario |

**Restricciones**

- Nadie puede cambiar **su propio rol** ni **desactivar su propia cuenta** (`403`).
- Un `AgencyAdmin` no puede mover usuarios a otra inmobiliaria (`403`) ni asignar el rol `PlatformAdmin` (`403`). Los usuarios de otras inmobiliarias y los `PlatformAdmin` no le son visibles, así que recibe `404`.

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
    "isActive": true
  }
]
```

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
| `409` | Conflicto, por ejemplo correo duplicado |
| `429` | Límite de intentos del login superado |
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
