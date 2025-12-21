using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.Tenant
{
  /// <summary>
  /// Factory interface for creating tenant-scoped Unit of Work instances.
  /// </summary>
  public interface ITenantUnitOfWorkFactory
  {
    /// <summary>
    /// Creates a Unit of Work using the current tenant context
    /// </summary>
    ITenantUnitOfWork Create();

    /// <summary>
    /// Creates a Unit of Work for a specific connection string
    /// Useful for background workers or cross-tenant operations
    /// </summary>
    ITenantUnitOfWork Create(string connectionString);
  }

  /// <summary>
  /// Factory for creating tenant-scoped Unit of Work instances.
  /// Integrates with TenantAccessor to resolve tenant connection strings.
  /// </summary>
  public class TenantUnitOfWorkFactory : ITenantUnitOfWorkFactory
  {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TenantUnitOfWork> _uowLogger;
    private readonly ILogger<TenantUnitOfWorkFactory> _logger;

    public TenantUnitOfWorkFactory(
        IServiceProvider serviceProvider,
        ILogger<TenantUnitOfWork> uowLogger,
        ILogger<TenantUnitOfWorkFactory> logger)
    {
      _serviceProvider = serviceProvider;
      _uowLogger = uowLogger;
      _logger = logger;
    }

    /// <inheritdoc />
    public ITenantUnitOfWork Create()
    {
      var tenantAccessor = _serviceProvider.GetRequiredService<ITenantAccessor>();

      if (!tenantAccessor.HasTenant || tenantAccessor.TenantInfo == null)
      {
        throw new InvalidOperationException(
            "Cannot create TenantUnitOfWork: No tenant has been resolved in the current request context. " +
            "Ensure TenantResolutionMiddleware is properly configured and a valid X-Tenant-Slug header is provided.");
      }

      var tenantInfo = tenantAccessor.TenantInfo;

      _logger.LogDebug(
          "Creating TenantUnitOfWork for tenant {Slug} (DbName: {DbName})",
          tenantInfo.Slug, tenantInfo.DbName);

      var context = CreateContext(tenantInfo.ConnectionString);
      return new TenantUnitOfWork(context, _uowLogger);
    }

    /// <inheritdoc />
    public ITenantUnitOfWork Create(string connectionString)
    {
      if (string.IsNullOrWhiteSpace(connectionString))
      {
        throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
      }

      _logger.LogDebug("Creating TenantUnitOfWork with custom connection string");

      var context = CreateContext(connectionString);
      return new TenantUnitOfWork(context, _uowLogger);
    }

    private TenantDbContext CreateContext(string connectionString)
    {
      var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
      optionsBuilder.UseNpgsql(connectionString);
      optionsBuilder.EnableSensitiveDataLogging(false);
      optionsBuilder.EnableDetailedErrors(true);

      return new TenantDbContext(optionsBuilder.Options);
    }
  }
}
