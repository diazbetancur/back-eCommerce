# ?? Implementación de Autenticación por Tenant con JWT

## ?? Resumen

Se ha implementado un sistema completo de autenticación de usuarios por tenant con las siguientes características:

- ? Registro de usuarios por tenant
- ? Login con JWT
- ? Perfil de usuario autenticado
- ? Hasheo seguro de contraseñas (PBKDF2 con 100,000 iteraciones)
- ? Email único por tenant
- ? JWT con claims de tenant

---

## ??? Arquitectura

### **Entidades Creadas**

#### 1. **UserAccount** (CC.Domain/Users/UserAccount.cs)
```csharp
public class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; }           // Unique per tenant
    public string PasswordHash { get; set; }    // PBKDF2 hash
    public string PasswordSalt { get; set; }    // Random salt
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public UserProfile? Profile { get; set; }   // 1:1 relationship
}
```

#### 2. **UserProfile** (CC.Domain/Users/UserProfile.cs)
```csharp
public class UserProfile
{
    public Guid Id { get; set; }                // FK to UserAccount
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    
    public UserAccount UserAccount { get; set; }
}
```

### **Servicios**

#### **AuthService** (CC.Aplication/Auth/AuthService.cs)
- `RegisterAsync()` - Crea nueva cuenta de usuario
- `LoginAsync()` - Autentica y genera JWT
- `GetUserProfileAsync()` - Obtiene perfil completo

### **Endpoints**

#### **TenantAuthEndpoints** (Api-eCommerce/Auth/TenantAuthEndpoints.cs)
- `POST /auth/register` - Registro de usuario
- `POST /auth/login` - Autenticación
- `GET /auth/me` - Perfil del usuario autenticado (requiere JWT)

---

## ?? Seguridad

### **Hash de Contraseñas**

Se utiliza **PBKDF2** con las siguientes configuraciones:
- **Algoritmo:** HMACSHA256
- **Iteraciones:** 100,000
- **Salt:** 16 bytes aleatorios (único por usuario)
- **Hash:** 32 bytes

### **JWT**

#### Claims Incluidos
```json
{
  "sub": "user-id-guid",
  "email": "user@example.com",
  "jti": "unique-token-id",
  "tenant_id": "tenant-guid",
  "tenant_slug": "tenant-slug",
  "exp": 1733155200,
  "iat": 1733068800
}
```

#### Configuración
- **Algoritmo:** HS256 (HMAC SHA-256)
- **Expiración:** 24 horas
- **Clave:** Configurada en `appsettings.json` (`jwtKey`)

---

## ?? API Endpoints

### 1. Register User

**Endpoint:** `POST /auth/register`  
**Headers:**
- `X-Tenant-Slug: your-tenant` (required)
- `Content-Type: application/json`

#### Request Body

```typescript
interface RegisterRequest {
  email: string;           // Valid email format
  password: string;        // Min 8 characters
  firstName: string;
  lastName: string;
  phoneNumber?: string;    // Optional
}
```

```json
{
  "email": "john.doe@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}
```

#### Response (200 OK)

