using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.Admin
{
    /// <summary>
    /// Seeder para planes y sus límites (MVP)
    /// Solo 2 planes: Basic ($5/mes) y Premium ($15/mes)
    /// Los planes NO tienen CRUD por API - solo configurables en DB
    /// </summary>
    public static class PlanLimitsSeeder
    {
        public static async Task SeedAsync(AdminDbContext adminDb, ILogger? logger = null)
        {
            logger?.LogInformation("?? Seeding plans and limits...");

            await SeedPlansAsync(adminDb, logger);
            await SeedPlanLimitsAsync(adminDb, logger);

            logger?.LogInformation("? Plans and limits seeded successfully");
        }

        private static async Task SeedPlansAsync(AdminDbContext adminDb, ILogger? logger)
        {
            if (await adminDb.Plans.AnyAsync())
            {
                logger?.LogInformation("??  Plans already exist, skipping");
                return;
            }

            logger?.LogInformation("Creating plans...");

            var plans = new[]
            {
                new Plan
                {
                    Id = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                    Code = "Basic",
                    Name = "Plan Básico - $5/mes"
                },
                new Plan
                {
                    Id = Guid.Parse("22222222-0000-0000-0000-000000000002"),
                    Code = "Premium",
                    Name = "Plan Premium - $15/mes"
                }
            };

            adminDb.Plans.AddRange(plans);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} plans", plans.Length);
        }

        private static async Task SeedPlanLimitsAsync(AdminDbContext adminDb, ILogger? logger)
        {
            if (await adminDb.PlanLimits.AnyAsync())
            {
                logger?.LogInformation("??  Plan limits already exist, skipping");
                return;
            }

            logger?.LogInformation("Creating plan limits...");

            var basicPlanId = Guid.Parse("11111111-0000-0000-0000-000000000001");
            var premiumPlanId = Guid.Parse("22222222-0000-0000-0000-000000000002");

            var limits = new List<PlanLimit>();

            // ==================== PLAN BASIC ($5/mes) ====================
            limits.AddRange(new[]
            {
                // ?? Productos
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxProducts,
                    LimitValue = 500,
                    Description = "Máximo 500 productos"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxCategories,
                    LimitValue = 100,
                    Description = "Máximo 100 categorías"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxProductImages,
                    LimitValue = 4,
                    Description = "Máximo 4 imágenes por producto"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxProductVideos,
                    LimitValue = 1,
                    Description = "Máximo 1 video por producto"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = "max_video_duration_seconds",
                    LimitValue = 30,
                    Description = "Videos de máximo 30 segundos"
                },

                // ?? Usuarios Admin/Staff
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxAdminUsers,
                    LimitValue = 3,
                    Description = "Máximo 3 usuarios admin/staff"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = "max_customer_inactivity_days",
                    LimitValue = 90,  // 3 meses
                    Description = "Clientes eliminados después de 90 días de inactividad"
                },

                // ?? Órdenes (Ilimitadas)
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxOrdersPerMonth,
                    LimitValue = -1,  // Ilimitado
                    Description = "Órdenes ilimitadas por mes"
                },

                // ?? Almacenamiento
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxStorageMB,
                    LimitValue = 2048,  // 2 GB
                    Description = "2 GB de almacenamiento"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxFileUploadMB,
                    LimitValue = 5,
                    Description = "Máximo 5 MB por archivo"
                }
            });

            // ==================== PLAN PREMIUM ($15/mes) ====================
            limits.AddRange(new[]
            {
                // ?? Productos
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxProducts,
                    LimitValue = 5000,
                    Description = "Máximo 5000 productos"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxCategories,
                    LimitValue = 1000,
                    Description = "Máximo 1000 categorías"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxProductImages,
                    LimitValue = 20,
                    Description = "Máximo 20 imágenes por producto"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxProductVideos,
                    LimitValue = 5,
                    Description = "Máximo 5 videos por producto"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = "max_video_duration_seconds",
                    LimitValue = 60,
                    Description = "Videos de máximo 60 segundos"
                },

                // ?? Usuarios Admin/Staff
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxAdminUsers,
                    LimitValue = 15,
                    Description = "Máximo 15 usuarios admin/staff"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = "max_customer_inactivity_days",
                    LimitValue = 365,  // 12 meses
                    Description = "Clientes eliminados después de 365 días de inactividad"
                },

                // ?? Órdenes (Ilimitadas)
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxOrdersPerMonth,
                    LimitValue = -1,  // Ilimitado
                    Description = "Órdenes ilimitadas por mes"
                },

                // ?? Almacenamiento
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxStorageMB,
                    LimitValue = 20480,  // 20 GB
                    Description = "20 GB de almacenamiento"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxFileUploadMB,
                    LimitValue = 20,
                    Description = "Máximo 20 MB por archivo"
                }
            });

            adminDb.PlanLimits.AddRange(limits);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} plan limits", limits.Count);
        }
    }
}
