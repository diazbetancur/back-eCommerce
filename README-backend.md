# Backend Multitenant (.NET8, PostgreSQL)

## Variables de entorno

```
ConnectionStrings__ADMIN_PG="Host=localhost;Database=admin_db;Username=admin;Password=secret"
JWT__Key="clave-secreta-larga"
JWT__Issuer="cc-api"
# DataProtection opcional
DataProtection__KeysPath="C:/dpkeys"
```

> Requiere rol Postgres con permiso CREATEDB para crear las DB de tenants.

## Migraciones EF

- Admin:
```
dotnet ef migrations add InitialAdmin -c CC.Infraestructure.Admin.AdminDbContext -o CC.Infraestructure/Admin/Migrations/Admin
-dotnet ef database update -c CC.Infraestructure.Admin.AdminDbContext
```
- Tenant:
```
dotnet ef migrations add InitialTenant -c CC.Infraestructure.Tenant.TenantDbContext -o CC.Infraestructure/Tenant/Migrations/Tenant
# Se aplican al crear el tenant
```

## cURL de prueba

Crear tenant:
```
curl -X POST "http://localhost:5000/superadmin/tenants?slug=acme&name=Acme&planCode=basic"
```
Repair:
```
curl -X POST "http://localhost:5000/superadmin/repair?tenant=acme"
```
Config pública:
```
curl -H "X-Tenant-Slug: acme" http://localhost:5000/public/tenant-config
```
Login tenant:
```
curl -X POST -H "X-Tenant-Slug: acme" -d "email=admin@acme&password=temporal" http://localhost:5000/auth/login
```
Push subscribe:
```
curl -X POST -H "Content-Type: application/json" -H "X-Tenant-Slug: acme" \
 -d '{"endpoint":"...","p256dh":"...","auth":"...","userAgent":"Chrome"}' \
 http://localhost:5000/api/push/subscribe
```
Push send:
```
curl -X POST -H "Content-Type: application/json" -H "X-Tenant-Slug: acme" \
 -d '{"title":"Hi","body":"Welcome"}' \
 http://localhost:5000/api/push/send
```

## Notas
- Endpoint de creación realiza DB+Migraciones+Seed en mismo request.
- CORS por tenant: campo AllowedOrigins en admin.tenants (CSV).
- Metering mínimo: requests/errores por día en admin.tenantusagedaily.
