using Melon.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text;

namespace MelonWebApi.Middleware
{
    public class PluginMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase) { "password", "jwt", "code", "token" };

        public PluginMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.User.Identity.IsAuthenticated)
            {
                var userId = context.User.FindFirst(ClaimTypes.UserData)?.Value;
                var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
                var queryParameters = context.Request.Query;
                var parmameters = new Dictionary<string, object>();
                foreach(var p in queryParameters)
                {
                    // Plugins never see credentials or tokens that older clients still put in the query string.
                    if (SensitiveKeys.Contains(p.Key)) continue;
                    parmameters.Add(p.Key, p.Value.Select(x=>x).ToArray());
                }
                var args = new WebApiEventArgs(context.Request.Path.Value, userId, role, parmameters);

                var middlewares = Program.mWebApi.GetPluginMiddlewares();


                foreach (var middleware in middlewares)
                {
                    Func<WebApiEventArgs, byte[]> func = middleware.Value;
                    var res = func(args);

                    if (res != null)
                    {
                        await context.Response.Body.WriteAsync(res);
                        return;
                    }
                    // Proceed to the next middleware if not handling
                }

            }
            await _next(context);
        }
    }
}
