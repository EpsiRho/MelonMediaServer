using Melon.Classes;
using Melon.LocalClasses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Collections.Specialized;
using System.Text;
namespace MelonWebApi
{
    public static class Program
    {
        public static bool started = false;
        public static WebApplication app;
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            builder.Logging.ClearProviders();
            Log.Logger = new LoggerConfiguration()
                        .WriteTo.File(StateManager.melonPath)
                        .CreateLogger();

            builder.Host.UseSerilog();

            app = builder.Build();

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.RunAsync();

            if (!started)
            {
                started = true;
                Console.ForegroundColor = ConsoleColor.White;
                Console.OutputEncoding = Encoding.UTF8;

                // Melon Startup
                StateManager.Init();

                // UI Startup
                DisplayManager.DisplayHome();
            }
        }
    }
}
