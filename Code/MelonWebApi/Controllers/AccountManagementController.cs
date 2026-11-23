using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MelonWebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountManagementController : ControllerBase
{
    private static ServerAdministration Admin => ServerAdministration.Instance;

    [HttpGet("api/users/manage")]
    public IActionResult ListUsers() => Ok(Admin.GetUsers());

    [HttpPost("api/users/{id}/password-reset-code")]
    public IActionResult GenerateReset(string id)
    {
        try { return Ok(Admin.CreatePasswordReset(id)); }
        catch (ArgumentException) { return NotFound("User not found."); }
    }

    [HttpGet("auth/invites")]
    public IActionResult ListInvites() => Ok(Admin.GetInvites());

    [HttpPost("auth/invites/revoke")]
    public IActionResult Revoke([FromBody] string code) { Admin.RevokeInvite(code); return NoContent(); }

    [HttpGet("api/stats/summary")]
    public IActionResult Summary(string userId = "", int days = 7)
    {
        try { return Ok(Admin.GetListeningSummary(userId, days)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    public sealed record ResetRequest(string Code, string Password);

    [AllowAnonymous]
    [HttpGet("auth/reset-password")]
    public IActionResult ResetForm() => PhysicalFile(Path.Combine(AppContext.BaseDirectory, "wwwroot", "reset-password.html"), "text/html; charset=utf-8");

    [AllowAnonymous]
    [EnableRateLimiting(Program.AuthRateLimit)]
    [HttpPost("auth/reset-password")]
    public IActionResult CompleteReset([FromBody] ResetRequest request)
    {
        try { return Admin.ResetPassword(request.Code.Trim(), request.Password) ? NoContent() : BadRequest("Invalid or expired reset code."); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
