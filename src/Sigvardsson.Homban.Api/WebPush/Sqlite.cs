using System;
using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Sigvardsson.Homban.Api.WebPush;

public class SqlitePushSubscriptionStore : IPushSubscriptionStore
{
    private readonly PushSubscriptionContext m_context;

    public SqlitePushSubscriptionStore(PushSubscriptionContext context)
    {
        m_context = context;
    }

    public Task StoreSubscriptionAsync(PushSubscription subscription)
    {
        m_context.Subscriptions.Add(new PushSubscriptionContext.PushSubscription(subscription));

        return m_context.SaveChangesAsync();
    }

    public async Task DiscardSubscriptionAsync(string endpoint)
    {
        var subscription = await m_context.Subscriptions.FindAsync(endpoint);

        if (subscription == null)
            return;

        m_context.Subscriptions.Remove(subscription);

        await m_context.SaveChangesAsync();
    }

    public Task ForEachSubscriptionAsync(Action<PushSubscription> action)
    {
        return ForEachSubscriptionAsync(action, CancellationToken.None);
    }

    public Task ForEachSubscriptionAsync(Action<PushSubscription> action, CancellationToken cancellationToken)
    {
        return m_context.Subscriptions.AsNoTracking().ForEachAsync(action, cancellationToken);
    }
}

public class PushSubscriptionContext : DbContext
{
    public class PushSubscription : Lib.Net.Http.WebPush.PushSubscription
    {
        // ReSharper disable once InconsistentNaming
        public string P256DH
        {
            get => GetKey(PushEncryptionKeyName.P256DH);
            set => SetKey(PushEncryptionKeyName.P256DH, value);
        }

        public string Auth
        {
            get => GetKey(PushEncryptionKeyName.Auth);
            set => SetKey(PushEncryptionKeyName.Auth, value);
        }

        public PushSubscription()
        { }

        public PushSubscription(Lib.Net.Http.WebPush.PushSubscription subscription)
        {
            Endpoint = subscription.Endpoint;
            Keys = subscription.Keys;
        }
    }

    public DbSet<PushSubscription> Subscriptions { get; set; } = null!;

    public PushSubscriptionContext(DbContextOptions<PushSubscriptionContext> options)
        : base(options)
    { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        EntityTypeBuilder<PushSubscription> pushSubscriptionEntityTypeBuilder = modelBuilder.Entity<PushSubscription>();
        pushSubscriptionEntityTypeBuilder.HasKey(e => e.Endpoint);
        pushSubscriptionEntityTypeBuilder.Ignore(p => p.Keys);
    }
}

public class SqlitePushSubscriptionStoreAccessor : IPushSubscriptionStoreAccessor
{
    private IServiceScope? m_serviceScope;

    public IPushSubscriptionStore PushSubscriptionStore { get; private set; }

    public SqlitePushSubscriptionStoreAccessor(IPushSubscriptionStore pushSubscriptionStore)
    {
        PushSubscriptionStore = pushSubscriptionStore;
    }

    public SqlitePushSubscriptionStoreAccessor(IServiceScope serviceScope)
    {
        m_serviceScope = serviceScope;
        PushSubscriptionStore = m_serviceScope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();
    }

    public void Dispose()
    {
        PushSubscriptionStore = null!;

        if (m_serviceScope is not null)
        {
            m_serviceScope.Dispose();
            m_serviceScope = null;
        }
    }
}

public class SqlitePushSubscriptionStoreAccessorProvider : IPushSubscriptionStoreAccessorProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public SqlitePushSubscriptionStoreAccessorProvider(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    public IPushSubscriptionStoreAccessor GetPushSubscriptionStoreAccessor()
    {
        if (_httpContextAccessor.HttpContext is null)
        {
            return new SqlitePushSubscriptionStoreAccessor(_serviceProvider.CreateScope());
        }
        else
        {
            return new SqlitePushSubscriptionStoreAccessor(_httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IPushSubscriptionStore>());
        }
    }
}