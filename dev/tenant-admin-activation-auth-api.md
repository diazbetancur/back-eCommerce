# Tenant Admin Activation + Auth API Contract

Contrato real implementado en backend para la Fase 1.1.

Este documento describe el comportamiento observado en codigo y runtime. No propone formatos ideales ni asume validaciones que el backend no hace hoy.

## Notas globales

- Los endpoints tenant-scoped usan `X-Tenant-Slug` como header principal.
- El middleware tenant tambien acepta `?tenant=<slug>` como fallback.
- Los endpoints tenant-scoped pueden fallar antes del handler por `TenantResolutionMiddleware`.
- Los errores del middleware NO usan `ProblemDetails`; responden JSON plano.
- Muchos errores del handler SI usan `ProblemDetails`.
- `ProblemDetails.Title` y `ProblemDetails.Detail` pasan por el localizador global de errores en `Program.cs`, por lo que pueden salir traducidos.
- `POST /auth/login` existe. `POST /api/auth/login` no existe.
- `POST /provision/tenants/init` usa el record de `CC.Domain.Dto.InitProvisioningRequest`, no el DTO con DataAnnotations de `CC.Aplication.Provisioning.InitProvisioningRequest`.

## Errores comunes del middleware tenant

Estos errores aplican a endpoints tenant-scoped como `POST /auth/login`, `POST /api/auth/activate-account`, `POST /api/auth/forgot-password` y `POST /api/auth/reset-password`.

### Tenant header faltante

Status: `400 Bad Request`

```json
{
  "error": "Tenant Required",
  "detail": "This endpoint requires a tenant. Provide tenant slug via X-Tenant-Slug header or ?tenant query parameter",
  "path": "/api/auth/activate-account"
}
```

### Tenant inexistente

Status: `404 Not Found`

```json
{
  "error": "Tenant Not Found",
  "detail": "No tenant found with slug 'foo'",
  "slug": "foo"
}
```

### Tenant no disponible para la ruta

Status: `503 Service Unavailable`

```json
{
  "error": "Tenant Not Available",
  "detail": "Tenant 'foo' is currently unavailable",
  "status": "PendingActivation",
  "slug": "foo"
}
```

Nota:

- Para `POST /api/auth/activate-account`, `POST /api/auth/forgot-password` y `POST /api/auth/reset-password`, el middleware permite `PendingActivation` y `Active`.
- Para `POST /auth/login`, el middleware solo permite `Active`.

## 1. Crear tenant desde superadmin

| Campo                            | Contrato real                                                                                                                                                                                                                                                                                                                                                                        |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Metodo / ruta                    | `POST /superadmin/tenants/`                                                                                                                                                                                                                                                                                                                                                          |
| Headers                          | `Authorization: Bearer <admin-jwt>` requerido. `Content-Type: application/json`. No usa `X-Tenant-Slug`.                                                                                                                                                                                                                                                                             |
| Body                             | JSON con `slug`, `name`, `planCode`, `adminEmail`. Los 4 campos son esperados. No hay campos opcionales.                                                                                                                                                                                                                                                                             |
| Campos obligatorios / opcionales | Obligatorios: `slug`, `name`, `planCode`, `adminEmail`.                                                                                                                                                                                                                                                                                                                              |
| Success response                 | `201 Created` con body `{ slug, status, adminEmail, activationNotificationAccepted, message }` y header `Location: /superadmin/tenants/{slug}`.                                                                                                                                                                                                                                      |
| Error responses                  | `400 ValidationProblemDetails` para slug invalido. `400 ProblemDetails` para adminEmail invalido. `404 ProblemDetails` para plan no encontrado. `409 ProblemDetails` para slug existente. `409 ProblemDetails` si la DB fisica ya existe. `401 ProblemDetails` si falta auth. `403 ProblemDetails` si el token no tiene rol `SuperAdmin`. `500 ProblemDetails` en excepcion general. |
| Notas para frontend              | No devuelve password temporal. `status` sale como string del enum. `activationNotificationAccepted` puede ser `false` y la creacion igual responder `201`.                                                                                                                                                                                                                           |

### Body de request

```json
{
  "slug": "mi-tienda-nueva",
  "name": "Mi Tienda Nueva",
  "planCode": "Basic",
  "adminEmail": "admin@mitienda.com"
}
```

### Success response

```json
{
  "slug": "qa-1777305030",
  "status": "PendingActivation",
  "adminEmail": "qa-1777305030@example.com",
  "activationNotificationAccepted": true,
  "message": "Tenant created successfully. Admin activation is pending."
}
```

### Error responses reales

#### Slug invalido

Status: `400 Bad Request`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "slug": [
      "Formato invalido: debe contener solo letras minusculas, numeros y guiones, con minimo 3 caracteres."
    ]
  }
}
```

#### Email admin invalido

Status: `400 Bad Request`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Solicitud invalida",
  "status": 400,
  "detail": "El correo del administrador es obligatorio y debe tener un formato valido."
}
```

