using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace CC.Infraestructure.Admin
{
    /// <summary>
    /// Seeder para la base de datos administrativa global (AdminDb)
    /// Crea el usuario SuperAdmin del sistema que gestiona todos los tenants
    /// </summary>
    public static class AdminDbSeeder
    {
        /// <summary>
        /// Seed de datos iniciales del AdminDb (roles y superadmin)
        /// Este m�todo es IDEMPOTENTE - puede ejecutarse m�ltiples veces sin duplicar datos
        /// </summary>
        public static async Task SeedAsync(AdminDbContext adminDb, ILogger? logger = null)
        {
            logger?.LogInformation("?? Starting AdminDb seed...");

            // ==================== 1. SEED ROLES ====================
            await SeedRolesAsync(adminDb, logger);

            // ==================== 2. SEED SUPERADMIN USER ====================
            await SeedSuperAdminAsync(adminDb, logger);

            // ==================== 3. SEED PLANS AND LIMITS ? NUEVO ====================
            await PlanLimitsSeeder.SeedAsync(adminDb, logger);

            // ==================== 4. SEED NOTIFICATION CATALOG ====================
            await SeedNotificationsAsync(adminDb, logger);

            logger?.LogInformation("? AdminDb seed completed successfully");
        }

        private static async Task SeedNotificationsAsync(AdminDbContext adminDb, ILogger? logger)
        {
            logger?.LogInformation("Seeding notification catalog...");

            var now = DateTime.UtcNow;
            var events = new[]
            {
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.PasswordReset,
                    Name = "Password reset",
                    Description = "Transactional password reset email",
                    Category = NotificationCategory.Security,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = false,
                    IsSystemRequired = true,
                    ConsumesQuota = false,
                    DefaultEnabled = true,
                    TemplateCode = NotificationTemplateCodes.PasswordReset,
                    IsActive = true,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.TenantAdminActivation,
                    Name = "Tenant admin activation",
                    Description = "Transactional activation email for the tenant primary administrator",
                    Category = NotificationCategory.Security,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = false,
                    IsSystemRequired = true,
                    ConsumesQuota = false,
                    DefaultEnabled = true,
                    TemplateCode = NotificationTemplateCodes.TenantAdminActivation,
                    IsActive = true,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.UserInvitation,
                    Name = "User invitation",
                    Description = "Transactional user invitation email",
                    Category = NotificationCategory.Security,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = false,
                    IsSystemRequired = true,
                    ConsumesQuota = false,
                    DefaultEnabled = true,
                    TemplateCode = NotificationTemplateCodes.UserInvitation,
                    IsActive = true,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.OrderCreated,
                    Name = "Order created",
                    Description = "Commercial notification when an order is created",
                    Category = NotificationCategory.Orders,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = true,
                    IsSystemRequired = false,
                    ConsumesQuota = true,
                    DefaultEnabled = false,
                    TemplateCode = NotificationTemplateCodes.OrderCreated,
                    IsActive = false,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.OrderShipped,
                    Name = "Order shipped",
                    Description = "Commercial notification when an order is shipped",
                    Category = NotificationCategory.Orders,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = true,
                    IsSystemRequired = false,
                    ConsumesQuota = true,
                    DefaultEnabled = false,
                    TemplateCode = NotificationEventCodes.OrderShipped,
                    IsActive = false,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.OrderDelivered,
                    Name = "Order delivered",
                    Description = "Commercial notification when an order is delivered",
                    Category = NotificationCategory.Orders,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = true,
                    IsSystemRequired = false,
                    ConsumesQuota = true,
                    DefaultEnabled = false,
                    TemplateCode = NotificationEventCodes.OrderDelivered,
                    IsActive = false,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.OrderCancelled,
                    Name = "Order cancelled",
                    Description = "Commercial notification when an order is cancelled",
                    Category = NotificationCategory.Orders,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = true,
                    IsSystemRequired = false,
                    ConsumesQuota = true,
                    DefaultEnabled = false,
                    TemplateCode = NotificationEventCodes.OrderCancelled,
                    IsActive = false,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.PaymentApproved,
                    Name = "Payment approved",
                    Description = "Commercial notification when a payment is approved",
                    Category = NotificationCategory.Payments,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = true,
                    IsSystemRequired = false,
                    ConsumesQuota = true,
                    DefaultEnabled = false,
                    TemplateCode = NotificationEventCodes.PaymentApproved,
                    IsActive = false,
                    CreatedAt = now
                },
                new NotificationEventDefinition
                {
                    Code = NotificationEventCodes.PaymentRejected,
                    Name = "Payment rejected",
                    Description = "Commercial notification when a payment is rejected",
                    Category = NotificationCategory.Payments,
                    Channel = NotificationChannel.Email,
                    IsTenantConfigurable = true,
                    IsSystemRequired = false,
                    ConsumesQuota = true,
                    DefaultEnabled = false,
                    TemplateCode = NotificationEventCodes.PaymentRejected,
                    IsActive = false,
                    CreatedAt = now
                }
            };

            var existingEvents = await adminDb.NotificationEventDefinitions.ToListAsync();
            var addedEvents = 0;
            var updatedEvents = 0;

            foreach (var eventDefinition in events)
            {
                var existing = existingEvents.FirstOrDefault(item =>
                    item.Code.Equals(eventDefinition.Code, StringComparison.OrdinalIgnoreCase) &&
                    item.Channel == eventDefinition.Channel);

                if (existing == null)
                {
                    adminDb.NotificationEventDefinitions.Add(eventDefinition);
                    addedEvents++;
                    continue;
                }

                existing.Name = eventDefinition.Name;
                existing.Description = eventDefinition.Description;
                existing.Category = eventDefinition.Category;
                existing.IsTenantConfigurable = eventDefinition.IsTenantConfigurable;
                existing.IsSystemRequired = eventDefinition.IsSystemRequired;
                existing.ConsumesQuota = eventDefinition.ConsumesQuota;
                existing.DefaultEnabled = eventDefinition.DefaultEnabled;
                existing.TemplateCode = eventDefinition.TemplateCode;
                existing.IsActive = eventDefinition.IsActive;
                existing.UpdatedAt = now;
                updatedEvents++;
            }

            var templates = new[]
            {
                new NotificationTemplate
                {
                    Code = NotificationTemplateCodes.PasswordReset,
                    Channel = NotificationChannel.Email,
                    SourceType = NotificationSourceType.Platform,
                    Name = "Password reset",
                    SubjectTemplate = "Restablece tu contraseña en {{tenantName}}",
                    HtmlTemplate = "<p>Hola {{userName}},</p><p>Haz clic aquí para restablecer tu contraseña: <a href=\"{{resetPasswordUrl}}\">Restablecer contraseña</a></p><p>El enlace vence en {{expirationHours}} horas.</p><p>Si necesitas ayuda, escribe a {{supportEmail}}.</p>",
                    TextTemplate = "Hola {{userName}}, usa este enlace para restablecer tu contraseña: {{resetPasswordUrl}}. El enlace vence en {{expirationHours}} horas. Soporte: {{supportEmail}}.",
                    AvailableVariablesJson = "[\"tenantName\",\"userName\",\"resetPasswordUrl\",\"supportEmail\",\"expirationHours\"]",
                    Version = 1,
                    IsActive = true,
                    CreatedAt = now
                },
                new NotificationTemplate
                {
                    Code = NotificationTemplateCodes.TenantAdminActivation,
                    Channel = NotificationChannel.Email,
                    SourceType = NotificationSourceType.Platform,
                    Name = "Tenant admin activation",
                    SubjectTemplate = "Activa tu cuenta de administrador en {{tenantName}}",
                    HtmlTemplate = "<p>Hola {{adminName}},</p><p>Tu tienda <strong>{{tenantName}}</strong> ya está lista. Activa tu cuenta con este enlace de un solo uso: <a href=\"{{activationUrl}}\">Activar cuenta</a>.</p><p>El enlace vence en {{expirationHours}} horas.</p><p>Si necesitas ayuda, escribe a {{supportEmail}}.</p>",
                    TextTemplate = "Hola {{adminName}}, activa tu cuenta de administrador en {{tenantName}} usando este enlace: {{activationUrl}}. El enlace vence en {{expirationHours}} horas. Soporte: {{supportEmail}}.",
                    AvailableVariablesJson = "[\"tenantName\",\"adminName\",\"activationUrl\",\"supportEmail\",\"expirationHours\"]",
                    Version = 1,
                    IsActive = true,
                    CreatedAt = now
                },
                new NotificationTemplate
                {
                    Code = NotificationTemplateCodes.UserInvitation,
                    Channel = NotificationChannel.Email,
                    SourceType = NotificationSourceType.Platform,
                    Name = "User invitation",
                    SubjectTemplate = "Invitación a {{tenantName}}",
                    HtmlTemplate = "<p>Hola {{customerName}},</p><p>Tienes una invitación pendiente para acceder a {{tenantName}}. Actívala aquí: <a href=\"{{actionUrl}}\">Aceptar invitación</a></p><p>Soporte: {{supportEmail}}</p>",
                    TextTemplate = "Hola {{customerName}}, acepta tu invitación a {{tenantName}} en {{actionUrl}}. Soporte: {{supportEmail}}.",
                    AvailableVariablesJson = "[\"tenantName\",\"customerName\",\"actionUrl\",\"supportEmail\"]",
                    Version = 1,
                    IsActive = true,
                    CreatedAt = now
                },
                new NotificationTemplate
                {
                    Code = NotificationTemplateCodes.OrderCreated,
                    Channel = NotificationChannel.Email,
                    SourceType = NotificationSourceType.Platform,
                    Name = "Order created",
                    SubjectTemplate = "Tu pedido {{orderNumber}} fue creado",
                    HtmlTemplate = "<p>Hola {{customerName}},</p><p>Confirmamos la creación de tu pedido <strong>{{orderNumber}}</strong> en {{tenantName}}.</p><p>Si necesitas ayuda, contáctanos en {{supportEmail}}.</p>",
                    TextTemplate = "Hola {{customerName}}, tu pedido {{orderNumber}} fue creado en {{tenantName}}. Soporte: {{supportEmail}}.",
                    AvailableVariablesJson = "[\"tenantName\",\"customerName\",\"orderNumber\",\"supportEmail\"]",
                    Version = 1,
                    IsActive = true,
                    CreatedAt = now
                }
            };

            var existingTemplates = await adminDb.NotificationTemplates.ToListAsync();
            var addedTemplates = 0;
            var updatedTemplates = 0;

            foreach (var template in templates)
            {
                var existing = existingTemplates.FirstOrDefault(item =>
                    item.Code.Equals(template.Code, StringComparison.OrdinalIgnoreCase) &&
                    item.Channel == template.Channel &&
                    item.Version == template.Version);

                if (existing == null)
                {
                    adminDb.NotificationTemplates.Add(template);
                    addedTemplates++;
                    continue;
                }

                existing.SourceType = template.SourceType;
                existing.Name = template.Name;
                existing.SubjectTemplate = template.SubjectTemplate;
                existing.HtmlTemplate = template.HtmlTemplate;
                existing.TextTemplate = template.TextTemplate;
                existing.AvailableVariablesJson = template.AvailableVariablesJson;
                existing.IsActive = template.IsActive;
                existing.UpdatedAt = now;
                updatedTemplates++;
            }

            if (addedEvents > 0 || updatedEvents > 0 || addedTemplates > 0 || updatedTemplates > 0)
            {
                await adminDb.SaveChangesAsync();
            }

            logger?.LogInformation(
                "Notification catalog seed finished. Added {AddedEvents} events, updated {UpdatedEvents} events, added {AddedTemplates} templates and updated {UpdatedTemplates} templates",
                addedEvents,
                updatedEvents,
                addedTemplates,
                updatedTemplates);
        }

        /// <summary>
        /// Seed de roles administrativos del sistema
        /// </summary>
        private static async Task SeedRolesAsync(AdminDbContext adminDb, ILogger? logger)
        {
            if (await adminDb.AdminRoles.AnyAsync())
            {
                logger?.LogInformation("??  Admin roles already exist, skipping seed");
                return;
            }

            logger?.LogInformation("Creating admin roles...");

            var roles = new[]
            {
                new AdminRole
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = AdminRoleNames.SuperAdmin,
                    Description = "Full system access - Can manage all tenants, users, and system configuration",
                    CreatedAt = DateTime.UtcNow
                },
                new AdminRole
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = AdminRoleNames.TenantManager,
                    Description = "Can create and manage tenants, view reports and analytics",
                    CreatedAt = DateTime.UtcNow
                },
                new AdminRole
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Name = AdminRoleNames.Support,
                    Description = "Support team access - Can view tenant information and assist users",
                    CreatedAt = DateTime.UtcNow
                },
                new AdminRole
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Name = AdminRoleNames.Viewer,
                    Description = "Read-only access - Can view but not modify system data",
                    CreatedAt = DateTime.UtcNow
                }
            };

            adminDb.AdminRoles.AddRange(roles);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} admin roles", roles.Length);
        }

        /// <summary>
        /// Seed del usuario SuperAdmin inicial
        /// Credenciales por defecto: admin@yourdomain.com / Admin123!
        /// IMPORTANTE: Cambiar la contrase�a despu�s del primer login en producci�n
        /// </summary>
        private static async Task SeedSuperAdminAsync(AdminDbContext adminDb, ILogger? logger)
        {
            var superAdminId = Guid.Parse("99999999-9999-9999-9999-999999999999");
            const string adminEmail = "admin@yourdomain.com";

            // Obtener rol SuperAdmin
            var superAdminRole = await adminDb.AdminRoles
                .FirstOrDefaultAsync(r => r.Name == AdminRoleNames.SuperAdmin);

            if (superAdminRole == null)
            {
                logger?.LogError("? SuperAdmin role not found! Run SeedRolesAsync first");
                throw new InvalidOperationException("SuperAdmin role not found. Roles must be seeded before users.");
            }

            var superAdmin = await adminDb.AdminUsers
                .FirstOrDefaultAsync(u => u.Id == superAdminId || u.Email == adminEmail);

            if (superAdmin == null)
            {
                logger?.LogInformation("Creating SuperAdmin user...");

                var (hash, salt) = HashPassword("Admin123!");

                superAdmin = new AdminUser
                {
                    Id = superAdminId,
                    Email = adminEmail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FullName = "Super Administrator",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                adminDb.AdminUsers.Add(superAdmin);
                await adminDb.SaveChangesAsync();

                logger?.LogInformation("? Created SuperAdmin user: {Email}", adminEmail);
                logger?.LogWarning("??  DEFAULT CREDENTIALS - Email: {Email} | Password: Admin123!", adminEmail);
                logger?.LogWarning("??  IMPORTANT: Change the password after first login in production!");
            }
            else
            {
                logger?.LogInformation("??  SuperAdmin user already exists, skipping creation");
            }

            var roleAlreadyAssigned = await adminDb.AdminUserRoles
                .AnyAsync(ur => ur.AdminUserId == superAdmin.Id && ur.AdminRoleId == superAdminRole.Id);

            if (roleAlreadyAssigned)
            {
                logger?.LogInformation("??  SuperAdmin role already assigned, skipping role seed");
                return;
            }

            var userRole = new AdminUserRole
            {
                AdminUserId = superAdmin.Id,
                AdminRoleId = superAdminRole.Id,
                AssignedAt = DateTime.UtcNow
            };

            adminDb.AdminUserRoles.Add(userRole);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Assigned SuperAdmin role");
        }

        /// <summary>
        /// Crea un usuario administrativo adicional (�til para desarrollo o m�ltiples admins)
        /// </summary>
        public static async Task CreateAdminUserAsync(
            AdminDbContext adminDb,
            string email,
            string password,
            string fullName,
            string roleName,
            ILogger? logger = null)
        {
            // Verificar si el usuario ya existe
            if (await adminDb.AdminUsers.AnyAsync(u => u.Email == email))
            {
                logger?.LogWarning("??  User {Email} already exists", email);
                return;
            }

            // Buscar rol
            var role = await adminDb.AdminRoles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                logger?.LogError("? Role {RoleName} not found", roleName);
                throw new InvalidOperationException($"Role '{roleName}' not found");
            }

            // Crear usuario
            var (hash, salt) = HashPassword(password);

            var user = new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt,
                FullName = fullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            adminDb.AdminUsers.Add(user);
            await adminDb.SaveChangesAsync();

            // Asignar rol
            var userRole = new AdminUserRole
            {
                AdminUserId = user.Id,
                AdminRoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            };

            adminDb.AdminUserRoles.Add(userRole);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created admin user: {Email} with role {Role}", email, roleName);
        }

        // ==================== PASSWORD HASHING ====================

        public static (string Hash, string Salt) HashPassword(string password)
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
    }
}
