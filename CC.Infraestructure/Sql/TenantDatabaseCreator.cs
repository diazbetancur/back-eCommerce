using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CC.Infraestructure.Sql
{
    /// <summary>
    /// Servicio para crear bases de datos de tenants en PostgreSQL
    /// </summary>
    public interface ITenantDatabaseCreator
    {
        Task<bool> CreateDatabaseAsync(string dbName, CancellationToken cancellationToken = default);
        Task<bool> DatabaseExistsAsync(string dbName, CancellationToken cancellationToken = default);
    }

    public class TenantDatabaseCreator : ITenantDatabaseCreator
    {
        private readonly string _masterConnectionString;
        private readonly ILogger<TenantDatabaseCreator> _logger;

        public TenantDatabaseCreator(IConfiguration configuration, ILogger<TenantDatabaseCreator> logger)
        {
            // Connection string a la DB "postgres" (master) para crear nuevas bases de datos
            var adminCs = configuration.GetConnectionString("AdminDb") 
                ?? throw new InvalidOperationException("AdminDb connection string not configured");
            
            // Cambiar el nombre de la base de datos a "postgres" (la DB por defecto)
            var builder = new NpgsqlConnectionStringBuilder(adminCs)
            {
                Database = "postgres"
            };
            _masterConnectionString = builder.ToString();
            _logger = logger;
        }

        public async Task<bool> DatabaseExistsAsync(string dbName, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_masterConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new NpgsqlCommand(
                    "SELECT 1 FROM pg_database WHERE datname = @dbName", 
                    connection);
                command.Parameters.AddWithValue("dbName", dbName);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if database {DbName} exists", dbName);
                throw;
            }
        }

        public async Task<bool> CreateDatabaseAsync(string dbName, CancellationToken cancellationToken = default)
        {
            try
            {
                // Verificar si la base de datos ya existe
                if (await DatabaseExistsAsync(dbName, cancellationToken))
                {
                    _logger.LogWarning("Database {DbName} already exists", dbName);
                    return true;
                }

                await using var connection = new NpgsqlConnection(_masterConnectionString);
                await connection.OpenAsync(cancellationToken);

                // Crear la base de datos
                // Nota: Los nombres de base de datos no pueden ser parametrizados, 
                // pero validamos el nombre antes de usarlo
                if (!IsValidDatabaseName(dbName))
                {
                    throw new ArgumentException($"Invalid database name: {dbName}");
                }

                var createDbSql = $"CREATE DATABASE \"{dbName}\" WITH ENCODING = 'UTF8' LC_COLLATE = 'en_US.UTF-8' LC_CTYPE = 'en_US.UTF-8' TEMPLATE = template0";
                
                await using var command = new NpgsqlCommand(createDbSql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Successfully created database {DbName}", dbName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database {DbName}", dbName);
                throw;
            }
        }

        private static bool IsValidDatabaseName(string dbName)
        {
            // Validar que el nombre solo contenga caracteres seguros
            // Formato esperado: ecom_tenant_{slug}
            return !string.IsNullOrWhiteSpace(dbName) 
                && dbName.Length <= 63 // Límite de PostgreSQL
                && System.Text.RegularExpressions.Regex.IsMatch(dbName, @"^[a-z0-9_]+$");
        }
    }
}
