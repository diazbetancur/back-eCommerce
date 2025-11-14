# ?? Guía de Despliegue - Migraciones y Seed

## Paso 1: Generar Migración de AdminDb

```bash
# Navegar al directorio raíz del proyecto
cd D:\Proyects\eCommerce\back.eCommerce

# Generar migración
dotnet ef migrations add AddAdminUsersAndRoles \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context AdminDbContext \
  --output-dir Admin/Migrations
```

**Resultado esperado:**
```
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

**Archivos creados:**
- `CC.Infraestructure/Admin/Migrations/YYYYMMDDHHMMSS_AddAdminUsersAndRoles.cs`
- `CC.Infraestructure/Admin/Migrations/AdminDbContextModelSnapshot.cs`

---

## Paso 2: Revisar la Migración

Abrir el archivo de migración generado y verificar:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "AdminUsers",
        schema: "admin",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
            PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            PasswordSalt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            IsActive = table.Column<bool>(type: "boolean", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
            LastLoginAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_AdminUsers", x => x.Id);
        });

    // ... más tablas ...
}
```

---

## Paso 3: Aplicar Migración

```bash
# Aplicar migración a la base de datos
dotnet ef database update \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context AdminDbContext
```

**Resultado esperado:**
```
Build started...
Build succeeded.
Applying migration '20241203XXXXXX_AddAdminUsersAndRoles'.
Done.
```

---

## Paso 4: Verificar en la Base de Datos

```sql
-- Conectar a PostgreSQL
psql -U postgres -d ecommerce_admin

-- Verificar tablas
\dt admin.*

-- Resultado esperado:
--             List of relations
--  Schema |       Name        | Type  |  Owner   
-- --------+-------------------+-------+----------
--  admin  | AdminRoles        | table | postgres
--  admin  | AdminUserRoles    | table | postgres
--  admin  | AdminUsers        | table | postgres
--  admin  | Features          | table | postgres
--  admin  | PlanFeatures      | table | postgres
--  admin  | Plans             | table | postgres
--  admin  | TenantFeatureOverrides | table | postgres
--  admin  | TenantProvisionings | table | postgres
--  admin  | TenantUsageDaily  | table | postgres
--  admin  | Tenants           | table | postgres

-- Verificar estructura de AdminUsers
\d admin."AdminUsers"
```

---

## Paso 5: Crear Script de Seed

### Opción A: Seed Manual (SQL)

Crear archivo `scripts/seed-admin-users.sql`:

```sql
-- Seed Admin Roles
INSERT INTO admin."AdminRoles" ("Id", "Name", "Description", "CreatedAt")
VALUES 
    ('11111111-1111-1111-1111-111111111111', 'SuperAdmin', 'Full system access', NOW()),
    ('22222222-2222-2222-2222-222222222222', 'TenantManager', 'Can manage tenants', NOW()),
    ('33333333-3333-3333-3333-333333333333', 'Support', 'Support team access', NOW()),
    ('44444444-4444-4444-4444-444444444444', 'Viewer', 'Read-only access', NOW())
ON CONFLICT DO NOTHING;

-- Seed Super Admin User
-- Password: Admin123! (ya hasheada con PBKDF2)
INSERT INTO admin."AdminUsers" ("Id", "Email", "PasswordHash", "PasswordSalt", "FullName", "IsActive", "CreatedAt")
VALUES (
    '99999999-9999-9999-9999-999999999999',
    'admin@yourdomain.com',
    'YOUR_PASSWORD_HASH_HERE',  -- Generar con AdminAuthService.HashPassword()
    'YOUR_PASSWORD_SALT_HERE',
    'Super Administrator',
    true,
    NOW()
)
ON CONFLICT DO NOTHING;

-- Assign SuperAdmin role
INSERT INTO admin."AdminUserRoles" ("AdminUserId", "AdminRoleId", "AssignedAt")
VALUES (
    '99999999-9999-9999-9999-999999999999',
    '11111111-1111-1111-1111-111111111111',
    NOW()
)
ON CONFLICT DO NOTHING;
```

**Ejecutar:**
```bash
psql -U postgres -d ecommerce_admin -f scripts/seed-admin-users.sql
```

