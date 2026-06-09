using System;

namespace AuthScape.Services.Keycloak
{
    public class KeycloakAdminException : Exception
    {
        public KeycloakAdminFailureKind Kind { get; }

        public KeycloakAdminException(KeycloakAdminFailureKind kind, string message, Exception inner = null)
            : base(message, inner)
        {
            Kind = kind;
        }
    }

    public enum KeycloakAdminFailureKind
    {
        Disabled,
        Misconfigured,
        Unreachable,
        Unauthorized,
        Unexpected
    }
}
