using System.Reflection;
using System.Security.Claims;
using MongoDB.Driver;
using System.Security.Cryptography.X509Certificates;
using Melon.LocalClasses;
using Melon.Models;
using MelonLib.API;
using MelonWebApi.Middleware;
using MelonWebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

namespace MelonWebApi
{
    public static class Program
    {
        private static readonly HttpClient UpdateDownloads = new();
        public static MWebApi mWebApi;
        public const string Version = "1.0.0.1756";
        public const string Branch = "Beta";

        public static async Task<int> Main(string[] args)
        {
            StateManager.Version = Version;
            StateManager.Branch = Branch;
            StateManager.ParseArgs(args);
            if (StateManager.LaunchArgs.ContainsKey("version"))
            {
                Console.WriteLine(Version);
                return 0;
            }
            var admin = ServerAdministration.Instance;
            admin.PrepareWindowsUpdate = release =>
            {
                var asset = release.assets.FirstOrDefault(a => a.name == MelonInstaller.Classes.WindowsBundleUpdate.AssetName);
                return MelonInstaller.Classes.WindowsBundleUpdate.Prepare(UpdateDownloads, new DirectoryInfo(AppContext.BaseDirectory).Parent!.FullName,
                    asset?.browser_download_url, asset?.digest, StateManager.melonPath, Environment.ProcessId,
                    Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService());
            };
            if (StateManager.LaunchArgs.ContainsKey("help"))
            {
                foreach (var option in admin.GetHelp()) Console.WriteLine($"{option.Key}  {option.Value}");
                return 0;
            }
            if (StateManager.LaunchArgs.ContainsKey("openFolder"))
            {
                Console.WriteLine(StateManager.melonPath);
                return 0;
            }
            if (StateManager.LaunchArgs.TryGetValue("dataPath", out var path))
                StateManager.melonPath = Path.GetFullPath(path);
            Directory.CreateDirectory(StateManager.melonPath);
            using var ownership = new FileStream(Path.Combine(StateManager.melonPath, "server.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            if (StateManager.LaunchArgs.ContainsKey("setup"))
                Console.WriteLine("Open a local controller to configure MongoDB and create the first administrator.");
            bool restart;
            do
            {
                restart = false;
                using var stopping = new CancellationTokenSource();
                void Shutdown(bool again) { restart = again; stopping.Cancel(); }
                void Interrupt(object? sender, ConsoleCancelEventArgs e) { e.Cancel = true; Shutdown(false); }
                mWebApi = new MWebApi();
                StateManager.Initialize(mWebApi);
                admin.BeginRun();
                Log.Logger = new LoggerConfiguration().WriteTo.File(Path.Combine(StateManager.melonPath, "MelonWebLogs.txt"))
                    .WriteTo.Console().CreateLogger();
                admin.ShutdownRequested += Shutdown;
                Console.CancelKeyPress += Interrupt;
                var control = new LocalControllerServer(admin, LocalControllerClient.GetPipeName(StateManager.melonPath)).RunAsync(stopping.Token);
                var cleaner = Task.CompletedTask;
                try
                {
                    if (control.IsFaulted) await control;
                    try { admin.ConnectDatabase(); }
                    catch (Exception ex) { Log.Warning("Database unavailable ({ErrorType}). Local management is available.", ex.GetType().Name); }
                    admin.ReloadPlugins(true);
                    cleaner = QueuesCleaner.RunAsync(stopping.Token);
                    var web = RunWebAsync(stopping.Token);
                    if (await Task.WhenAny(web, control) == control)
                    {
                        stopping.Cancel();
                        await control;
                    }
                    await web;
                }
                finally
                {
                    stopping.Cancel();
                    await control;
                    await cleaner;
                    while (MelonScanner.Scanning) await Task.Delay(100);
                    admin.UnloadPlugins();
                    StateManager.DbClient?.Dispose();
                    Log.CloseAndFlush();
                    admin.ShutdownRequested -= Shutdown;
                    Console.CancelKeyPress -= Interrupt;
                }
            } while (restart);
            return 0;
        }

        private static async Task RunWebAsync(CancellationToken stopping)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ApplicationName = typeof(Program).Assembly.GetName().Name,
                    ContentRootPath = AppContext.BaseDirectory
                });
                builder.WebHost.UseStaticWebAssets();

                builder.Services.AddControllers().AddNewtonsoftJson();

                builder.Logging.ClearProviders();

                builder.WebHost.UseUrls(StateManager.MelonSettings.ListeningURL);

                // Load SSL Certificate
                var sslConfig = Security.GetSSLConfig();

                if (!string.IsNullOrEmpty(sslConfig.PathToCert))
                {
                    if (Security.VerifySSLConfig(sslConfig) != "Valid") throw new InvalidOperationException("Invalid HTTPS certificate.");
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(sslConfig.PathToCert, sslConfig.Password);
                    builder.WebHost.ConfigureKestrel(options => options.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate));
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
                                OnTokenValidated = async context =>
                                {
                                    var principal = context.Principal!;
                                    var id = principal.FindFirstValue(ClaimTypes.UserData);
                                    if (id == "NA" && principal.IsInRole("Server")) return;
                                    var account = await StateManager.DbClient.GetDatabase("Melon").GetCollection<User>("Users")
                                        .Find(u => u._id == id).Project(u => new { u.Password, u.Type, u.Username }).FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                                    if (account == null || account.Username != principal.Identity?.Name || !principal.IsInRole(account.Type) ||
                                        principal.FindFirstValue("melon_session") != Security.SessionStamp(account.Password))
                                        context.Fail("Account credentials have changed.");
                                },
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

                    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement()
                    {
                    {
                        new OpenApiSecuritySchemeReference("Bearer", document),
                        new List<string>()
                    }
                    });
                });

                builder.Services.Configure<KestrelServerOptions>(options =>
                {
                    options.AllowSynchronousIO = true;
                });

                await using var app = builder.Build();

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
                    options.OpenApiVersion = OpenApiSpecVersion.OpenApi2_0;
                });

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                    options.RoutePrefix = string.Empty;
                });

                app.MapControllers();

                app.UseStaticFiles();

                await app.RunAsync(stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            catch (Exception ex) when (!stopping.IsCancellationRequested)
            {
                Log.Error(ex, "Web listener unavailable. Correct settings through local management and restart.");
                try { await Task.Delay(Timeout.Infinite, stopping); }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            }
        }
    }
}
