using Melon.LocalClasses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MelonWebApi.Middleware
{
    namespace ApiKeyAuthentication.Middlewares
    {
        public class JwtMiddleware
        {
            private readonly RequestDelegate _next;

            public JwtMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public async Task Invoke(HttpContext context)
            {
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

                if (token != null)
                    AttachUserToContext(context, token);

                await _next(context);
            }

            private void AttachUserToContext(HttpContext context, string token)
            {
                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var key = Encoding.ASCII.GetBytes(StateManager.MelonSettings.JWTKey);
                    tokenHandler.ValidateToken(token, new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    }, out SecurityToken validatedToken);

                    var jwtToken = (JwtSecurityToken)validatedToken;
                    context.User = (ClaimsPrincipal)(from claim in jwtToken.Claims
                                   where claim.Type == ClaimTypes.Name
                                   select claim.Value);
                }
                catch
                {
                    
                }
            }
        }
    }
}
