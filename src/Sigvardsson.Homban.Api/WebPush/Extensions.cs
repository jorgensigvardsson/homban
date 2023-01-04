using System;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sigvardsson.Homban.Api.WebPush;

public static class Extensions
{
    public static IApplicationBuilder UsePushSubscriptionStore(this IApplicationBuilder app)
    {
        using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<PushSubscriptionContext>();
            context.Database.EnsureCreated();
        }

        return app;
    }
    
    public static IServiceCollection AddPushSubscriptionStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PushSubscriptionContext>(options => options.UseSqlite(configuration.GetConnectionString("PushNotifications")));

        services.AddTransient<IPushSubscriptionStore, SqlitePushSubscriptionStore>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IPushSubscriptionStoreAccessorProvider, SqlitePushSubscriptionStoreAccessorProvider>();

        return services;
    }

    public static IServiceCollection AddPushNotificationService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions();

        services.AddPushServicePushNotificationService(configuration);

        return services;
    }

    public static IServiceCollection AddPushNotificationsQueue(this IServiceCollection services)
    {
        services.AddSingleton<IPushNotificationsQueue, PushNotificationsQueue>();
        services.AddSingleton<IHostedService, PushNotificationsDequeuer>();

        return services;
    }
 
    public static IServiceCollection AddPushServicePushNotificationService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddMemoryVapidTokenCache();
        services.AddPushServiceClient(options =>
        {
            var pushNotificationServiceConfigurationSection = configuration.GetSection(nameof(PushServiceClient));
        
            options.Subject = pushNotificationServiceConfigurationSection.GetValue<string>(nameof(options.Subject));
            options.PublicKey = pushNotificationServiceConfigurationSection.GetValue<string>(nameof(options.PublicKey));
            options.PrivateKey = pushNotificationServiceConfigurationSection.GetValue<string>(nameof(options.PrivateKey));
        });
        
        
        services.AddTransient<IPushNotificationService, PushServicePushNotificationService>();

        return services;
    }
    
    private class VapidAuthenticationProvider : IDisposable
    {
        public VapidAuthentication VapidAuthentication { get; }

        public VapidAuthenticationProvider(VapidAuthentication vapidAuthentication)
        {
            VapidAuthentication = vapidAuthentication;
        }

        public void Dispose()
        {
            VapidAuthentication?.Dispose();
        }
    }
}