using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.Admin
{
    /// <summary>
    /// Seeder para planes y sus límites
    /// Define los planes disponibles (Basic, Premium, Enterprise) con sus restricciones
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
                    Name = "Plan Básico"
                },
                new Plan
                {
                    Id = Guid.Parse("22222222-0000-0000-0000-000000000002"),
                    Code = "Premium",
                    Name = "Plan Premium"
                },
                new Plan
                {
                    Id = Guid.Parse("33333333-0000-0000-0000-000000000003"),
                    Code = "Enterprise",
                    Name = "Plan Enterprise"
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
            var enterprisePlanId = Guid.Parse("33333333-0000-0000-0000-000000000003");

            var limits = new List<PlanLimit>();

            // ==================== PLAN BASIC ====================
            limits.AddRange(new[]
            {
                // Productos
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxProductImages,
                    LimitValue = 3,
                    Description = "Máximo 3 imágenes por producto"
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
                    LimitCode = PlanLimitCodes.MaxProducts,
                    LimitValue = 100,
                    Description = "Máximo 100 productos"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxCategories,
                    LimitValue = 10,
                    Description = "Máximo 10 categorías"
                },

                // Usuarios
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxUsers,
                    LimitValue = 5,
                    Description = "Máximo 5 usuarios"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxAdminUsers,
                    LimitValue = 2,
                    Description = "Máximo 2 usuarios admin"
                },

                // Órdenes
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxOrdersPerMonth,
                    LimitValue = 500,
                    Description = "Máximo 500 órdenes por mes"
                },

                // Almacenamiento
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxStorageMB,
                    LimitValue = 1024,  // 1 GB
                    Description = "1 GB de almacenamiento"
                },
                new PlanLimit
                {
                    PlanId = basicPlanId,
                    LimitCode = PlanLimitCodes.MaxFileUploadMB,
                    LimitValue = 5,
                    Description = "Máximo 5 MB por archivo"
                }
            });

            // ==================== PLAN PREMIUM ====================
            limits.AddRange(new[]
            {
                // Productos
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxProductImages,
                    LimitValue = 10,
                    Description = "Máximo 10 imágenes por producto"
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
                    LimitCode = PlanLimitCodes.MaxProducts,
                    LimitValue = 1000,
                    Description = "Máximo 1000 productos"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxCategories,
                    LimitValue = 50,
                    Description = "Máximo 50 categorías"
                },

                // Usuarios
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxUsers,
                    LimitValue = 50,
                    Description = "Máximo 50 usuarios"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxAdminUsers,
                    LimitValue = 10,
                    Description = "Máximo 10 usuarios admin"
                },

                // Órdenes
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxOrdersPerMonth,
                    LimitValue = 5000,
                    Description = "Máximo 5000 órdenes por mes"
                },

                // Almacenamiento
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxStorageMB,
                    LimitValue = 10240,  // 10 GB
                    Description = "10 GB de almacenamiento"
                },
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxFileUploadMB,
                    LimitValue = 20,
                    Description = "Máximo 20 MB por archivo"
                },

                // Loyalty
                new PlanLimit
                {
                    PlanId = premiumPlanId,
                    LimitCode = PlanLimitCodes.MaxLoyaltyPointsPerOrder,
                    LimitValue = 1000,
                    Description = "Máximo 1000 puntos por orden"
                }
            });

            // ==================== PLAN ENTERPRISE ====================
            limits.AddRange(new[]
            {
                // Productos
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxProductImages,
                    LimitValue = -1,  // Ilimitado
                    Description = "Imágenes ilimitadas por producto"
                },
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxProductVideos,
                    LimitValue = -1,  // Ilimitado
                    Description = "Videos ilimitados por producto"
                },
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxProducts,
                    LimitValue = -1,  // Ilimitado
                    Description = "Productos ilimitados"
                },
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxCategories,
                    LimitValue = -1,  // Ilimitado
                    Description = "Categorías ilimitadas"
                },

                // Usuarios
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxUsers,
                    LimitValue = -1,  // Ilimitado
                    Description = "Usuarios ilimitados"
                },
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxAdminUsers,
                    LimitValue = -1,  // Ilimitado
                    Description = "Usuarios admin ilimitados"
                },

                // Órdenes
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxOrdersPerMonth,
                    LimitValue = -1,  // Ilimitado
                    Description = "Órdenes ilimitadas por mes"
                },

                // Almacenamiento
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxStorageMB,
                    LimitValue = -1,  // Ilimitado
                    Description = "Almacenamiento ilimitado"
                },
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxFileUploadMB,
                    LimitValue = 100,
                    Description = "Máximo 100 MB por archivo"
                },

                // Loyalty
                new PlanLimit
                {
                    PlanId = enterprisePlanId,
                    LimitCode = PlanLimitCodes.MaxLoyaltyPointsPerOrder,
                    LimitValue = -1,  // Ilimitado
                    Description = "Puntos ilimitados por orden"
                }
            });

            adminDb.PlanLimits.AddRange(limits);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} plan limits", limits.Count);
        }
    }
}
