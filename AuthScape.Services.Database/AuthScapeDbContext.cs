using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthScape.Services.Database;

/// <summary>
/// Base DbContext for AuthScape applications. Provides multi-database provider support
/// (SQL Server, PostgreSQL, SQLite) with provider-aware GUID generation and filter syntax.
/// Inherit from this class and add your application-specific DbSets.
/// </summary>
public abstract class AuthScapeDbContext<TUser, TRole>
    : IdentityDbContext<TUser, TRole, long>, IDataProtectionKeyContext
    where TUser : IdentityUser<long>
    where TRole : IdentityRole<long>
{
    /// <summary>
    /// Creates a context with auto-detected provider based on connection string format.
    /// </summary>
    protected AuthScapeDbContext(string connectionString)
        : base(DatabaseProviderExtensions.GetOptions(connectionString))
    {
    }

    /// <summary>
    /// Creates a context with the specified options (used by DI).
    /// </summary>
    protected AuthScapeDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    /// <summary>
    /// Gets the appropriate UUID/GUID generation SQL for the current database provider.
    /// Use this in OnModelCreating for HasDefaultValueSql calls.
    /// </summary>
    protected string NewGuidSql => GetNewGuidSql();

    /// <summary>
    /// Gets the current database provider based on the configured options.
    /// </summary>
    protected DatabaseProvider CurrentProvider
    {
        get
        {
            if (Database.IsSqlServer()) return DatabaseProvider.SqlServer;
            if (Database.IsNpgsql()) return DatabaseProvider.PostgreSQL;
            if (Database.IsSqlite()) return DatabaseProvider.SQLite;
            // MySQL detection will be added when Pomelo releases .NET 10 compatible version
            return DatabaseProvider.SqlServer; // Default fallback
        }
    }

    /// <summary>
    /// Gets the appropriate UUID/GUID generation SQL for the current database provider.
    /// </summary>
    protected string GetNewGuidSql()
    {
        return CurrentProvider switch
        {
            DatabaseProvider.SqlServer => "newsequentialid()",
            DatabaseProvider.PostgreSQL => "gen_random_uuid()",
            DatabaseProvider.MySQL => "(UUID())",
            DatabaseProvider.SQLite => "(lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))))",
            _ => "newsequentialid()"
        };
    }

    /// <summary>
    /// Gets the appropriate filter syntax for nullable column checks.
    /// SQL Server uses [Column] IS NOT NULL, PostgreSQL uses "Column" IS NOT NULL, etc.
    /// </summary>
    protected string GetNotNullFilter(string columnName)
    {
        return CurrentProvider switch
        {
            DatabaseProvider.SqlServer => $"[{columnName}] IS NOT NULL",
            DatabaseProvider.PostgreSQL => $"\"{columnName}\" IS NOT NULL",
            DatabaseProvider.MySQL => $"`{columnName}` IS NOT NULL",
            DatabaseProvider.SQLite => $"\"{columnName}\" IS NOT NULL",
            _ => $"[{columnName}] IS NOT NULL"
        };
    }
}
