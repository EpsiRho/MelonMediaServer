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
using Microsoft.Extensions.Options;
using Amazon.Util.Internal;
using Pastel;
using Melon.Interface;
namespace MelonWebApi
{
    public static class Program
    {
        //public static bool started = false;
        public static WebApplication app;
        public static MWebApi mWebApi;
        public static FileSystemWatcher watcher;
        public static FileSystemWatcher shutdownWatcher;
        public const string Version = "1.0.027.997";

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

                _ = Task.Run(() =>
                {
                    // Watch for settings changes
                    watcher = new FileSystemWatcher();
                    watcher.Path = $"{StateManager.melonPath}/Configs/";

                    watcher.NotifyFilter = NotifyFilters.LastWrite
                                         | NotifyFilters.FileName
                                         | NotifyFilters.DirectoryName;

                    watcher.Filter = "*.json";

                    FileSystemEventHandler func = (sender, args) =>
                    {
                        if(args.Name == "MelonSettings.json")
                        {
                            // Check if settings have actually changed
                            var temp = Storage.LoadConfigFile<Settings>("MelonSettings", null, out _);
                            if (StateManager.MelonSettings == null || temp == null || 
                                Storage.PropertiesEqual(StateManager.MelonSettings, temp))
                            {
                                return;
                            }
                            StateManager.MelonSettings = temp;
                        }
                        else if (args.Name == "SSLConfig.json")
                        {
                            try
                            {
                                StateManager.RestartServer = true;
                                app.StopAsync();
                            }
                            catch (Exception)
                            {

                            }
                        }
                        else if (args.Name == "DisabledPlugins.json")
                        {
                            try
                            {
                                foreach (var plugin in StateManager.Plugins)
                                {
                                    plugin.Destroy();
                                }
                                PluginsManager.LoadPlugins();
                                PluginsManager.ExecutePlugins();
                            }
                            catch (Exception)
                            {

                            }
                        }
                        else if (args.Name == "restartServer.json")
                        {
                            try
                            {
                                System.IO.File.Delete(args.FullPath);
                                StateManager.RestartServer = true;
                                app.StopAsync();
                            }
                            catch (Exception)
                            {
                            }
                        }
                    };

                    // Add event handlers.
                    watcher.Changed += func;
                    watcher.Created += func;

                    watcher.EnableRaisingEvents = true;

                    // And again for so I can detect a shutdown rq
                    shutdownWatcher = new FileSystemWatcher();
                    shutdownWatcher.Path = $"{AppDomain.CurrentDomain.BaseDirectory}";

                    shutdownWatcher.NotifyFilter = NotifyFilters.LastWrite
                                         | NotifyFilters.FileName
                                         | NotifyFilters.DirectoryName;

                    shutdownWatcher.Filter = "*.sdrq";

                    FileSystemEventHandler updateFunc = (sender, args) =>
                    {
                        if (args.Name == "GoAway.sdrq")
                        {
                            StateManager.RestartServer = false;
                            app.StopAsync();
                            File.Delete($"{AppDomain.CurrentDomain.BaseDirectory}/GoAway.sdrq");
                            Environment.Exit(0);
                        }
                    };

                    shutdownWatcher.Changed += updateFunc;
                    shutdownWatcher.Created += updateFunc;

                    shutdownWatcher.EnableRaisingEvents = true;
                });

                var builder = WebApplication.CreateBuilder();

                builder.Services.AddControllers().AddNewtonsoftJson();



                builder.Logging.ClearProviders();

                builder.WebHost.UseUrls(StateManager.MelonSettings.ListeningURL);

                // Load SSL Certificate
                var sslConfig = Security.GetSSLConfig();

                // Verify SSL Cert
                if(!String.IsNullOrEmpty(sslConfig.PathToCert) && !String.IsNullOrEmpty(sslConfig.Password)) // No Cert set, skip and used http
                {
                    var res = Security.VerifySSLConfig(sslConfig);

                    if (res == "Expired") // Expired, notify
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            TrayIconManager.ShowMessageBox("The SSL Certificate is expired, please generate a new SSL Certificate.");
                        }
                        Console.WriteLine("The SSL Certificate is expired, please generate a new SSL Certificate.");
                    }
                    else if (res == "Invalid") // Path or Password is wrong, notify
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            TrayIconManager.ShowMessageBox("The SSL Certificate is invalid, please link a new SSL Certificate.");
                        }
                        Console.WriteLine("The SSL Certificate is invalid, please generate a new SSL Certificate.");
                    }

                    if (res != "Invalid")
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
                            x.Events = new JwtBearerEvents
                            {
                                OnMessageReceived = context =>
                                {
                                    // First, check if the token is present in the Authorization header
                                    var authHeader = context.Request.Headers["Authorization"].ToString();
                                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                                    {
                                        context.Token = authHeader.Substring("Bearer ".Length).Trim();
                                    }
                                    else
                                    {
                                        // If not found, check if the token is present in the query string
                                        var token = context.Request.Query["jwt"];
                                        if (!string.IsNullOrEmpty(token))
                                        {
                                            context.Token = token;
                                        }
                                    }

                                    return Task.CompletedTask;
                                }
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

                //app.UseMiddleware<JwtMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseHttpsRedirection();
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
                watcher.Dispose();
            }
            TrayIconManager.RemoveIcon();
            return 0;
        }
    }
}
