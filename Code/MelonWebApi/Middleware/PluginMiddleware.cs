using Melon.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text;

namespace MelonWebApi.Middleware
{
    public class PluginMiddleware
    {
        private readonly RequestDelegate _next;

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
                    parmameters.Add(p.Key, p.Value.ToList());
                }
                var args = new WebApiEventArgs(context.Request.Path.Value, userId, role, parmameters);

                var middlewares = Program.mWebApi.GetPluginMiddlewares();


                foreach (var middleware in middlewares)
                {
                    Func<WebApiEventArgs, byte[]> func = middleware.Value;
                    var res = func(args);

                    if (res != null)
                    {
                        context.Response.Body.Write(res);
                        return;
                    }
                }

                // Proceed to the next middleware if not handling
            }
            await _next(context);
        }
    }
}
