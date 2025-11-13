namespace Authscape.IdentityServer.Models
{
    public enum ClientType
    {
        /// <summary>
        /// Single Page Application (SPA) - Authorization Code + PKCE
        /// </summary>
        Spa,

        /// <summary>
        /// Web Application - Authorization Code with client secret
        /// </summary>
        Web,

        /// <summary>
        /// Native Application - Authorization Code + PKCE
        /// </summary>
        Native,

        /// <summary>
        /// Machine to Machine - Client Credentials Flow
        /// </summary>
        Machine,

        /// <summary>
        /// Device Flow - For input-constrained devices
        /// </summary>
        Device,

        /// <summary>
        /// Single Page Application (Legacy) - Implicit Flow
        /// </summary>
        SpaLegacy
    }
}
