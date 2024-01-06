using Melon.Classes;
using Melon.LocalClasses;
using MelonWebApi.Middleware.ApiKeyAuthentication.Middlewares;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Specialized;
using System.Reflection;
using System.Text;
namespace MelonWebApi
{
    public static class Program
    {
        public static bool started = false;
        public static WebApplication app;
        public static void Main(string[] args)
        {
            if (!started)
            {
                StateManager.Init();
            }

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddControllers();

            builder.Logging.ClearProviders();
            
            if (args.Contains("-headless"))
            {
                Log.Logger = new LoggerConfiguration()
                        .WriteTo.File($"{StateManager.melonPath}/logs.txt")
                        .WriteTo.Console()
                        .CreateLogger();
            }
            else
            {
                Log.Logger = new LoggerConfiguration()
                        .WriteTo.File($"{StateManager.melonPath}/logs.txt")
                        .CreateLogger();
            }

            builder.Host.UseSerilog();

            var key = Encoding.ASCII.GetBytes(StateManager.MelonSettings.JWTKey);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(x =>
                    {
                        x.RequireHttpsMetadata = false; // Set to true in production
                        x.SaveToken = true;
                        x.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(key),
                            ValidateIssuer = false,
                            ValidateAudience = false
                        };
                    });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                // Use method name as operationId
                c.CustomOperationIds(apiDesc =>
                {
                    return apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null;
                });
            });

            app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseMiddleware<JwtMiddleware>();

            app.UseSwagger(options =>
            {
                options.SerializeAsV2 = true;
            });

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                options.RoutePrefix = string.Empty;
            });

            app.MapControllers();

            app.RunAsync();

            if (!started && !args.Contains("-headless"))
            {
                started = true;
                Console.ForegroundColor = ConsoleColor.White;
                Console.OutputEncoding = Encoding.UTF8;

                // Melon Startup

                // UI Startup
                DisplayManager.DisplayHome();
            }
            else
            {
                app.WaitForShutdown();
            }
        }
    }
}
