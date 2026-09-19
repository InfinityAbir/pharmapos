namespace PharmaPOS.Application.Common.Authorization;

/// <summary>
/// The permission catalog. Permission codes are the contract between the server (which enforces
/// them) and Keycloak (which can surface them as a <c>permissions</c> claim). Business-module
/// permissions are added by their own phases; Phase 0 defines platform/security permissions only.
///
/// Codes are never trusted from the client — they are either carried in a validated JWT by the
/// identity provider or resolved server-side from tenant membership.
/// </summary>
public static class Permissions
{
    public static class Platform
    {
        public const string TenantsManage = "platform.tenants.manage";
        public const string UsersManage = "platform.users.manage";
        public const string AuditRead = "platform.audit.read";
    }
}
