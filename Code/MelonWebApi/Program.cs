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
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Identity.Client;
using System.Runtime.Versioning;
using Melon.PluginModels;
using Microsoft.Extensions.FileSystemGlobbing;
namespace MelonWebApi
{
    public static class Program
    {
        //public static bool started = false;
        public static WebApplication app;
        public static MWebApi mWebApi;
        public static FileSystemWatcher watcher;
        public const string Version = "1.0.84.279";

        public static async Task<int> Main(string[] args)
        {
            StateManager.Version = Version;
            StateManager.ParseArgs(args);
            StateManager.ServerIsAlive = true;
            MelonColor.SetDefaults();
            //StateManager.ConsoleIsAlive = true;

            if (StateManager.LaunchArgs.ContainsKey("openFolder"))
            {
                SettingsUI.OpenMelonFolder();
                Environment.Exit(0);
            }

            if (StateManager.LaunchArgs.ContainsKey("help") || StateManager.LaunchArgs.ContainsKey("version"))
            {
                StateManager.Init(null, true, false);
                return 0;
            }

            if (OperatingSystem.IsWindows() && !StateManager.LaunchArgs.ContainsKey("headless"))
            {
                StateManager.ConsoleIsAlive = false;

                TrayIconManager.HideConsole();
                TrayIconManager.ShowConsole();
            }
            else if (!StateManager.LaunchArgs.ContainsKey("headless"))
            {
                TrayIconManager.ShowConsole();
            }

            StateManager.RestartServer = true;
            while (StateManager.RestartServer)
            {
                StateManager.RestartServer = false;
                mWebApi = new MWebApi();
                StateManager.Init(mWebApi, true, false);

                if (OperatingSystem.IsWindows() && !StateManager.LaunchArgs.ContainsKey("headless"))
                {
                    try
                    {
                        TrayIconManager.AddIcon();
                    }
                    catch (Exception)
                    {

                    }
                }

                // Watch for settings changes
                _ = Task.Run(() =>
                {
                    watcher = new FileSystemWatcher();
                    watcher.Path = $"{StateManager.melonPath}/Configs/";

                    // Watch for changes in LastAccess and LastWrite times, and
                    // the renaming of files or directories.
                    watcher.NotifyFilter = NotifyFilters.LastWrite
                                         | NotifyFilters.FileName
                                         | NotifyFilters.DirectoryName;

                    // Only watch text files.
                    watcher.Filter = "*.json";

                    FileSystemEventHandler func = (sender, args) =>
                    {
                        if(args.Name == "MelonSettings.json")
                        {
                            // Check if settings have actually changed
                            var temp = Storage.LoadConfigFile<Settings>(args.Name.Replace(".json",""), new[] { "JWTKey" }, out _);
                            if (StateManager.MelonSettings == null || temp == null || 
                                Storage.PropertiesEqual(StateManager.MelonSettings, temp))
                            {
                                return;
                            }
                        }

                        // Restart Server
                        try
                        {
                            StateManager.RestartServer = true;
                            app.StopAsync();
                        }
                        catch (Exception)
                        {

                        }
                    };

                    // Add event handlers.
                    watcher.Changed += func;
                    watcher.Created += func;
                    //watcher.Deleted += func;

                    // Begin watching.
                    watcher.EnableRaisingEvents = true;
                });

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
                        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(10);
                        serverOptions.ConfigureHttpsDefaults(httpsOptions =>
                        {
                            httpsOptions.ServerCertificate = certificate;
                        });
                    });
                }



                if (OperatingSystem.IsWindows() && !StateManager.LaunchArgs.ContainsKey("headless"))
                {
                    Log.Logger = new LoggerConfiguration()
                            .WriteTo.File($"{StateManager.melonPath}/MelonWebLogs.txt")
                            .CreateLogger();
                }
                else
                {
                    Log.Logger = new LoggerConfiguration()
                            .WriteTo.File($"{StateManager.melonPath}/MelonWebLogs.txt")
                            .WriteTo.Console()
                            .CreateLogger();
                }

                builder.Host.UseSerilog();
                builder.Host.UseWindowsService();

                var key = StateManager.JWTKey;
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

                var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
                
                lifetime.ApplicationStopping.Register(() =>
                {
                    QueuesCleaner.CleanerActive = false;
                    watcher.Dispose();
                    TrayIconManager.RemoveIcon();
                });

                app.Run();

                await app.StopAsync();
            }
            TrayIconManager.RemoveIcon();
            return 0;
        }
    }
}
