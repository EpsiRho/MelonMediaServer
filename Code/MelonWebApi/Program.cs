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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace MelonWebApi
{
    public static class Program
    {
        private static readonly HttpClient UpdateDownloads = new();
        public static MWebApi mWebApi;
        public const string Version = "0.9.0.1";
        public const string Branch = "Beta";
        public const string AuthRateLimit = "auth", AnonymousArtRateLimit = "anonymous-art";
        public const long MaxRequestBodyBytes = 1024 * 1024;

        public static async Task<int> Main(string[] args)
        {
            // Response models are projections of the stored documents; tolerate fields they don't declare
            // instead of failing the whole request (e.g. Collection.StaticMembership broke collections/search).
            MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register("MelonIgnoreExtraElements",
                new MongoDB.Bson.Serialization.Conventions.ConventionPack { new MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention(true) }, _ => true);
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
                // Secrets in request URLs (?jwt= media tokens, old ?password= logins) are masked before any sink sees them.
                // The file rolls at the configured size and only the newest files are kept.
                var logLimits = StateManager.MelonSettings;
                Log.Logger = new LoggerConfiguration().Enrich.With<LogRedaction>()
                    .WriteTo.File(Path.Combine(StateManager.melonPath, "MelonWebLogs.txt"),
                        fileSizeLimitBytes: Math.Clamp(logLimits?.LogFileMegabytes ?? 100, 1, 1024) * 1024L * 1024,
                        rollOnFileSizeLimit: true, retainedFileCountLimit: Math.Clamp(logLimits?.LogFilesKept ?? 5, 1, 100))
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

                // The models are plain DTOs without nullable annotations: a field a client leaves null (e.g. a track's Chapters
                // on files scanned before chapters existed) must not fail validation as "required".
                builder.Services.AddControllers(o =>
                {
                    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                    o.Filters.Add<PagingLimits>();
                }).AddNewtonsoftJson();

                // Clients behind a local reverse proxy or tunnel arrive from loopback; take their address from
                // X-Forwarded-For (trusted from loopback only, the framework default) so rate limits apply per client.
                builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor);

                builder.Services.AddRateLimiter(o =>
                {
                    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    // Sign-in, invite and reset endpoints: per client address.
                    o.AddPolicy(AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                    // Artwork without a token (resizing costs CPU): per client address; signed-in callers aren't limited.
                    o.AddPolicy(AnonymousArtRateLimit, context => context.User.Identity?.IsAuthenticated == true
                        ? RateLimitPartition.GetNoLimiter("signed-in")
                        : RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                });

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

                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(x =>
                        {
                            x.RequireHttpsMetadata = false; // Set to true in production
                            x.SaveToken = true;
                            x.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuerSigningKey = true,
                                // Resolved per request: "sign out every device" swaps the key while the server runs.
                                IssuerSigningKeyResolver = (_, _, _, _) => [new SymmetricSecurityKey(StateManager.JWTKey)],
                                ClockSkew = TimeSpan.FromMinutes(1),
                                ValidateIssuer = false,
                                ValidateAudience = false
                            };
                            x.Events = new JwtBearerEvents
                            {
                                OnTokenValidated = async context =>
                                {
                                    var principal = context.Principal!;
                                    var id = principal.FindFirstValue(ClaimTypes.UserData);
                                    var failure = TokenRules.Check(context.HttpContext, principal);
                                    if (failure != null) { context.Fail(failure); return; }
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
                                            context.HttpContext.Items[TokenRules.FromQueryKey] = true;
                                        }
                                    }

                                    return Task.CompletedTask;
                                }
                            };
                        });
                builder.Services.Configure<KestrelServerOptions>(options =>
                {
                    options.AllowSynchronousIO = false;
                    // Requests are small JSON or query strings; endpoints that take uploads raise this with [RequestSizeLimit].
                    options.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
                });

                await using var app = builder.Build();

                app.UseForwardedHeaders();
                app.UseAuthentication();
                // Security events recorded during this request name the caller and their address.
                app.Use((context, next) =>
                {
                    SecurityLog.SetOrigin(context.User.FindFirstValue(ClaimTypes.UserData), context.Connection.RemoteIpAddress?.ToString());
                    return next(context);
                });
                app.UseAuthorization();
                app.UseRateLimiter();
                app.UseHttpsRedirection();
                app.UseMiddleware<PluginMiddleware>();

                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromMinutes(2)
                };

                app.UseWebSockets(webSocketOptions);

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
