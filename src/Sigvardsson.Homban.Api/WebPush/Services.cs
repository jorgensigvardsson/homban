using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lib.Net.Http.WebPush;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sigvardsson.Homban.Api.WebPush;

public class PushNotificationsDequeuer : BackgroundService
{
    private readonly IPushSubscriptionStoreAccessorProvider m_subscriptionStoreAccessorProvider;
    private readonly IPushNotificationsQueue m_messagesQueue;
    private readonly IPushNotificationService m_notificationService;

    public PushNotificationsDequeuer(IPushNotificationsQueue messagesQueue,
                                     IPushSubscriptionStoreAccessorProvider subscriptionStoreAccessorProvider,
                                     IPushNotificationService notificationService)
    {
        m_subscriptionStoreAccessorProvider = subscriptionStoreAccessorProvider;
        m_messagesQueue = messagesQueue;
        m_notificationService = notificationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await m_messagesQueue.DequeueAsync(stoppingToken);

            if (message != null && !stoppingToken.IsCancellationRequested)
            {
                using (var subscriptionStoreAccessor = m_subscriptionStoreAccessorProvider.GetPushSubscriptionStoreAccessor())
                {
                    await subscriptionStoreAccessor.PushSubscriptionStore.ForEachSubscriptionAsync(subscription =>
                    {
                        // Fire-and-forget 
                        m_notificationService.SendNotificationAsync(subscription, message, stoppingToken);
                    }, stoppingToken);
                }
            }
        }
    }
}

public class PushNotificationsQueue : IPushNotificationsQueue
{
    private readonly ConcurrentQueue<PushMessage> m_messages = new();
    private readonly SemaphoreSlim m_messageEnqueuedSignal = new(0);

    public void Enqueue(PushMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        m_messages.Enqueue(message);
        m_messageEnqueuedSignal.Release();
    }

    public async Task<PushMessage?> DequeueAsync(CancellationToken cancellationToken)
    {
        await m_messageEnqueuedSignal.WaitAsync(cancellationToken);

        m_messages.TryDequeue(out var message);

        return message;
    }
}

public class PushServicePushNotificationService : IPushNotificationService
{
    private readonly PushServiceClient m_pushClient;
    private readonly IPushSubscriptionStoreAccessorProvider m_subscriptionStoreAccessorProvider;
    private readonly ILogger m_logger;

    public string PublicKey => m_pushClient.DefaultAuthentication.PublicKey;

    public PushServicePushNotificationService(PushServiceClient pushClient,
                                              IPushSubscriptionStoreAccessorProvider subscriptionStoreAccessorProvider,
                                              ILogger<PushServicePushNotificationService> logger)
    {
        m_pushClient = pushClient ?? throw new ArgumentNullException(nameof(pushClient));
        m_subscriptionStoreAccessorProvider = subscriptionStoreAccessorProvider ?? throw new ArgumentNullException(nameof(subscriptionStoreAccessorProvider));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendNotificationAsync(PushSubscription subscription, PushMessage message)
    {
        return SendNotificationAsync(subscription, message, CancellationToken.None);
    }

    public async Task SendNotificationAsync(PushSubscription subscription, PushMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await m_pushClient.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
        }
        catch (Exception ex)
        {
            await HandlePushMessageDeliveryExceptionAsync(ex, subscription);
        }
    }

    private async Task HandlePushMessageDeliveryExceptionAsync(Exception exception, PushSubscription subscription)
    {
        if (exception is not PushServiceClientException pushServiceClientException)
        {
            m_logger.LogError(exception, "Failed requesting push message delivery to {Endpoint}.", subscription.Endpoint);
        }
        else
        {
            if (pushServiceClientException.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                using (var subscriptionStoreAccessor = m_subscriptionStoreAccessorProvider.GetPushSubscriptionStoreAccessor())
                {
                    await subscriptionStoreAccessor.PushSubscriptionStore.DiscardSubscriptionAsync(subscription.Endpoint);
                }

                m_logger.LogInformation("Subscription has expired or is no longer valid and has been removed.");
            }
        }
    }
}