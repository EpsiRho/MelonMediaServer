using System.Security.Claims;
using Melon.LocalClasses;

namespace MelonWebApi.Middleware
{
    /// <summary>Marks endpoints that serve media (audio, artwork) and so accept the read-only media token, including from <c>?jwt=</c>.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class MediaAccessAttribute : Attribute { }

    /// <summary>
    /// Which tokens a request may use, checked after the signature and expiry:
    /// <list type="bullet">
    /// <item>Media tokens (scope "media") only on <see cref="MediaAccessAttribute"/> endpoints.</item>
    /// <item>Session tokens (scope "api") only in the Authorization header.</item>
    /// <item>Every token names a session, and works only while that session is active (revoked devices are cut off within 30 seconds).</item>
    /// </list>
    /// </summary>
    public static class TokenRules
    {
        public const string FromQueryKey = "melon.token-from-query";

        /// <summary>Null when the token may be used for this request, otherwise why not.</summary>
        public static string? Check(HttpContext http, ClaimsPrincipal principal)
        {
            var scope = principal.FindFirstValue(AuthSessions.ScopeClaim);
            var sid = principal.FindFirstValue(AuthSessions.SessionClaim);
            bool fromQuery = http.Items.ContainsKey(FromQueryKey);
            bool mediaRoute = http.GetEndpoint()?.Metadata.GetMetadata<MediaAccessAttribute>() != null;

            switch (scope)
            {
                case AuthSessions.MediaScope:
                    if (!mediaRoute) return "Media tokens only work for media downloads.";
                    break;
                case AuthSessions.ApiScope:
                    if (fromQuery) return "Send this token in the Authorization header.";
                    break;
                case null:
                    return "This token is from an older sign-in method. Sign in again.";
                default:
                    return "Unknown token scope.";
            }

            if (sid == null) return "This token is from an older sign-in method. Sign in again.";
            if (!AuthSessions.IsActive(sid, principal.FindFirstValue(ClaimTypes.UserData) ?? ""))
                return "This device was signed out.";
            return null;
        }

        public static string? SessionId(ClaimsPrincipal user) => user.FindFirstValue(AuthSessions.SessionClaim);
    }
}