---

### Opción B: Seed Programático (C#)

Crear archivo `CC.Infraestructure/Admin/AdminDbSeeder.cs`:

```csharp
using CC.Infraestructure.Admin;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Admin
{
    public static class AdminDbSeeder
    {
        public static async Task SeedAsync(AdminDbContext adminDb)
        {
            // 1. Seed Roles
            if (!await adminDb.AdminRoles.AnyAsync())
            {
                var roles = new[]
                {
                    new AdminRole
                    {
                        Id = Guid.NewGuid(),
                        Name = AdminRoleNames.SuperAdmin,
                        Description = "Full system access",
                        CreatedAt = DateTime.UtcNow
                    },
                    new AdminRole
                    {
                        Id = Guid.NewGuid(),
                        Name = AdminRoleNames.TenantManager,
                        Description = "Can manage tenants",
                        CreatedAt = DateTime.UtcNow
                    },
                    new AdminRole
                    {
                        Id = Guid.NewGuid(),
                        Name = AdminRoleNames.Support,
                        Description = "Support team access",
                        CreatedAt = DateTime.UtcNow
                    },
                    new AdminRole
                    {
                        Id = Guid.NewGuid(),
                        Name = AdminRoleNames.Viewer,
                        Description = "Read-only access",
                        CreatedAt = DateTime.UtcNow
                    }
                };

                adminDb.AdminRoles.AddRange(roles);
                await adminDb.SaveChangesAsync();
            }

            // 2. Seed SuperAdmin User
            var superAdminRole = await adminDb.AdminRoles
                .FirstAsync(r => r.Name == AdminRoleNames.SuperAdmin);

            var adminEmail = "admin@yourdomain.com";
            
            if (!await adminDb.AdminUsers.AnyAsync(u => u.Email == adminEmail))
            {
                var (hash, salt) = CC.Aplication.Admin.AdminAuthService.HashPassword("Admin123!");
                
                var superAdmin = new AdminUser
                {
                    Id = Guid.NewGuid(),
                    Email = adminEmail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FullName = "Super Administrator",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                adminDb.AdminUsers.Add(superAdmin);
                await adminDb.SaveChangesAsync();

                // Assign role
                var userRole = new AdminUserRole
                {
                    AdminUserId = superAdmin.Id,
                    AdminRoleId = superAdminRole.Id,
                    AssignedAt = DateTime.UtcNow
                };

                adminDb.AdminUserRoles.Add(userRole);
                await adminDb.SaveChangesAsync();
            }
        }
    }
}
```

**Agregar a Program.cs:**

```csharp
var app = builder.Build();

// ==================== SEED ADMIN DATA ====================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Seeding admin data...");
        await AdminDbSeeder.SeedAsync(adminDb);
        logger.LogInformation("? Admin data seeded successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "? Error seeding admin data");
    }
}

// Configure the HTTP request pipeline...
```

---

## Paso 6: Generar Hash de Contraseña

Si necesitas generar hashes manualmente para SQL:

```csharp
// Crear un endpoint temporal o usar LINQPad
var (hash, salt) = CC.Aplication.Admin.AdminAuthService.HashPassword("Admin123!");
Console.WriteLine($"Hash: {hash}");
Console.WriteLine($"Salt: {salt}");
```

O crear un script:

```bash
dotnet run --project Tools.PasswordHasher -- "Admin123!"
```

Crear proyecto `Tools.PasswordHasher/Program.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run -- <password>");
    return;
}

var password = args[0];
var (hash, salt) = HashPassword(password);

Console.WriteLine($"\nPassword: {password}");
Console.WriteLine($"\nHash:\n{hash}");
Console.WriteLine($"\nSalt:\n{salt}");

static (string Hash, string Salt) HashPassword(string password)
{
    var saltBytes = new byte[32];
    using (var rng = RandomNumberGenerator.Create())
    {
        rng.GetBytes(saltBytes);
    }

    using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
    var hashBytes = pbkdf2.GetBytes(32);

    return (
        Convert.ToBase64String(hashBytes),
        Convert.ToBase64String(saltBytes)
    );
}
```

