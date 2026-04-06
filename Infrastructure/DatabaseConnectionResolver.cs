using Microsoft.Extensions.Configuration;

namespace Web_Project.Infrastructure;

public static class DatabaseConnectionResolver
{
    public static string ResolveSqlServerConnectionString(IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection")?.Trim();
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            return defaultConnection;
        }

        var dbHost = configuration["DB_HOST"]?.Trim();
        if (string.IsNullOrWhiteSpace(dbHost))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection or DB_HOST must be configured.");
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
}
