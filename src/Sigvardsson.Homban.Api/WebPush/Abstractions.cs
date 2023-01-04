using System;
using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;

namespace Sigvardsson.Homban.Api.WebPush;

public interface IPushNotificationService
{
    string PublicKey { get; }
    Task SendNotificationAsync(PushSubscription subscription, PushMessage message);
    Task SendNotificationAsync(PushSubscription subscription, PushMessage message, CancellationToken cancellationToken);
}

public interface IPushNotificationsQueue
{
    void Enqueue(PushMessage message);
    Task<PushMessage?> DequeueAsync(CancellationToken cancellationToken);
}

public interface IPushSubscriptionStore
{
    Task StoreSubscriptionAsync(PushSubscription subscription);
    Task DiscardSubscriptionAsync(string endpoint);
    Task ForEachSubscriptionAsync(Action<PushSubscription> action);
    Task ForEachSubscriptionAsync(Action<PushSubscription> action, CancellationToken cancellationToken);
}

public interface IPushSubscriptionStoreAccessor : IDisposable
{
    IPushSubscriptionStore PushSubscriptionStore { get; }
}

public interface IPushSubscriptionStoreAccessorProvider
{
    IPushSubscriptionStoreAccessor GetPushSubscriptionStoreAccessor();
}