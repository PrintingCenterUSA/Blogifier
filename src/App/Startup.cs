﻿using Askmethat.Aspnet.JsonLocalizer.Extensions;
using Core;
using Core.Data;
using Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Events;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace App
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;

            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.RollingFile("Logs/{Date}.txt", LogEventLevel.Warning)
              .CreateLogger();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var section = Configuration.GetSection("Blogifier");

            services.AddAppSettings<AppItem>(section);

            if (section.GetValue<string>("DbProvider") == "SqlServer")
            {
                AppSettings.DbOptions = options => options.UseSqlServer(section.GetValue<string>("ConnString"));
            }
            else if (section.GetValue<string>("DbProvider") == "MySql")
            {
                AppSettings.DbOptions = options => options.UseMySql(section.GetValue<string>("ConnString"));
            }
            else if (section.GetValue<string>("DbProvider") == "Postgres")
            {
                AppSettings.DbOptions = options => options.UseNpgsql(section.GetValue<string>("ConnString"));
            }
            else
            {
                AppSettings.DbOptions = options => options.UseSqlite(section.GetValue<string>("ConnString"));
            }
            
            services.AddDbContext<AppDbContext>(AppSettings.DbOptions, ServiceLifetime.Transient);

            services.AddIdentity<AppUser, IdentityRole>(options => {
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 4;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.User.AllowedUserNameCharacters = null;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            services.AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true));

            services.AddJsonLocalization();

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo("en-US"),
                    new CultureInfo("es-ES"),
                    new CultureInfo("pt-BR"),
                    new CultureInfo("ru-RU"),
                    new CultureInfo("zh-cn"),
                    new CultureInfo("zh-tw")
                };

                options.DefaultRequestCulture = new RequestCulture(culture: "en-US", uiCulture: "en-US");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });

            services.AddRouting(options => options.LowercaseUrls = true);

            services.AddMvc()
            .AddViewLocalization()
            .ConfigureApplicationPartManager(p =>
            {
                foreach (var assembly in AppConfig.GetAssemblies())
                {
                    p.ApplicationParts.Add(new AssemblyPart(assembly));
                }
            })
            .AddRazorPagesOptions(options =>
            {
                options.Conventions.AuthorizeFolder("/Admin");
            })
            .AddApplicationPart(typeof(Core.Api.AuthorsController).GetTypeInfo().Assembly).AddControllersAsServices()
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            if (Environment.IsDevelopment())
            {
                services.AddSwaggerGen(setupAction => {
                    setupAction.SwaggerDoc("spec",
                    new Microsoft.OpenApi.Models.OpenApiInfo()
                    {
                        Title = "Blogifier API",
                        Version = "1"
                    });
                    setupAction.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CoreAPI.xml"));
                });
            }
            
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "wwwroot/themes/_active";
            });
            services.AddCors(c =>
            {
                c.AddPolicy("AllowOrigin", options => options
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                );
            });

            services.AddAppServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseSwagger();
                app.UseSwaggerUI(setupAction =>
                {
                    setupAction.SwaggerEndpoint(
                        "/swagger/spec/swagger.json",
                        "Blogifier API"
                    );
                });
            }

            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    const int durationInSeconds = 360*60 * 60 * 24;
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                        "public,max-age=" + durationInSeconds;
                }
            });
            app.UseSpaStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    const int durationInSeconds = 360 * 60 * 60 * 24;
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                        "public,max-age=" + durationInSeconds;
                }
            });
            app.UseRequestLocalization();        

            AppSettings.WebRootPath = Environment.WebRootPath;
            AppSettings.ContentRootPath = Environment.ContentRootPath;

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Blog}/{action=Index}/{id?}");
            });

            app.UseSpa(spa => { });
            app.UseCors(options => options.AllowAnyOrigin());
        }
    }
}