#### Slug existente

Status: `409 Conflict`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflicto de negocio",
  "status": 409,
  "detail": "Ya existe un tenant con el slug 'mi-tienda'.",
  "existingStatus": "PendingActivation"
}
```

### Codigos de error reales

- No devuelve campo `code` en los errores propios de este endpoint.
- Status posibles: `400`, `401`, `403`, `404`, `409`, `500`.

## 2. Provisioning init

Este endpoint parece mas orientado a scripts o backoffice que a frontend publico, pero el contrato real es este.

| Campo                            | Contrato real                                                                                                                                                                                                                  |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Metodo / ruta                    | `POST /provision/tenants/init`                                                                                                                                                                                                 |
| Headers                          | `Content-Type: application/json`. No requiere `Authorization`. No usa `X-Tenant-Slug`.                                                                                                                                         |
| Body                             | JSON con `slug`, `name`, `plan`, `adminEmail`.                                                                                                                                                                                 |
| Campos obligatorios / opcionales | En la practica, el endpoint espera los 4. No hay manejo seguro cuando falta `adminEmail`.                                                                                                                                      |
| Success response                 | `200 OK` con `{ provisioningId, confirmToken, next, message }`.                                                                                                                                                                |
| Error responses                  | `400 ProblemDetails` para plan fuera de lista permitida. `400 ProblemDetails` para plan no encontrado en DB. `409 ProblemDetails` para slug ya existente. `500 ProblemDetails` ante excepcion, incluido `adminEmail` faltante. |
| Notas para frontend              | `planCode` NO aplica aqui; aqui el campo se llama `plan`. Devuelve `confirmToken` plano. Si falta `adminEmail`, hoy responde `500` por bug.                                                                                    |

### Body de request

```json
{
  "slug": "prov-ok-1777305768",
  "name": "Provision Ok",
  "plan": "Basic",
  "adminEmail": "prov-ok-1777305768@example.com"
}
```

### Success response

```json
{
  "provisioningId": "b3d6612b-d029-463b-a043-cf3bfeb09d81",
  "confirmToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "next": "/provision/tenants/confirm",
  "message": "Provisioning initialized. Use the confirmation token within 15 minutes to proceed."
}
```

### Error responses reales

#### Plan no permitido

Status: `400 Bad Request`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid Plan",
  "status": 400,
  "detail": "Plan 'Foo' is not allowed. Allowed plans: Basic, Premium, Enterprise"
}
```

#### AdminEmail faltante hoy

Status: `500 Internal Server Error`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Error interno del servidor",
  "status": 500,
  "detail": "An error occurred while initializing provisioning"
}
```

### Codigos de error reales

- No devuelve campo `code`.
- Status posibles: `400`, `409`, `500`.

### Hallazgo backend

- El bug esta en `PrimaryAdminEmail = request.AdminEmail.Trim().ToLower()` dentro de `InitProvisioning`.
- Correccion minima sugerida: validar `string.IsNullOrWhiteSpace(request.AdminEmail)` antes de usar `Trim()`, y devolver `400` consistente.

## 3. Activar cuenta

| Campo                            | Contrato real                                                                                                                                |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Metodo / ruta                    | `POST /api/auth/activate-account`                                                                                                            |
| Headers                          | `Content-Type: application/json`. `X-Tenant-Slug` requerido en la practica. Fallback tecnico: `?tenant=<slug>`. No requiere `Authorization`. |
| Body                             | JSON con `token`, `password`, `confirmPassword`.                                                                                             |
| Campos obligatorios / opcionales | Los 3 son esperados. No hay validacion DTO explicita; las reglas salen de la logica del servicio.                                            |
| Success response                 | `200 OK` con `{ success, message }`.                                                                                                         |
| Error responses                  | Errores del middleware tenant o `ProblemDetails` desde el handler con campo extra `code`.                                                    |
| Notas para frontend              | Este endpoint acepta tenants en `PendingActivation` o `Active`. Success y error no tienen el mismo formato.                                  |

### Body de request

```json
{
  "token": "TOKEN_RECIBIDO_POR_EMAIL",
  "password": "QaSecure123!",
  "confirmPassword": "QaSecure123!"
}
```

### Success response real

```json
{
  "success": true,
  "message": "Cuenta activada correctamente."
}
```

### Error response real por reuse

Status: `409 Conflict`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Account Activation Failed",
  "status": 409,
  "detail": "Security token was already used.",
  "code": "USED_ACTIVATION_TOKEN"
}
```

### Error response real por tenant incorrecto

