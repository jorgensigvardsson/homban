using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Sigvardsson.Homban.Api.Controllers;
using Sigvardsson.Homban.Api.Services;

namespace Sigvardsson.Homban.Api;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var apiJsonSettings = new ApiJsonSettings();
        var utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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

        var app = builder.Build();
        
        app.UseCors(b =>
        {
            b.AllowAnyOrigin()
             .AllowAnyMethod()
             .AllowAnyHeader();
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();
        app.UseAuthentication();
        app.MapControllers();
        app.UseWebSockets();
        await app.RunAsync();

        return 0;
    }

    private static byte[] Hash(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }
}