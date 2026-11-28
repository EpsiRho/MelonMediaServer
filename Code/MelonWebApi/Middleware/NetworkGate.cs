using System.Net;
using System.Security.Claims;
using Melon.LocalClasses;
using Melon.LocalClasses.Networking;

namespace MelonWebApi.Middleware;

/// <summary>
/// Who may talk to Melon at all, decided before anything else runs.
/// <list type="bullet">
/// <item>Blocked addresses get 403, whatever the mode.</item>
/// <item>In Automatic mode, addresses outside the home network get 403 unless Remote Access is on, and only over HTTPS. (A
/// reverse proxy on this computer arrives from loopback and is allowed; it does its own TLS.)</item>
/// </list>
/// </summary>
internal static class NetworkGate
{
    /// <summary>Runs on the raw connection address, before X-Forwarded-For is applied.</summary>
    public static async Task Outer(HttpContext context, Func<Task> next)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip == null) { await next(); return; }
        if (NetworkGuard.IsBlocked(ip)) { NetworkGuard.NoteBlocked(ip); await Refuse(context, "This address is blocked."); return; }
        var scope = NetAddresses.Scope(ip);
        if (scope is AddressScope.Loopback or AddressScope.Local || !RemoteAccessService.Automatic) { await next(); return; }

        var ra = StateManager.MelonSettings.RemoteAccess;
        string? why = null;
        if (!ra.Enabled) why = "Remote Access is off on this Melon server.";
        else if (!context.Request.IsHttps) why = "Connect with HTTPS from outside your network.";
        else if (scope == AddressScope.Public)
        {
            bool v6 = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !ip.IsIPv4MappedToIPv6;
            if (ra.Method != "Automatic") why = $"This Melon server is reached through {(ra.Method == "Tailscale" ? "Tailscale" : "its reverse proxy")}.";
            else if (v6 && !ra.UseIPv6) why = "IPv6 isn't turned on for Remote Access.";
        }
        if (why != null) { NetworkGuard.NoteRefused(ip, why); await Refuse(context, why); return; }
        RemoteAccessService.NoteOutside(ip);
        await next();
    }

    /// <summary>Runs on the client's address (after X-Forwarded-For from a local proxy): the blocklist and the outside-traffic tally.</summary>
    public static async Task Inner(HttpContext context, Func<Task> next)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip == null) { await next(); return; }
        if (NetworkGuard.IsBlocked(ip)) { NetworkGuard.NoteBlocked(ip); await Refuse(context, "This address is blocked."); return; }
        if (NetAddresses.Scope(ip) is AddressScope.Loopback or AddressScope.Local) { await next(); return; }
        try { await next(); }
        finally
        {
            try { NetworkGuard.Note(ip, context.Response.StatusCode, context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null); }
            catch (Exception) { }
        }
    }

    private static Task Refuse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(message);
    }
}