Status: `409 Conflict`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Account Activation Failed",
  "status": 409,
  "detail": "The activation token is not valid for the current tenant.",
  "code": "TENANT_MISMATCH"
}
```

### Codigos de error reales

| code                             | Status | Observacion                                                       |
| -------------------------------- | -----: | ----------------------------------------------------------------- |
| `PASSWORD_CONFIRMATION_MISMATCH` |    400 | Password y confirmPassword no coinciden                           |
| `PASSWORD_POLICY_NOT_MET`        |    400 | Password menor a 8 o vacio                                        |
| `INVALID_ACTIVATION_TOKEN`       |    400 | Token invalido o tenant/user no encontrados                       |
| `EXPIRED_ACTIVATION_TOKEN`       |    400 | Token expirado                                                    |
| `TENANT_REQUIRED`                |    400 | El servicio lo define, pero normalmente el middleware corta antes |
| `TENANT_MISMATCH`                |    409 | Token de otro tenant                                              |
| `USED_ACTIVATION_TOKEN`          |    409 | Token ya usado                                                    |
| `REVOKED_ACTIVATION_TOKEN`       |    409 | Token revocado                                                    |
| `TENANT_NOT_PENDING_ACTIVATION`  |    409 | Tenant con estado no permitido                                    |
| `USER_NOT_PENDING_ACTIVATION`    |    409 | Usuario no esta pendiente                                         |
| `TENANT_SYNC_FAILED`             |    500 | Error al guardar cambios en TenantDb                              |

## 4. Forgot password

| Campo                            | Contrato real                                                                                                                                |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Metodo / ruta                    | `POST /api/auth/forgot-password`                                                                                                             |
| Headers                          | `Content-Type: application/json`. `X-Tenant-Slug` requerido en la practica. Fallback tecnico: `?tenant=<slug>`. No requiere `Authorization`. |
| Body                             | JSON con `email`.                                                                                                                            |
| Campos obligatorios / opcionales | `email` se espera en el body, pero si llega vacio o no encuentra usuario, igual devuelve success generico.                                   |
| Success response                 | `200 OK` con `{ success, message }`.                                                                                                         |
| Error responses                  | No tiene errores propios del handler. Solo errores del middleware tenant o excepciones no controladas externas.                              |
| Notas para frontend              | La respuesta siempre es generica. No permite distinguir si se reenvio activacion, si se genero `PasswordReset`, o si el usuario no existe.   |

### Body de request

```json
{
  "email": "qa-1777305030@example.com"
}
```

### Success response real

```json
{
  "success": true,
  "message": "Si la cuenta existe, enviaremos instrucciones al correo asociado."
}
```

### Comportamiento real segun estado

- Si el usuario existe y esta `PendingActivation`, y ademas es el `PrimaryAdminUserId` del tenant, genera un nuevo `TenantAdminActivation` y reenvia activacion.
- Si el usuario existe y esta `Active`, genera un `PasswordReset` y dispara `PASSWORD_RESET`.
- Si no existe o falta email, responde el mismo success generico.

### Codigos de error reales

- No devuelve campo `code`.
- Error observable principal: errores del middleware tenant (`400`, `404`, `503`).

## 5. Reset password

| Campo                            | Contrato real                                                                                                                                |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Metodo / ruta                    | `POST /api/auth/reset-password`                                                                                                              |
| Headers                          | `Content-Type: application/json`. `X-Tenant-Slug` requerido en la practica. Fallback tecnico: `?tenant=<slug>`. No requiere `Authorization`. |
| Body                             | JSON con `token`, `password`, `confirmPassword`.                                                                                             |
| Campos obligatorios / opcionales | Los 3 son esperados. Las reglas salen de la logica del servicio.                                                                             |
| Success response                 | `200 OK` con `{ success, message }`.                                                                                                         |
| Error responses                  | Errores del middleware tenant o `ProblemDetails` con campo `code`.                                                                           |
| Notas para frontend              | El formato de exito es igual al de activate-account.                                                                                         |

### Body de request

```json
{
  "token": "PASSWORD_RESET_TOKEN",
  "password": "NuevaClave123!",
  "confirmPassword": "NuevaClave123!"
}
```

### Success response

```json
{
  "success": true,
  "message": "Contrase\u00f1a actualizada correctamente."
}
```

### Error response de ejemplo

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Password Reset Failed",
  "status": 409,
  "detail": "Security token was already used.",
  "code": "USED_PASSWORD_RESET_TOKEN"
}
```

### Codigos de error reales

