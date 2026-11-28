using Melon.LocalClasses;
using Melon.LocalClasses.Networking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MelonWebApi.Controllers;

/// <summary>How clients find this server again: which server it is, and every address it can be reached at.</summary>
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ConnectionController : ControllerBase
{
    /// <summary>
    /// Which Melon server this is, before signing in. Apps try several saved addresses at once and keep the first one where
    /// the id matches (and the HTTPS certificate matches the fingerprint they pinned).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("api/server/identity")]
    public IActionResult Identity() => Ok(new
    {
        id = NetworkIdentity.ServerId,
        name = LocalBroadcast.NameFor(StateManager.MelonSettings.BroadcastName),
        version = StateManager.Version,
        fingerprint = RemoteAccessService.Fingerprint(),
    });

    /// <summary>
    /// The server's addresses (home network, public, Tailscale, reverse proxy), its certificate fingerprint, Pkarr key and
    /// Remote Access status. Signed-in users only: it includes the home's public address. Read-only: Remote Access is
    /// changed only from MelonGUI or MelonCLI on the server.
    /// </summary>
    [Authorize(Roles = "Admin,User,Pass")]
    [HttpGet("api/server/connection")]
    public IActionResult Connection() => Ok(RemoteAccessService.Connection());
}
