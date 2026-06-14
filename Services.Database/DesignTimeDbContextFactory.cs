using System;
using System.IO;
using AuthScape.Services.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Services.Context
{
    /// <summary>
    /// Design-time factory used by the EF Core CLI (dotnet ef / Add-Migration / Update-Database) and
    /// any other design-time tooling (e.g. dbcontext info/scaffold).
    ///
    /// DatabaseContext has two constructors — (string) and (DbContextOptions) — which the EF tooling
    /// cannot disambiguate on its own. This factory removes that ambiguity and supplies the connection
    /// string.
    ///
    /// The provider is auto-detected from the connection string (SQL Server, PostgreSQL, or SQLite) via
    /// <see cref="DatabaseProviderExtensions"/> — exactly the same selection the app uses at runtime —
    /// so design-time tooling works against whichever database the configured connection string targets.
    ///
    /// Connection string resolution (first match wins):
    ///   1. AUTHSCAPE_MIGRATIONS_CONNECTION environment variable.
    ///   2. AppSettings:DatabaseContext from appsettings(.Development).json in the current directory
    ///      (the startup project's directory when invoked via dotnet ef).
    ///   3. The local Docker SQL Server from docker-compose.yml (last-resort dev default).
    ///
    /// This is NOT used at runtime — the app builds its own context from AppSettings.DatabaseContext.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("AUTHSCAPE_MIGRATIONS_CONNECTION")
                ?? ConnectionStringFromConfiguration()
                ?? "Server=localhost,1433;Database=AuthScape;User Id=sa;Password=Your_password123;TrustServerCertificate=true;";

            // Reuse the runtime provider auto-detection (SQL Server / PostgreSQL / SQLite) so the
            // factory is never pinned to a single provider.
            var provider = DatabaseProviderExtensions.DetectProvider(connectionString);

            var builder = new DbContextOptionsBuilder<DatabaseContext>();
            builder.ConfigureProvider(provider, connectionString);

            return new DatabaseContext(builder.Options);
        }

        private static string? ConnectionStringFromConfiguration()
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                return configuration["AppSettings:DatabaseContext"];
            }
            catch
            {
                return null;
            }
        }
    }
}