| code                             | Status | Observacion                                                  |
| -------------------------------- | -----: | ------------------------------------------------------------ |
| `PASSWORD_CONFIRMATION_MISMATCH` |    400 | Password y confirmPassword no coinciden                      |
| `PASSWORD_POLICY_NOT_MET`        |    400 | Password menor a 8 o vacio                                   |
| `INVALID_PASSWORD_RESET_TOKEN`   |    400 | Token invalido                                               |
| `EXPIRED_PASSWORD_RESET_TOKEN`   |    400 | Token expirado                                               |
| `TENANT_REQUIRED`                |    400 | Definido por servicio; normalmente el middleware corta antes |
| `TENANT_MISMATCH`                |    409 | Token de otro tenant                                         |
| `USED_PASSWORD_RESET_TOKEN`      |    409 | Token ya usado                                               |
| `REVOKED_PASSWORD_RESET_TOKEN`   |    409 | Token revocado                                               |
| `USER_NOT_ACTIVE`                |    409 | Solo permite reset a usuario `Active`                        |

## 6. Login posterior a activacion

| Campo                            | Contrato real                                                                                                                                |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Metodo / ruta                    | `POST /auth/login`                                                                                                                           |
| Headers                          | `Content-Type: application/json`. `X-Tenant-Slug` requerido en la practica. Fallback tecnico: `?tenant=<slug>`. No requiere `Authorization`. |
| Body                             | JSON con `email`, `password`.                                                                                                                |
| Campos obligatorios / opcionales | Ambos obligatorios.                                                                                                                          |
| Success response                 | `200 OK` con `{ token, expiresAt, user }`.                                                                                                   |
| Error responses                  | Errores del middleware tenant o `ProblemDetails` del handler.                                                                                |
| Notas para frontend              | El contrato de success no cambio por `UserStatus`. Lo que cambio es que el login solo pasa si `IsActive == true` y `Status == Active`.       |

### Body de request

```json
{
  "email": "qa-1777305030@example.com",
  "password": "QaSecure123!"
}
```

### Success response real

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-04-28T16:00:20.831004Z",
  "user": {
    "userId": "ed82158d-76f5-4d48-b4b0-774a258a3419",
    "email": "qa-1777305030@example.com",
    "firstName": "Admin",
    "lastName": "System",
    "phoneNumber": null,
    "roles": ["SuperAdmin"],
    "modules": [
      "catalog",
      "customers",
      "dashboard",
      "inventory",
      "loyalty",
      "orders",
      "permissions",
      "settings",
      "users"
    ],
    "permissions": [
      {
        "moduleCode": "inventory",
        "moduleName": "Inventario",
        "iconName": "warehouse",
        "canView": true,
        "canCreate": true,
        "canUpdate": true,
        "canDelete": true
      }
    ],
    "isActive": true,
    "mustChangePassword": false,
    "features": {
      "allowGuestCheckout": true,
      "enableExpressCheckout": false,
      "showStock": true,
      "hasVariants": false,
      "enableMultiStore": false,
      "enableWishlist": false,
      "enableReviews": false,
      "enableCartSave": false,
      "maxCartItems": 50,
      "enableAdvancedSearch": false,
      "enableAnalytics": false,
      "enableNewsletterSignup": false,
      "loyaltyEnabled": true,
      "payments": {
        "wompiEnabled": false,
        "stripeEnabled": false,
        "payPalEnabled": false,
        "cashOnDelivery": true
      }
    }
  }
}
```

### Error responses reales

#### Validacion basica

Status: `400 Bad Request`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Email and password are required"
}
```

#### Credenciales invalidas o estado de cuenta

Status: `401 Unauthorized`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Authentication Failed",
  "status": 401,
  "detail": "Invalid email or password"
}
```

### Errores reales observables

- `400` ProblemDetails: `Validation Error`
- `401` ProblemDetails: `Authentication Failed`
- `409` ProblemDetails: `Tenant Not Resolved`
- `500` ProblemDetails: `Internal Server Error`
- `400`, `404`, `503` JSON plano del middleware tenant

### Nota importante para frontend

- Aunque `UnifiedAuthService` tiene el mensaje `Account is pending activation`, el flujo real para un tenant aun `PendingActivation` normalmente no llega al handler de login porque el middleware tenant devuelve antes `503 Tenant Not Available` para `/auth/login`.

## Inconsistencias reales a reportar

1. `POST /superadmin/tenants/` usa `planCode`, pero `POST /provision/tenants/init` usa `plan`.
2. Los endpoints tenant-scoped mezclan dos formatos de error:
   - JSON plano del middleware tenant
   - `ProblemDetails` del handler
3. `POST /api/auth/activate-account`, `POST /api/auth/forgot-password` y `POST /api/auth/reset-password` devuelven success como `{ success, message }`, mientras `POST /auth/login` devuelve `{ token, expiresAt, user }`.
4. `POST /provision/tenants/init` declara `AdminEmail` como requerido a nivel conceptual, pero hoy falta validacion segura y termina en `500` si no se envia.
5. `TenantResolver` implementa un `423 Locked` para tenant no listo, pero en requests normales manda primero `TenantResolutionMiddleware`, que responde `503 Service Unavailable` para esas rutas.
