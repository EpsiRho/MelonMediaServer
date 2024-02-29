using Melon.Classes;
using Melon.DisplayClasses;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Specialized;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.RateLimiting;
using Melon.Models;
using NuGet.Protocol.Plugins;
using MelonWebApi.Middleware;
namespace MelonWebApi
{
    public static class Program
    {
        //public static bool started = false;
        public static WebApplication app;
        public static MWebApi mWebApi;
        public static async Task<int> Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.OutputEncoding = Encoding.UTF8;
            bool openFolder = args.Contains("--openFolder");
            if (openFolder)
            {
                SettingsUI.OpenMelonFolder();
                Environment.Exit(0);
            }

            bool headless = args.Contains("--headless") || args.Contains("-h");
            bool setup = args.Contains("--setup");
            bool disablePlugins = args.Contains("--disablePlugins");
            string lang = args.Where(x => x.Contains("--lang")).FirstOrDefault() != null ? args.Where(x=>x.Contains("--lang")).FirstOrDefault().Split("=")[1] : "";
            StateManager.RestartServer = true;
            while (StateManager.RestartServer)
            {
                StateManager.RestartServer = false;
                mWebApi = new MWebApi();
                StateManager.Init(headless, setup, disablePlugins, lang, mWebApi);

                if (headless && DisplayManager.UIExtensions.Count() != 0)
                {
                    SetupUI.ShowSetupError();
                    return -1;
                }

                var builder = WebApplication.CreateBuilder();

                builder.Services.AddControllers();

                builder.Logging.ClearProviders();

                builder.WebHost.UseUrls(StateManager.MelonSettings.ListeningURL);

                // Load SSL Certificate
                var sslConfig = Security.GetSSLConfig();

                if (sslConfig.PathToCert != "")
                {
                    var certificate = new X509Certificate2(sslConfig.PathToCert, sslConfig.Password);

                    // Configure Kestrel to use SSL
                    builder.WebHost.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.ConfigureHttpsDefaults(httpsOptions =>
                        {
                            httpsOptions.ServerCertificate = certificate;
                        });
                    });
                }



                if (args.Contains("--headless") || args.Contains("-h"))
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

                var key = StateManager.MelonSettings.JWTKey;
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
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Melon Web API", Version = "v1.0.0" });

                    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                    c.IncludeXmlComments(xmlPath);

                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = @"JWT Authorization header using the Bearer scheme.",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer"
                    });

                    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                    {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header,

                        },
                        new List<string>()
                    }
                    });
                });

                builder.Services.Configure<KestrelServerOptions>(options =>
                {
                    options.AllowSynchronousIO = true;
                });

                app = builder.Build();

                app.UseHttpsRedirection();

                app.UseAuthentication();

                app.UseAuthorization();

                app.UseMiddleware<JwtMiddleware>();
                app.UseMiddleware<PluginMiddleware>();

                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromMinutes(2)
                };

                app.UseWebSockets(webSocketOptions);

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

                //var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
                //
                //lifetime.ApplicationStopping.Register(() =>
                //{
                //
                //});

                app.RunAsync();

                if (!args.Contains("--headless") && !args.Contains("-h"))
                {


                    // Melon Startup

                    // UI Startup
                    DisplayManager.DisplayHome();
                }
                else
                {
                    StateManager.RestartServer = false;
                    app.WaitForShutdown();
                }

                await app.StopAsync();
            }
            return 0;
        }
    }
}
