using CC.Aplication.Services;
using CC.Domain.Interfaces.Repositories;
using CC.Domain.Interfaces.Services;
using CC.Infraestructure.Configurations;
using CC.Infraestructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using System.Reflection;
using ILogger = Serilog.ILogger;

namespace Api_eCommerce.Handlers
{
    public class DependencyInyectionHandler
    {
        public static void DepencyInyectionConfig(IServiceCollection services)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");

            IConfiguration configuration = builder.Build();

            services.AddSingleton(configuration);

            #region database (PostgreSQL)

            services.AddDbContext<DBContext>(opt => opt.UseNpgsql(configuration.GetConnectionString("PgSQL")));

            #endregion database (PostgreSQL)

            #region Automapper

            services.AddAutoMapper(cfg =>
            {
                cfg.AddMaps(Assembly.Load("CC.Domain"));
            });

            #endregion Automapper

            #region ServiceRegistrarion

            ServicesRegistration(services);

            #endregion ServiceRegistrarion

            #region RepositoriesRegistrarion

            RepositoryRegistration(services);

            #endregion RepositoriesRegistrarion

            services.AddSingleton<ExceptionControl>();

            #region Logs

            Logger logger = new LoggerConfiguration()
                .WriteTo
                .File("log.txt",
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            logger.Information("Done setting up serilog - Application starting up");

            services.AddSingleton<ILogger>(logger);

            #endregion Logs

            //services.AddTransient<SeedDB>();
        }

        public static void ServicesRegistration(IServiceCollection services)
        {
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IBannerService, BannerService>();
            services.AddScoped<IFileStorageService, CC.Infraestructure.Services.GoogleStorageService>(provider =>
                new CC.Infraestructure.Services.GoogleStorageService(provider.GetRequiredService<IConfiguration>()));
            //services.AddScoped<IAuditService, AuditService>();

            services.AddHttpClient();
        }

        public static void RepositoryRegistration(IServiceCollection services)
        {
            services.AddScoped<IQueryableUnitOfWork, DBContext>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IBannerRepository, BannerRepository>();
            //services.AddScoped<IAuditRepository, AuditRepository>();
        }
    }
}