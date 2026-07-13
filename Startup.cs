using System;
using System.IO;
using System.Reflection;
using Cassandra;
using Coflnet.Core;
using Coflnet.Sky.Settings.Models;
using Coflnet.Sky.Settings.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
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

            services.AddCoflnetCore();
            services.AddHostedService<SettingsBackgroundService>();
            services.AddSingleton<ISettingsService, StorageService>();
            if (Configuration["MIGRATOR"]?.Equals("true") ?? false)
            {
                services.AddHostedService<MigrationService>();
            }
            /* services.AddStackExchangeRedisCache(options =>
             {
                 options.Configuration = Configuration["REDIS_HOST"];
                 options.InstanceName = "SkySettings";
             });*/
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
            app.UseCoflnetCore();
            /* app.UseSwagger();
             app.UseSwaggerUI(c =>
             {
                 c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkySettings v1");
                 c.RoutePrefix = "api";
             });*/

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
