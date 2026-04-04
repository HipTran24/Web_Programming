using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Web_Project.Infrastructure;

namespace Web_Project.Models
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var databaseProvider = DatabaseConnectionResolver.ResolveDatabaseProvider(configuration);

            if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                databaseProvider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.UseNpgsql(DatabaseConnectionResolver.ResolvePostgresConnectionString(configuration));
            }
            else
            {
                optionsBuilder.UseSqlServer(DatabaseConnectionResolver.ResolveSqlServerConnectionString(configuration));
            }

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
