using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Sinks.SystemConsole.Themes;
using Sigvardsson.Homban.Api.Controllers;
using Sigvardsson.Homban.Api.Hubs;
using Sigvardsson.Homban.Api.Services;

namespace Sigvardsson.Homban.Api;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var apiJsonSettings = new ApiJsonSettings();
        var utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        builder.Services.AddSignalR(options => options.EnableDetailedErrors = true);
        builder.Services.AddSingleton<IBoardService, BoardService>();
        builder.Services.AddSingleton<IBackingStoreService, BackingStoreService>();
        builder.Services.AddSingleton<IConfigurableJsonSerializer<ApiJsonSettings>>(new ConfigurableJsonSerializer<ApiJsonSettings>(apiJsonSettings, utf8Encoding));
        builder.Services.AddSingleton<IConfigurableJsonSerializer<StorageJsonSettings>>(new ConfigurableJsonSerializer<StorageJsonSettings>(new StorageJsonSettings(), utf8Encoding));
        builder.Services.AddSingleton<IHostedService, BoardScheduler>();
        builder.Services.AddSingleton<IInactiveTaskScheduler, InactiveTaskScheduler>();
        builder.Services.AddSingleton<IDtoMapper, DtoMapper>();
        builder.Services.AddSingleton<IThreadControl, ThreadControl>();
        builder.Services.AddSingleton<IClock, Clock>();
        builder.Services.AddSingleton<IGuidGenerator, GuidGenerator>();
        builder.Services.AddSingleton<IBoardHubService, BoardHubService>();
        builder.Services
               .AddControllers()
               .AddNewtonsoftJson(o =>
                {
                    apiJsonSettings.CopyTo(o);
                });

        var jwtSigningKey = Hash(builder.Configuration["JwtSigningKey"] ?? throw new ApplicationException("No JwtSigningKey configured."));
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Sigvardsson.Homban",
            ValidateAudience = true,
            ValidAudience = "Sigvardsson.Homban",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtSigningKey),
            NameClaimType = ClaimTypes.NameIdentifier
        };
        
        builder.Services
               .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
               .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = tokenValidationParameters;
                });

        builder.Services.AddSingleton(tokenValidationParameters);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.UseOneOfForPolymorphism();
            c.SelectDiscriminatorNameUsing(type => type == typeof(Controllers.Schedule) ? "type" : null);
            c.SelectSubTypesUsing(baseType =>
            {
                if (baseType == typeof(Controllers.Schedule))
                {
                    return new[]
                    {
                        typeof(Controllers.OneTimeSchedule),
                        typeof(Controllers.PeriodicScheduleFollowingActivity),
                        typeof(Controllers.PeriodicScheduleFollowingCalendar)
                    };
                }

                return Array.Empty<Type>();
            });
        });
        
        builder.Host.UseSerilog((ctx, cfg) =>
        {
            cfg.Enrich.WithProperty("Application", ctx.HostingEnvironment.ApplicationName)
               .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
               .WriteTo.Console(LogEventLevel.Information, theme: AnsiConsoleTheme.Code);

            var lokiSink = ctx.Configuration["LokiSink"];
            if (lokiSink != null && Uri.TryCreate(lokiSink, UriKind.Absolute, out _))
            {
                cfg.WriteTo.GrafanaLoki(lokiSink, new[] {new LokiLabel {Key = "Instance", Value = "Api"}}, restrictedToMinimumLevel: LogEventLevel.Information);
            }
        });

        var app = builder.Build();
        
#if DEBUG        
        app.UseCors(b =>
        {
            b.WithOrigins("http://localhost:3000")
             .AllowCredentials()
             .AllowAnyHeader()
             .AllowAnyMethod();
            
            b.AllowAnyMethod()
             .AllowAnyHeader();
        });
#endif

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();
        app.UseAuthentication();
        app.MapControllers();
        app.MapHub<BoardHub>("/board-hub");
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30)});
        app.UseSerilogRequestLogging();

        try
        {
            await app.RunAsync();
            return 0;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static byte[] Hash(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }
}