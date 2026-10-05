using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Stockroom.Web;

// Security events (release security review R-02). Every event uses this class's log category,
// "Stockroom.Web.SecurityEvents", and a stable event ID. Messages carry account identifiers and request metadata only:
// never passwords, cookie values, tokens, form bodies, or the email submitted for an account that does not exist.
public sealed class SecurityEvents
{
    // Remote address of the client. Test hosts supply none.
    public static string ClientAddress(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public static partial class SecurityLog
{
    [LoggerMessage(EventId = 1001, EventName = nameof(SignInSucceeded), Level = LogLevel.Information,
        Message = "Sign-in succeeded for user {UserId} from {ClientAddress}.")]
    public static partial void SignInSucceeded(this ILogger logger, string userId, string clientAddress);

    [LoggerMessage(EventId = 1002, EventName = nameof(SignInFailed), Level = LogLevel.Warning,
        Message = "Sign-in failed for user {UserId} from {ClientAddress}: {Reason}.")]
    public static partial void SignInFailed(this ILogger logger, string userId, string clientAddress, string reason);

    [LoggerMessage(EventId = 1003, EventName = nameof(SignInLockedOut), Level = LogLevel.Warning,
        Message = "Sign-in refused for user {UserId} from {ClientAddress}: the account is locked out until {LockoutEnd}.")]
    public static partial void SignInLockedOut(this ILogger logger, string userId, string clientAddress, string lockoutEnd);

    [LoggerMessage(EventId = 1004, EventName = nameof(SignedOut), Level = LogLevel.Information,
        Message = "User {UserId} signed out from {ClientAddress}. Other sessions revoked: {SessionsRevoked}.")]
    public static partial void SignedOut(this ILogger logger, string userId, string clientAddress, bool sessionsRevoked);

    [LoggerMessage(EventId = 1005, EventName = nameof(LoginThrottled), Level = LogLevel.Warning,
        Message = "Login attempt from {ClientAddress} rejected by the rate limit: {Method} {Path}.")]
    public static partial void LoginThrottled(this ILogger logger, string clientAddress, string method, string path);

    [LoggerMessage(EventId = 1006, EventName = nameof(AccessDenied), Level = LogLevel.Warning,
        Message = "Access denied for user {UserId}: {Method} {Path} requires {Requirement}.")]
    public static partial void AccessDenied(this ILogger logger, string userId, string method, string path, string requirement);

    [LoggerMessage(EventId = 1007, EventName = nameof(PermissionRefused), Level = LogLevel.Warning,
        Message = "Permission refused for user {UserId}: {Action} requires {Requirement}.")]
    public static partial void PermissionRefused(this ILogger logger, string userId, string action, string requirement);
}

// Logs page and endpoint authorization denials for signed-in users, then applies the framework's default handling
// (a redirect to the access-denied page). Anonymous requests are challenged to log in and are not logged here.
public sealed class SecurityEventAuthorizationHandler(ILogger<SecurityEvents> securityLog) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            securityLog.AccessDenied(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                Describe(context.GetEndpoint()));
        }
        return defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    // Names the roles and policies the endpoint requires, from its [Authorize] metadata.
    private static string Describe(Endpoint? endpoint)
    {
        var parts = (endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? [])
            .Select(data => data.Roles is { Length: > 0 } roles ? $"role {roles}"
                : data.Policy is { Length: > 0 } name ? $"policy {name}"
                : null)
            .OfType<string>()
            .Distinct()
            .ToList();
        return parts.Count > 0 ? string.Join(" and ", parts) : "an authorization policy";
    }
}
