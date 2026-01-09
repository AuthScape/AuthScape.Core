namespace Services.Database
{
    /// <summary>
    /// Supported database providers for AuthScape.
    /// Configure this in appsettings.json under AppSettings:DatabaseProvider
    /// </summary>
    public enum DatabaseProvider
    {
        /// <summary>
        /// Microsoft SQL Server (default)
        /// Connection string example: "Server=localhost;Database=AuthScape;Trusted_Connection=true;TrustServerCertificate=true;"
        /// </summary>
        SqlServer = 0,

        /// <summary>
        /// PostgreSQL
        /// Connection string example: "Host=localhost;Database=AuthScape;Username=postgres;Password=yourpassword"
        /// </summary>
        PostgreSQL = 1,

        /// <summary>
        /// MySQL or MariaDB
        /// Connection string example: "Server=localhost;Database=AuthScape;User=root;Password=yourpassword"
        /// </summary>
        MySQL = 2,

        /// <summary>
        /// SQLite (recommended for development/testing only)
        /// Connection string example: "Data Source=AuthScape.db"
        /// </summary>
        SQLite = 3
    }

    /// <summary>
    /// Provides connection string examples for each database provider.
    /// </summary>
    public static class DatabaseProviderConnectionStrings
    {
        /// <summary>
        /// SQL Server connection string examples
        /// </summary>
        public static class SqlServer
        {
            /// <summary>
            /// Windows Authentication (Integrated Security)
            /// </summary>
            public const string WindowsAuth = "Server=localhost;Database=AuthScape;Trusted_Connection=true;TrustServerCertificate=true;";

            /// <summary>
            /// SQL Server Authentication (Username/Password)
            /// </summary>
            public const string SqlAuth = "Server=localhost;Database=AuthScape;User Id=sa;Password=yourpassword;TrustServerCertificate=true;";

            /// <summary>
            /// Azure SQL Database
            /// </summary>
            public const string AzureSql = "Server=tcp:yourserver.database.windows.net,1433;Database=AuthScape;User Id=yourusername;Password=yourpassword;Encrypt=true;";

            /// <summary>
            /// Local SQL Server Express
            /// </summary>
            public const string SqlExpress = "Server=.\\SQLEXPRESS;Database=AuthScape;Trusted_Connection=true;TrustServerCertificate=true;";
        }

        /// <summary>
        /// PostgreSQL connection string examples
        /// </summary>
        public static class PostgreSQL
        {
            /// <summary>
            /// Standard PostgreSQL connection
            /// </summary>
            public const string Standard = "Host=localhost;Database=authscape;Username=postgres;Password=yourpassword;";

            /// <summary>
            /// PostgreSQL with port specified
            /// </summary>
            public const string WithPort = "Host=localhost;Port=5432;Database=authscape;Username=postgres;Password=yourpassword;";

            /// <summary>
            /// PostgreSQL with SSL
            /// </summary>
            public const string WithSsl = "Host=localhost;Database=authscape;Username=postgres;Password=yourpassword;SSL Mode=Require;Trust Server Certificate=true;";

            /// <summary>
            /// Azure Database for PostgreSQL
            /// </summary>
            public const string Azure = "Host=yourserver.postgres.database.azure.com;Database=authscape;Username=yourusername@yourserver;Password=yourpassword;SSL Mode=Require;";
        }

        /// <summary>
        /// MySQL/MariaDB connection string examples
        /// </summary>
        public static class MySQL
        {
            /// <summary>
            /// Standard MySQL connection
            /// </summary>
            public const string Standard = "Server=localhost;Database=authscape;User=root;Password=yourpassword;";

            /// <summary>
            /// MySQL with port specified
            /// </summary>
            public const string WithPort = "Server=localhost;Port=3306;Database=authscape;User=root;Password=yourpassword;";

            /// <summary>
            /// MariaDB connection
            /// </summary>
            public const string MariaDB = "Server=localhost;Database=authscape;User=root;Password=yourpassword;";

            /// <summary>
            /// Azure Database for MySQL
            /// </summary>
            public const string Azure = "Server=yourserver.mysql.database.azure.com;Database=authscape;User=yourusername@yourserver;Password=yourpassword;SslMode=Required;";
        }

        /// <summary>
        /// SQLite connection string examples
        /// </summary>
        public static class SQLite
        {
            /// <summary>
            /// File-based SQLite database
            /// </summary>
            public const string File = "Data Source=AuthScape.db;";

            /// <summary>
            /// In-memory SQLite database (data is lost when connection closes)
            /// </summary>
            public const string InMemory = "Data Source=:memory:;";

            /// <summary>
            /// SQLite with full path
            /// </summary>
            public const string FullPath = "Data Source=C:\\Data\\AuthScape.db;";

            /// <summary>
            /// Shared in-memory database (persists across connections with same name)
            /// </summary>
            public const string SharedInMemory = "Data Source=AuthScape;Mode=Memory;Cache=Shared;";
        }

        /// <summary>
        /// Gets an example connection string for the specified provider.
        /// </summary>
        public static string GetExample(DatabaseProvider provider)
        {
            return provider switch
            {
                DatabaseProvider.SqlServer => SqlServer.WindowsAuth,
                DatabaseProvider.PostgreSQL => PostgreSQL.Standard,
                DatabaseProvider.MySQL => MySQL.Standard,
                DatabaseProvider.SQLite => SQLite.File,
                _ => SqlServer.WindowsAuth
            };
        }
    }
}
