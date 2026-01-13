namespace AuthScape.Models;

/// <summary>
/// Specifies which configuration source to prioritize when loading AuthScape settings.
/// </summary>
public enum ConfigurationSource
{
    /// <summary>
    /// Use the default cascade priority:
    /// 1. Shared authscape.json (lowest)
    /// 2. Project appsettings.json
    /// 3. User Secrets (development)
    /// 4. Environment Variables
    /// 5. Azure Key Vault / AWS Secrets Manager (highest)
    /// </summary>
    Default = 0,

    /// <summary>
    /// Prioritize project appsettings.json over shared configuration.
    /// Use this when you want project-specific settings to take precedence.
    /// Order: Shared JSON → appsettings.json → Env Variables → Secrets
    /// </summary>
    ProjectSettings = 1,

    /// <summary>
    /// Prioritize shared authscape.json configuration.
    /// Use this for centralized configuration across multiple projects.
    /// Order: appsettings.json → Shared JSON → Env Variables → Secrets
    /// </summary>
    SharedSettings = 2,

    /// <summary>
    /// Prioritize environment variables over JSON files.
    /// Use this for containerized deployments (Docker, Kubernetes).
    /// Order: JSON files → Environment Variables → Secrets
    /// </summary>
    EnvironmentVariables = 3,

    /// <summary>
    /// Only use project appsettings.json, ignore all other sources.
    /// Use this for isolated development or testing.
    /// No shared config, no environment variables, no secrets.
    /// </summary>
    ProjectOnly = 4,

    /// <summary>
    /// Only use shared authscape.json, ignore project-specific settings.
    /// Use this when all configuration should come from centralized config.
    /// No appsettings.json, no environment variables, no secrets.
    /// </summary>
    SharedOnly = 5,

    /// <summary>
    /// Only use environment variables, ignore all JSON files.
    /// Use this for fully externalized configuration in cloud environments.
    /// No JSON files, only environment variables and secrets.
    /// </summary>
    EnvironmentOnly = 6
}
