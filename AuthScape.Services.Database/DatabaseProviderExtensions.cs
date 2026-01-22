using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthScape.Services.Database;

/// <summary>
/// Extension methods for configuring database providers in AuthScape.
/// </summary>
public static class DatabaseProviderExtensions
{
    /// <summary>
    /// Adds a DbContext with auto-detected provider based on the connection string.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="enableSensitiveDataLogging">Enable sensitive data logging (for development).</param>
    /// <param name="useOpenIddict">Whether to configure OpenIddict entity sets.</param>
    /// <param name="lifetime">The service lifetime. Defaults to Scoped.</param>
    public static IServiceCollection AddAuthScapeDatabase<TContext>(
        this IServiceCollection services,
        string connectionString,
        bool enableSensitiveDataLogging = false,
        bool useOpenIddict = true,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        var provider = DetectProvider(connectionString);

        services.AddDbContext<TContext>(options =>
        {
            ConfigureProvider(options, provider, connectionString);

            if (enableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }

            if (useOpenIddict)
            {
                options.UseOpenIddict();
            }
        }, lifetime);

        return services;
    }

    /// <summary>
    /// Detects the database provider from the connection string format.
    /// </summary>
    /// <param name="connectionString">The connection string to analyze.</param>
    /// <returns>The detected DatabaseProvider.</returns>
    public static DatabaseProvider DetectProvider(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        var connStr = connectionString.ToLowerInvariant();

        // SQLite detection - check first as it's most distinctive
        // Patterns: "Data Source=*.db", "Data Source=:memory:", "Filename=", "Mode=Memory"
        if (connStr.Contains("data source=") &&
            (connStr.Contains(".db") || connStr.Contains(":memory:") || connStr.Contains("mode=memory")))
        {
            return DatabaseProvider.SQLite;
        }
        if (connStr.Contains("filename="))
        {
            return DatabaseProvider.SQLite;
        }

        // PostgreSQL detection
        // Patterns: "Host=", "Username=" (not "User Id="), "Npgsql"
        if (connStr.Contains("host=") &&
            (connStr.Contains("username=") || connStr.Contains("password=")))
        {
            return DatabaseProvider.PostgreSQL;
        }

        // MySQL detection
        // Patterns: "Server=" with "User=" (not "User Id="), "Uid=", "Charset=", "SslMode=" with MySQL-style values
        if ((connStr.Contains("server=") || connStr.Contains("data source=")) &&
            (connStr.Contains("user=") && !connStr.Contains("user id=")) ||
            connStr.Contains("uid=") ||
            connStr.Contains("charset=") ||
            (connStr.Contains("sslmode=") && !connStr.Contains("ssl mode=")))
        {
            return DatabaseProvider.MySQL;
        }

        // SQL Server detection (default) - most common patterns
        // Patterns: "Server=", "Data Source=", "Initial Catalog=", "Integrated Security=",
        //           "Trusted_Connection=", "User Id=", "TrustServerCertificate="
        if (connStr.Contains("initial catalog=") ||
            connStr.Contains("integrated security=") ||
            connStr.Contains("trusted_connection=") ||
            connStr.Contains("trustservercertificate=") ||
            connStr.Contains("user id=") ||
            connStr.Contains("multipleactiveresultsets=") ||
            connStr.Contains("application name=") ||
            connStr.Contains("encrypt=") ||
            connStr.Contains(".database.windows.net") ||
            connStr.Contains("\\sqlexpress") ||
            connStr.Contains("server=tcp:"))
        {
            return DatabaseProvider.SqlServer;
        }

        // Default to SQL Server if we can't determine
        return DatabaseProvider.SqlServer;
    }

    /// <summary>
    /// Configures the DbContextOptionsBuilder with the appropriate database provider.
    /// </summary>
    /// <param name="options">The DbContext options builder.</param>
    /// <param name="provider">The database provider to use.</param>
    /// <param name="connectionString">The connection string.</param>
    public static DbContextOptionsBuilder ConfigureProvider(
        this DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => ConfigureSqlServer(options, connectionString),
            DatabaseProvider.PostgreSQL => ConfigurePostgreSQL(options, connectionString),
            DatabaseProvider.MySQL => ConfigureMySQL(options, connectionString),
            DatabaseProvider.SQLite => ConfigureSQLite(options, connectionString),
            _ => throw new ArgumentException($"Unsupported database provider: {provider}")
        };
    }

    private static DbContextOptionsBuilder ConfigureSqlServer(DbContextOptionsBuilder options, string connectionString)
    {
        return options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    }

    private static DbContextOptionsBuilder ConfigurePostgreSQL(DbContextOptionsBuilder options, string connectionString)
    {
        return options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });
    }

    private static DbContextOptionsBuilder ConfigureMySQL(DbContextOptionsBuilder options, string connectionString)
    {
        // MySQL support requires Pomelo.EntityFrameworkCore.MySql package
        // As of .NET 10, Pomelo hasn't released a compatible version yet.
        throw new NotSupportedException(
            "MySQL is not currently supported. Pomelo.EntityFrameworkCore.MySql does not yet have a .NET 10 compatible version. " +
            "Please use SQL Server, PostgreSQL, or SQLite instead.");
    }

    private static DbContextOptionsBuilder ConfigureSQLite(DbContextOptionsBuilder options, string connectionString)
    {
        // SQLite doesn't support retry on failure, but we configure it with sensible defaults
        return options.UseSqlite(connectionString);
    }

    /// <summary>
    /// Gets DbContextOptions for use with a DbContext constructor.
    /// Auto-detects the provider from the connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    public static DbContextOptions GetOptions(string connectionString)
    {
        var provider = DetectProvider(connectionString);
        var builder = new DbContextOptionsBuilder();
        ConfigureProvider(builder, provider, connectionString);
        return builder.Options;
    }

    /// <summary>
    /// Gets DbContextOptions for use with a DbContext constructor.
    /// Uses the specified provider.
    /// </summary>
    /// <param name="provider">The database provider.</param>
    /// <param name="connectionString">The connection string.</param>
    public static DbContextOptions GetOptions(DatabaseProvider provider, string connectionString)
    {
        var builder = new DbContextOptionsBuilder();
        ConfigureProvider(builder, provider, connectionString);
        return builder.Options;
    }
}
