using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Services.Context
{
    /// <summary>
    /// Design-time factory used by the EF Core CLI (dotnet ef / Add-Migration / Update-Database).
    ///
    /// DatabaseContext has two constructors — (string) and (DbContextOptions) — which the EF tooling
    /// cannot disambiguate on its own. This factory removes that ambiguity and supplies the connection
    /// string for migrations.
    ///
    /// Connection string resolution:
    ///   1. AUTHSCAPE_MIGRATIONS_CONNECTION environment variable, if set.
    ///   2. Otherwise the local Docker SQL Server from docker-compose.yml.
    ///
    /// This is NOT used at runtime — the app builds its own context from AppSettings.DatabaseContext.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("AUTHSCAPE_MIGRATIONS_CONNECTION")
                ?? "Server=localhost,1433;Database=AuthScape;User Id=sa;Password=Your_password123;TrustServerCertificate=true;";

            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.GetName().Name))
                .Options;

            return new DatabaseContext(options);
        }
    }
}
