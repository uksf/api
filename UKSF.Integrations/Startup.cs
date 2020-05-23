using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using UKSF.Api.Services;
using UKSF.Api.Services.Common;
using UKSF.Integrations.AppStart;
using UKSF.Integrations.AppStart.Services;

namespace UKSF.Integrations {
    public class Startup {
        private readonly IConfiguration configuration;
        private readonly IHostEnvironment currentEnvironment;

        public Startup(IHostEnvironment currentEnvironment, IConfiguration configuration) {
            this.configuration = configuration;
            this.currentEnvironment = currentEnvironment;
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
            builder.Build();
        }

        public void ConfigureServices(IServiceCollection services) {
            services.RegisterServices(configuration, currentEnvironment);

            services.AddCors(
                options => options.AddPolicy(
                    "CorsPolicy",
                    builder => {
                        builder.AllowAnyMethod().AllowAnyHeader().WithOrigins("http://localhost:4200", "http://localhost:5100", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk").AllowCredentials();
                    }
                )
            );
            services.AddAuthentication(options => { options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; }).AddCookie().AddSteam();

            services.AddControllers();
            services.AddSwaggerGen(options => { options.SwaggerDoc("v1", new OpenApiInfo { Title = "UKSF Integrations API", Version = "v1" }); });
            services.AddMvc(options => { options.Filters.Add<ExceptionHandler>(); }).AddNewtonsoftJson();
        }

        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IHostEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }

            app.UseCookiePolicy(new CookiePolicyOptions { MinimumSameSitePolicy = SameSiteMode.Lax });
            app.UseSwagger();
            app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "UKSF Integrations API v1"); });
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHsts();
            app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
            app.UseEndpoints(endpoints => { endpoints.MapControllers().RequireCors("CorsPolicy"); });

            Global.ServiceProvider = app.ApplicationServices;
            ServiceWrapper.ServiceProvider = Global.ServiceProvider;

            StartServices.Start();
        }
    }
}
