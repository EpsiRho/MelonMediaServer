using Azure;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;

namespace MelonWebApi.Middleware
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

            if (token == null)
            {
                //token = context.Request.Query.ToString();
                string jwt = context.Request.Query.Where(x=>x.Key=="jwt").FirstOrDefault().Value.ToString().Replace("\"", "");
                jwt = jwt.Replace("\"", "");
                if(jwt != null)
                {
                    var check = await AttachUserToContext(context, jwt);
                    if (!check)
                    {
                        return;
                    }
                }

            }

            if (token != null)
            {
                var check = await AttachUserToContext(context, token);
                if (!check)
                {
                    return;
                }
            }


            await _next(context);
        }

        private async Task<bool> AttachUserToContext(HttpContext context, string token)
        {
            try
            {
                IdentityModelEventSource.ShowPII = true;

                // Check if the endpoint has [Authorize] metadata
                var endpoint = context.GetEndpoint();

                if (endpoint != null)
                {
                    var authorizeMetadata = endpoint.Metadata.GetMetadata<IAuthorizeData>();

                    if (authorizeMetadata == null)
                    {
                        return true;
                    }
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = StateManager.JWTKey;

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.Zero,
                    TryAllIssuerSigningKeys = true
                };

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var jwtToken = validatedToken as JwtSecurityToken;
                if (jwtToken == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync("Invalid JWT token.");
                    return false;
                }

                var claimsIdentity = new ClaimsIdentity(jwtToken.Claims, "JWT");
                context.User = new ClaimsPrincipal(claimsIdentity);
                return true;
            }
            catch(Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsync("Invalid JWT token.");
                return false;
            }
        }
    }
}
