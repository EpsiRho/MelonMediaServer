using System.Security.Claims;
using Melon.LocalClasses;
using Melon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nova.Dsp;

namespace MelonWebApi.Controllers
{
    /// <summary>
    /// Saved EQ filters. Each user has their own; the owner can make one public so others can pick it. Pass a
    /// filter's id as <c>eq</c> on api/download/track or api/download/track-transcode to hear it applied.
    /// </summary>
    [ApiController]
    [Route("api/eq")]
    [Authorize(Roles = "Admin,User")]
    public class EqController : ControllerBase
    {
        private string CurrentUser => ((ClaimsIdentity)User.Identity!).Claims
            .Where(c => c.Type == ClaimTypes.UserData).Select(c => c.Value).FirstOrDefault() ?? "";

        private WebApiEventArgs Args(string route, Dictionary<string, object>? parameters = null) =>
            new(route, CurrentUser, parameters ?? new Dictionary<string, object>());

        /// <summary>
        /// The filters the user can pick: their own plus public ones (<paramref name="mine"/> for only their own),
        /// sorted by name, each with its owner's name. <paramref name="name"/> filters by a case-insensitive substring.
        /// </summary>
        [HttpGet("list")]
        public IActionResult List(int page = 0, int count = 100, bool mine = false, string name = "")
        {
            var args = Args("api/eq/list", new() { { "page", page }, { "count", count }, { "mine", mine }, { "name", name } });
            count = Math.Clamp(count, 1, 500);
            var filters = EqFilters.List(CurrentUser, mine, name, page, count);
            args.SendEvent("EQ filters sent", 200, Program.mWebApi);
            return Ok(filters);
        }

        /// <summary>One filter, if it's the user's or public.</summary>
        [HttpGet("get")]
        public IActionResult Get(string id)
        {
            var args = Args("api/eq/get", new() { { "id", id } });
            var filter = EqFilters.FindUsable(id, CurrentUser);
            if (filter == null)
            {
                args.SendEvent("EQ filter not found", 404, Program.mWebApi);
                return NotFound("EQ filter not found");
            }
            args.SendEvent("EQ filter sent", 200, Program.mWebApi);
            return Ok(EqFilters.ToResponse(filter));
        }

        /// <summary>Saves a new filter owned by the user. Returns its id.</summary>
        [HttpPost("add")]
        public IActionResult Add([FromBody] EqFilterRequest filter)
        {
            var args = Args("api/eq/add", new() { { "name", filter?.Name ?? "" } });
            try
            {
                var id = EqFilters.Add(CurrentUser, filter!);
                args.SendEvent("EQ filter added", 200, Program.mWebApi);
                return Ok(id);
            }
            catch (ArgumentException ex)
            {
                args.SendEvent(ex.Message, 400, Program.mWebApi);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Replaces the name, bands and gain of one of the user's filters (IsPublic too, if given).</summary>
        [HttpPost("update")]
        public IActionResult Update(string id, [FromBody] EqFilterRequest filter)
        {
            var args = Args("api/eq/update", new() { { "id", id } });
            try
            {
                if (!EqFilters.Update(id, CurrentUser, filter!))
                {
                    args.SendEvent("EQ filter not found", 404, Program.mWebApi);
                    return NotFound("EQ filter not found");
                }
            }
            catch (ArgumentException ex)
            {
                args.SendEvent(ex.Message, 400, Program.mWebApi);
                return BadRequest(ex.Message);
            }
            args.SendEvent("EQ filter updated", 200, Program.mWebApi);
            return Ok("EQ filter updated");
        }

        /// <summary>Shows or hides one of the user's filters from other users.</summary>
        [HttpPost("set-public")]
        public IActionResult SetPublic(string id, bool isPublic)
        {
            var args = Args("api/eq/set-public", new() { { "id", id }, { "isPublic", isPublic } });
            if (!EqFilters.SetPublic(id, CurrentUser, isPublic))
            {
                args.SendEvent("EQ filter not found", 404, Program.mWebApi);
                return NotFound("EQ filter not found");
            }
            args.SendEvent(isPublic ? "EQ filter made public" : "EQ filter made private", 200, Program.mWebApi);
            return Ok(isPublic ? "EQ filter is public" : "EQ filter is private");
        }

        /// <summary>Deletes one of the user's filters (admins can delete any).</summary>
        [HttpPost("delete")]
        [HttpDelete("delete")]
        public IActionResult Delete(string id)
        {
            var args = Args("api/eq/delete", new() { { "id", id } });
            if (!EqFilters.Delete(id, CurrentUser, User.IsInRole("Admin")))
            {
                args.SendEvent("EQ filter not found", 404, Program.mWebApi);
                return NotFound("EQ filter not found");
            }
            args.SendEvent("EQ filter deleted", 200, Program.mWebApi);
            return Ok("EQ filter deleted");
        }

        /// <summary>
        /// A saved filter's response curve for drawing: gain in dB at <paramref name="points"/> log-spaced
        /// frequencies from 20 Hz to 20 kHz, as applied to a track at <paramref name="sampleRate"/>.
        /// </summary>
        [HttpGet("response")]
        public IActionResult GetResponse(string id, int points = 256, int sampleRate = 44100)
        {
            var filter = EqFilters.FindUsable(id, CurrentUser);
            if (filter == null) return NotFound("EQ filter not found");
            return Curve(EqFilters.ToSettings(filter), points, sampleRate);
        }

        /// <summary>The response curve of unsaved settings, so editors can preview before saving.</summary>
        [HttpPost("response")]
        public IActionResult PreviewResponse([FromBody] EqFilterRequest filter, int points = 256, int sampleRate = 44100)
        {
            if (filter == null) return BadRequest("A filter is required.");
            var settings = EqFilters.ToSettings(filter.Preamp, filter.AutoHeadroom, filter.Bands);
            var error = settings.Validate();
            return error != null ? BadRequest(error) : Curve(settings, points, sampleRate);
        }

        private IActionResult Curve(EqualizerSettings settings, int points, int sampleRate)
        {
            if (points is < 2 or > 4096) return BadRequest("Points must be between 2 and 4096.");
            if (sampleRate is < 8000 or > 768000) return BadRequest("Sample rate must be between 8000 and 768000.");
            var frequencies = Equalizer.LogFrequencies(points, sampleRate);
            return Ok(new EqResponse
            {
                Frequencies = frequencies,
                Gains = Equalizer.ResponseDb(settings, sampleRate, frequencies),
                OutputGain = Equalizer.ComputeOutputGainDb(settings, sampleRate),
                SampleRate = sampleRate,
            });
        }
    }
}