---

## Paso 7: Testing

### Test 1: Login Admin

```bash
# Login
curl -X POST http://localhost:5000/admin/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "Admin123!"
  }'

# Resultado esperado:
# {
#   "token": "eyJhbGc...",
#   "expiresAt": "2024-12-04T15:30:00Z",
#   "user": {
#     "id": "...",
#     "email": "admin@yourdomain.com",
#     "fullName": "Super Administrator",
#     "isActive": true,
#     "roles": ["SuperAdmin"],
#     "createdAt": "2024-12-03T00:00:00Z",
#     "lastLoginAt": "2024-12-03T15:30:00Z"
#   }
# }
```

### Test 2: Get Profile

```bash
# Copiar el token de la respuesta anterior
TOKEN="eyJhbGc..."

curl -X GET http://localhost:5000/admin/auth/me \
  -H "Authorization: Bearer $TOKEN"

# Resultado esperado: mismo user object
```

### Test 3: List Tenants

```bash
curl -X GET "http://localhost:5000/admin/tenants?page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"

# Resultado esperado: lista de tenants
```

### Test 4: Get Tenant Details

```bash
# Copiar un ID de tenant de la lista anterior
TENANT_ID="..."

curl -X GET "http://localhost:5000/admin/tenants/$TENANT_ID" \
  -H "Authorization: Bearer $TOKEN"

# Resultado esperado: detalle completo del tenant
```

### Test 5: Update Tenant

```bash
curl -X PATCH "http://localhost:5000/admin/tenants/$TENANT_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Updated Store Name",
    "featureFlagsJson": "{\"loyalty\":{\"enabled\":true,\"pointsPerUnit\":2}}",
    "isActive": true
  }'

# Resultado esperado: tenant actualizado
```

---

## Paso 8: Verificación Final

### Checklist

- [ ] Migración generada exitosamente
- [ ] Migración aplicada a la base de datos
- [ ] Tablas creadas en schema `admin`
- [ ] Seed de AdminRoles ejecutado
- [ ] Seed de SuperAdmin creado
- [ ] Login admin funciona
- [ ] JWT token se genera correctamente
- [ ] Endpoints de tenants funcionan
- [ ] Authorization verifica el claim "admin": "true"

### Queries de Verificación

```sql
-- Verificar roles
SELECT * FROM admin."AdminRoles";

-- Verificar usuarios
SELECT "Id", "Email", "FullName", "IsActive", "CreatedAt" 
FROM admin."AdminUsers";

-- Verificar asignación de roles
SELECT 
    u."Email",
    r."Name" as "Role",
    ur."AssignedAt"
FROM admin."AdminUserRoles" ur
JOIN admin."AdminUsers" u ON u."Id" = ur."AdminUserId"
JOIN admin."AdminRoles" r ON r."Id" = ur."AdminRoleId";
```

---

## Troubleshooting

### Error: "Table already exists"

```bash
# Revertir migración
dotnet ef database update 0 \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context AdminDbContext

# Eliminar migración
dotnet ef migrations remove \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context AdminDbContext

# Volver a generar
dotnet ef migrations add AddAdminUsersAndRoles ...
```

### Error: "AdminDbContext not found"

Verificar que el `--context AdminDbContext` esté correcto y que la clase exista en `CC.Infraestructure/Admin/AdminDbContext.cs`.

### Error: "Connection string not found"

Verificar `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AdminDb": "Host=localhost;Database=ecommerce_admin;Username=postgres;Password=..."
  }
}
```

### Error: "Unauthorized" en endpoints

Verificar que:
1. El token JWT contiene el claim `"admin": "true"`
2. El token no ha expirado
3. La firma del token es válida (mismo `jwtKey` en configuración)

---

## ?? ¡Listo!

Una vez completados todos los pasos, tendrás:

- ? Base de datos administrativa con nuevas tablas
- ? SuperAdmin creado con contraseña `Admin123!`
- ? Sistema de roles funcional
- ? Endpoints administrativos operativos
- ? Separación clara entre Admin y Tenant

---

**Siguiente paso:** Implementar AdminPlanService y AdminUserService para completar el panel administrativo.