```typescript
interface AuthResponse {
  token: string;           // JWT token
  expiresAt: string;       // ISO 8601 datetime
  user: UserDto;
}

interface UserDto {
  id: string;              // UUID
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  createdAt: string;       // ISO 8601
  isActive: boolean;
}
```

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJlbWFpbCI6ImpvaG4uZG9lQGV4YW1wbGUuY29tIiwidGVuYW50X2lkIjoiYTFiMmMzZDQtZTVmNi03ODkwLWFiY2QtZWYxMjM0NTY3ODkwIiwidGVuYW50X3NsdWciOiJteS1zdG9yZSIsImV4cCI6MTczMzE1NTIwMCwiaWF0IjoxNzMzMDY4ODAwfQ.signature",
  "expiresAt": "2024-12-02T15:30:00Z",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "+1234567890",
    "createdAt": "2024-12-01T15:30:00Z",
    "isActive": true
  }
}
```

#### Error Responses

**400 Bad Request** - Validation errors
```json
{
  "title": "Validation Error",
  "status": 400,
  "detail": "Password must be at least 8 characters long"
}
```

**409 Conflict** - Email already exists
```json
{
  "title": "Email Already Exists",
  "status": 409,
  "detail": "Email already registered"
}
```

---

### 2. Login

**Endpoint:** `POST /auth/login`  
**Headers:**
- `X-Tenant-Slug: your-tenant` (required)
- `Content-Type: application/json`

#### Request Body

```typescript
interface LoginRequest {
  email: string;
  password: string;
}
```

```json
{
  "email": "john.doe@example.com",
  "password": "SecurePass123!"
}
```

#### Response (200 OK)

Same structure as Register response (`AuthResponse`).

```json
{
  "token": "eyJhbGc...",
  "expiresAt": "2024-12-02T15:30:00Z",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "+1234567890",
    "createdAt": "2024-12-01T15:30:00Z",
    "isActive": true
  }
}
```

#### Error Responses

**401 Unauthorized** - Invalid credentials
```json
{
  "title": "Authentication Failed",
  "status": 401,
  "detail": "Invalid email or password"
}
```

**401 Unauthorized** - Account disabled
```json
{
  "title": "Authentication Failed",
  "status": 401,
  "detail": "Account is disabled"
}
```

---

### 3. Get User Profile

**Endpoint:** `GET /auth/me`  
**Headers:**
- `X-Tenant-Slug: your-tenant` (required)
- `Authorization: Bearer {token}` (required)

#### Request Body

None

#### Response (200 OK)

```typescript
interface UserProfileDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  documentType: string | null;
  documentNumber: string | null;
  birthDate: string | null;    // ISO 8601
  address: string | null;
  city: string | null;
  country: string | null;
  createdAt: string;            // ISO 8601
  isActive: boolean;
}
```

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "documentType": null,
  "documentNumber": null,
  "birthDate": null,
  "address": null,
  "city": null,
  "country": null,
  "createdAt": "2024-12-01T15:30:00Z",
  "isActive": true
}
```

#### Error Responses

**401 Unauthorized** - Missing or invalid token
```json
{
  "title": "Invalid Token",
  "status": 401,
  "detail": "User ID not found in token"
}
```

**404 Not Found** - User not found
```json
{
  "title": "User Not Found",
  "status": 404,
  "detail": "User not found"
}
```

---

## ?? Ejemplos de Integración

### TypeScript/JavaScript

```typescript
// 1. Register new user
async function registerUser() {
  const response = await fetch('https://api.example.com/auth/register', {
    method: 'POST',
    headers: {
      'X-Tenant-Slug': 'my-store',
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      email: 'john.doe@example.com',
      password: 'SecurePass123!',
      firstName: 'John',
      lastName: 'Doe',
      phoneNumber: '+1234567890'
    })
  });

  const data = await response.json();
  
  if (response.ok) {
    // Save token
    localStorage.setItem('auth_token', data.token);
    localStorage.setItem('auth_expires', data.expiresAt);
    console.log('User registered:', data.user);
  } else {
    console.error('Registration failed:', data.detail);
  }
}

// 2. Login
async function loginUser() {
  const response = await fetch('https://api.example.com/auth/login', {
    method: 'POST',
    headers: {
      'X-Tenant-Slug': 'my-store',
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      email: 'john.doe@example.com',
      password: 'SecurePass123!'
    })
  });

  const data = await response.json();
  
  if (response.ok) {
    // Save token
    localStorage.setItem('auth_token', data.token);
    localStorage.setItem('auth_expires', data.expiresAt);
    console.log('User logged in:', data.user);
  } else {
    console.error('Login failed:', data.detail);
  }
}

// 3. Get profile (authenticated request)
async function getUserProfile() {
  const token = localStorage.getItem('auth_token');
  
  const response = await fetch('https://api.example.com/auth/me', {
    method: 'GET',
    headers: {
      'X-Tenant-Slug': 'my-store',
      'Authorization': `Bearer ${token}`
    }
  });

  const profile = await response.json();
  
  if (response.ok) {
    console.log('User profile:', profile);
  } else {
    console.error('Failed to get profile:', profile.detail);
    // Token might be expired, redirect to login
    if (response.status === 401) {
      localStorage.removeItem('auth_token');
      window.location.href = '/login';
    }
  }
}

// 4. Helper: Check if token is valid
function isTokenValid(): boolean {
  const token = localStorage.getItem('auth_token');
  const expires = localStorage.getItem('auth_expires');
  
  if (!token || !expires) {
    return false;
  }
  
  return new Date(expires) > new Date();
}

// 5. Helper: Logout
function logout() {
  localStorage.removeItem('auth_token');
  localStorage.removeItem('auth_expires');
  window.location.href = '/login';
}
```

