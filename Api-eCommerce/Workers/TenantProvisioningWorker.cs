using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Provisioning;
using CC.Aplication.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Api_eCommerce.Workers
{
    /// <summary>
    /// Background worker que procesa la cola de aprovisionamiento de tenants
    /// </summary>
    public class TenantProvisioningWorker : BackgroundService
    {
        private readonly ILogger<TenantProvisioningWorker> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly Channel<Guid> _provisioningQueue;

        public TenantProvisioningWorker(
            ILogger<TenantProvisioningWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _provisioningQueue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        /// <summary>
        /// Encola un tenant para aprovisionamiento
        /// </summary>
        public async Task EnqueueProvisioningAsync(Guid tenantId)
        {
            await _provisioningQueue.Writer.WriteAsync(tenantId);
            _logger.LogInformation("Tenant {TenantId} enqueued for provisioning", tenantId);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TenantProvisioningWorker started");

            await foreach (var tenantId in _provisioningQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Processing provisioning for tenant {TenantId}", tenantId);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var provisioner = scope.ServiceProvider.GetRequiredService<ITenantProvisioner>();
                    var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
                    var tenantAccountSecurityService = scope.ServiceProvider.GetRequiredService<ITenantAccountSecurityService>();

                    // Actualizar estado a "Seeding"
                    var tenant = await adminDb.Tenants.FindAsync(new object[] { tenantId }, stoppingToken);
                    if (tenant != null)
                    {
                        tenant.Status = TenantStatus.Seeding;
                        tenant.UpdatedAt = DateTime.UtcNow;
                        await adminDb.SaveChangesAsync(stoppingToken);
                    }

                    // Ejecutar aprovisionamiento
                    var success = await provisioner.ProvisionTenantAsync(tenantId, stoppingToken);

                    if (success)
                    {
                        tenant = await adminDb.Tenants.FindAsync(new object[] { tenantId }, stoppingToken);
                        if (tenant?.PrimaryAdminUserId != null && !string.IsNullOrWhiteSpace(tenant.PrimaryAdminEmail))
                        {
                            var dispatchResult = await tenantAccountSecurityService.CreateTenantAdminActivationAsync(new TenantAdminActivationDispatchRequest
                            {
                                TenantId = tenant.Id,
                                UserId = tenant.PrimaryAdminUserId.Value,
                                TenantSlug = tenant.Slug,
                                TenantName = tenant.Name,
                                AdminEmail = tenant.PrimaryAdminEmail,
                                AdminName = "Admin System"
                            }, stoppingToken);

                            if (!dispatchResult.NotificationAccepted)
                            {
                                _logger.LogWarning("Tenant {TenantId} was provisioned but activation notification was not accepted", tenantId);
                            }
                        }

                        _logger.LogInformation("Provisioning completed successfully for tenant {TenantId}", tenantId);
                    }
                    else
                    {
                        _logger.LogError("Provisioning failed for tenant {TenantId}", tenantId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error processing tenant {TenantId}", tenantId);

                    // Intentar marcar como fallido
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
                        var tenant = await adminDb.Tenants.FindAsync(new object[] { tenantId }, stoppingToken);
                        if (tenant != null)
                        {
                            tenant.Status = TenantStatus.Failed;
                            tenant.LastError = ex.Message;
                            tenant.UpdatedAt = DateTime.UtcNow;
                            await adminDb.SaveChangesAsync(stoppingToken);
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Failed to update tenant {TenantId} status", tenantId);
                    }
                }
            }

            _logger.LogInformation("TenantProvisioningWorker stopped");
        }
    }
}
