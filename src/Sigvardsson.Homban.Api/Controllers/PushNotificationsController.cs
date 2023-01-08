using System;
using Lib.Net.Http.WebPush;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigvardsson.Homban.Api.WebPush;
using System.Threading.Tasks;
using System.Web;

namespace Sigvardsson.Homban.Api.Controllers;

[ApiController]
[Route("/api/push-notifications")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PushNotificationsController : ControllerBase
{
    private readonly IPushSubscriptionStore m_subscriptionStore;
    private readonly IPushNotificationService m_notificationService;
    private readonly IPushNotificationsQueue m_pushNotificationsQueue;

    public PushNotificationsController(IPushSubscriptionStore subscriptionStore,
                                       IPushNotificationService notificationService,
                                       IPushNotificationsQueue pushNotificationsQueue)
    {
        m_subscriptionStore = subscriptionStore ?? throw new ArgumentNullException(nameof(subscriptionStore));
        m_notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        m_pushNotificationsQueue = pushNotificationsQueue ?? throw new ArgumentNullException(nameof(pushNotificationsQueue));
    }

    // GET push-notifications/public-key
    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
    {
        return Ok(m_notificationService.PublicKey);
    }

    // POST push-notifications/subscriptions
    [HttpPost("subscriptions")]
    public async Task<IActionResult> StoreSubscription([FromBody]PushSubscription subscription)
    {
        var hasSubscription = await m_subscriptionStore.HasStoreSubscriptionAsync(subscription);
        if (hasSubscription)
            return Ok("Already subscribed");
        
        await m_subscriptionStore.StoreSubscriptionAsync(subscription);

        return NoContent();
    }

    // DELETE push-notifications/subscriptions/{endpoint}
    [HttpDelete("subscriptions/{endpoint}")]
    public async Task<IActionResult> DiscardSubscription(string endpoint)
    {
        await m_subscriptionStore.DiscardSubscriptionAsync(HttpUtility.UrlDecode(endpoint));

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost]
    public IActionResult Push([FromBody] string message)
    {
        m_pushNotificationsQueue.Enqueue(new PushMessage(message));
        return Ok();
    }
}