### cURL Examples

```bash
# 1. Register
curl -X POST https://api.example.com/auth/register \
  -H "X-Tenant-Slug: my-store" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "password": "SecurePass123!",
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "+1234567890"
  }'

# 2. Login
curl -X POST https://api.example.com/auth/login \
  -H "X-Tenant-Slug: my-store" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "password": "SecurePass123!"
  }'

# 3. Get Profile (replace {token} with actual JWT)
curl -X GET https://api.example.com/auth/me \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer {token}"
```

---

## ??? Base de Datos

### Tablas Creadas en TenantDb

#### UserAccounts
```sql
CREATE TABLE "UserAccounts" (
    "Id" UUID PRIMARY KEY,
    "Email" VARCHAR(255) NOT NULL,
    "PasswordHash" VARCHAR(500) NOT NULL,
    "PasswordSalt" VARCHAR(500) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NULL,
    CONSTRAINT "UQ_UserAccounts_Email" UNIQUE ("Email")
);

CREATE INDEX "IX_UserAccounts_Email" ON "UserAccounts" ("Email");
```

#### UserProfiles
```sql
CREATE TABLE "UserProfiles" (
    "Id" UUID PRIMARY KEY,
    "FirstName" VARCHAR(100) NOT NULL,
    "LastName" VARCHAR(100) NOT NULL,
    "PhoneNumber" VARCHAR(50) NULL,
    "DocumentType" VARCHAR(50) NULL,
    "DocumentNumber" VARCHAR(100) NULL,
    "BirthDate" DATE NULL,
    "Address" VARCHAR(500) NULL,
    "City" VARCHAR(100) NULL,
    "Country" VARCHAR(100) NULL,
    CONSTRAINT "FK_UserProfiles_UserAccounts" 
        FOREIGN KEY ("Id") REFERENCES "UserAccounts" ("Id") 
        ON DELETE CASCADE
);
```

---

## ?? Próximos Pasos para Migraciones

### Crear Migración

```bash
# Crear migración para las nuevas tablas
dotnet ef migrations add AddUserAccountsAndProfiles \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context TenantDbContext \
  --output-dir Tenant/Migrations

# Aplicar a una base de datos específica (para testing)
dotnet ef database update \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context TenantDbContext \
  --connection "Host=localhost;Database=tenant_test;..."
```

### Aplicar a Tenants Existentes

Las migraciones se aplicarán automáticamente durante el provisioning de nuevos tenants. Para tenants existentes:

1. **Opción 1:** Usar el endpoint de repair:
```bash
POST /superadmin/tenants/repair
{ "tenant": "existing-tenant-slug" }
```

2. **Opción 2:** Script manual:
```sql
-- Ejecutar en cada base de datos de tenant existente
-- (Las migraciones generadas por EF Core)
```

---

## ? Checklist de Implementación

- [x] Entidades `UserAccount` y `UserProfile`
- [x] DTOs para auth (RegisterRequest, LoginRequest, AuthResponse, etc.)
- [x] `AuthService` con hasheo seguro de passwords
- [x] Generación de JWT con claims de tenant
- [x] Endpoints `/auth/register`, `/auth/login`, `/auth/me`
- [x] Validaciones de entrada
- [x] Manejo de errores apropiado
- [x] Integración con `TenantDbContext`
- [x] Registro de servicios en `Program.cs`
- [x] Documentación completa

### Pendientes

- [ ] Crear y aplicar migraciones de base de datos
- [ ] Testing de endpoints
- [ ] Actualizar README_API.md con los nuevos endpoints
- [ ] Implementar refresh tokens (opcional)
- [ ] Implementar cambio de contraseña
- [ ] Implementar recuperación de contraseña (email)

---

## ?? Convivencia con Guest Checkout

El sistema de autenticación **NO afecta** el checkout de invitados:

- **Usuarios registrados:** Pueden hacer checkout con su cuenta (JWT)
- **Invitados:** Siguen usando `X-Session-Id` como antes
- El campo `UserId` en `Order` sigue siendo opcional

---

**Fecha de implementación:** Diciembre 2024  
**Estado:** ? Completado y listo para testing  
**Build:** ? Exitoso
