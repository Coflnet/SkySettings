using System;
using System.IO;
using System.Reflection;
using Coflnet.Sky.Settings.Models;
using Coflnet.Sky.Settings.Services;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Prometheus;

namespace Coflnet.Sky.Settings
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SkySettings", Version = "v1" });
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            // Replace with your server version and type.
            // Use 'MariaDbServerVersion' for MariaDB.
            // Alternatively, use 'ServerVersion.AutoDetect(connectionString)'.
            // For common usages, see pull request #1233.
            var serverVersion = new MariaDbServerVersion(new Version(Configuration["MARIADB_VERSION"]));

            // Replace 'YourDbContext' with the name of your own DbContext derived class.
            services.AddDbContext<SettingsDbContext>(
                dbContextOptions => dbContextOptions
                    .UseMySql(Configuration["DB_CONNECTION"], serverVersion)
                    .EnableSensitiveDataLogging() // <-- These two calls are optional but help
                    .EnableDetailedErrors()       // <-- with debugging (remove for production).
            );
            services.AddHostedService<SettingsBackgroundService>();
            services.AddJaeger();
            services.AddTransient<SettingsService>();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = Configuration["REDIS_HOST"];
                options.InstanceName = "SkySettings";
            });
            services.AddSingleton<StackExchange.Redis.ConnectionMultiplexer>((config) =>
            {
                return StackExchange.Redis.ConnectionMultiplexer.Connect(Configuration["REDIS_HOST"]);
            });
            services.AddResponseCaching();
            services.AddMemoryCache();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkySettings v1");
                c.RoutePrefix = "api";
            });

            app.UseRouting();

            app.UseAuthorization();
            app.UseResponseCaching();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMetrics();
                endpoints.MapControllers();
            });
        }
    }
}
