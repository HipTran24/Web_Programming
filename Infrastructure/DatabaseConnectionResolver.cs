using Microsoft.Extensions.Configuration;

namespace Web_Project.Infrastructure;

public static class DatabaseConnectionResolver
{
    public static string ResolveDatabaseProvider(IConfiguration configuration)
    {
        var configuredProvider = configuration["DatabaseProvider"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return configuredProvider;
        }

        if (!string.IsNullOrWhiteSpace(configuration["DATABASE_URL"]))
        {
            return "PostgreSQL";
        }

        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(defaultConnection) &&
            (defaultConnection.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
             defaultConnection.Contains("Username=", StringComparison.OrdinalIgnoreCase)))
        {
            return "PostgreSQL";
        }

        return "SqlServer";
    }

    public static string ResolveSqlServerConnectionString(IConfiguration configuration)
    {
        var dbHost = configuration["DB_HOST"]?.Trim();
        if (string.IsNullOrWhiteSpace(dbHost))
        {
            return configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        }

        var dbPort = configuration["DB_PORT"]?.Trim();
        var dbName = configuration["DB_NAME"]?.Trim();
        var dbUser = configuration["DB_USER"]?.Trim();
        var dbPassword = configuration["DB_PASSWORD"]?.Trim();
        var dbEncrypt = configuration["DB_ENCRYPT"]?.Trim();
        var dbTrustServerCertificate = configuration["DB_TRUST_SERVER_CERTIFICATE"]?.Trim();

        if (string.IsNullOrWhiteSpace(dbPassword))
        {
            dbPassword = configuration["DB_SA_PASSWORD"]?.Trim();
        }

        return $"Server={dbHost},{(string.IsNullOrWhiteSpace(dbPort) ? "1433" : dbPort)};" +
               $"Database={(string.IsNullOrWhiteSpace(dbName) ? "myDB" : dbName)};" +
               $"User Id={(string.IsNullOrWhiteSpace(dbUser) ? "sa" : dbUser)};" +
               $"Password={dbPassword};" +
               $"TrustServerCertificate={(string.IsNullOrWhiteSpace(dbTrustServerCertificate) ? "True" : dbTrustServerCertificate)};" +
               $"Encrypt={(string.IsNullOrWhiteSpace(dbEncrypt) ? "False" : dbEncrypt)};";
    }

    public static string ResolvePostgresConnectionString(IConfiguration configuration)
    {
        var databaseUrl = configuration["DATABASE_URL"]?.Trim();
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return databaseUrl;
        }

        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(defaultConnection) &&
            (defaultConnection.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
             defaultConnection.Contains("Username=", StringComparison.OrdinalIgnoreCase)))
        {
            return defaultConnection;
        }

        var dbHost = configuration["PGHOST"]?.Trim()
            ?? configuration["POSTGRES_HOST"]?.Trim()
            ?? configuration["DB_HOST"]?.Trim();
        var dbPort = configuration["PGPORT"]?.Trim()
            ?? configuration["POSTGRES_PORT"]?.Trim()
            ?? configuration["DB_PORT"]?.Trim();
        var dbName = configuration["PGDATABASE"]?.Trim()
            ?? configuration["POSTGRES_DB"]?.Trim()
            ?? configuration["DB_NAME"]?.Trim();
        var dbUser = configuration["PGUSER"]?.Trim()
            ?? configuration["POSTGRES_USER"]?.Trim()
            ?? configuration["DB_USER"]?.Trim();
        var dbPassword = configuration["PGPASSWORD"]?.Trim()
            ?? configuration["POSTGRES_PASSWORD"]?.Trim()
            ?? configuration["DB_PASSWORD"]?.Trim();
        var sslMode = configuration["POSTGRES_SSL_MODE"]?.Trim()
            ?? configuration["PGSSLMODE"]?.Trim()
            ?? "Prefer";
        var trustServerCertificate = configuration["POSTGRES_TRUST_SERVER_CERTIFICATE"]?.Trim() ?? "true";

        if (string.IsNullOrWhiteSpace(dbHost))
        {
            throw new InvalidOperationException("PostgreSQL is selected but no PostgreSQL connection information is configured.");
        }

        return $"Host={dbHost};" +
               $"Port={(string.IsNullOrWhiteSpace(dbPort) ? "5432" : dbPort)};" +
               $"Database={(string.IsNullOrWhiteSpace(dbName) ? "postgres" : dbName)};" +
               $"Username={(string.IsNullOrWhiteSpace(dbUser) ? "postgres" : dbUser)};" +
               $"Password={dbPassword};" +
               $"SSL Mode={sslMode};" +
               $"Trust Server Certificate={trustServerCertificate};";
    }
}
