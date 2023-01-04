using Lib.Net.Http.WebPush;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigvardsson.Homban.Api.WebPush;
using System.Threading.Tasks;

namespace Sigvardsson.Homban.Api.Controllers;

[ApiController]
[Route("/api/push-notifications")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PushNotificationsController : ControllerBase
{
    private readonly IPushSubscriptionStore m_subscriptionStore;
    private readonly IPushNotificationService m_notificationService;

    public PushNotificationsController(IPushSubscriptionStore subscriptionStore,
                                       IPushNotificationService notificationService)
    {
        m_subscriptionStore = subscriptionStore;
        m_notificationService = notificationService;
    }

    // GET push-notifications-api/public-key
    [HttpGet("public-key")]
    public ContentResult GetPublicKey()
    {
        return Content(m_notificationService.PublicKey, "text/plain");
    }

    // POST push-notifications-api/subscriptions
    [HttpPost("subscriptions")]
    public async Task<IActionResult> StoreSubscription([FromBody]PushSubscription subscription)
    {
        await m_subscriptionStore.StoreSubscriptionAsync(subscription);

        return NoContent();
    }

    // DELETE push-notifications-api/subscriptions?endpoint={endpoint}
    [HttpDelete("subscriptions")]
    public async Task<IActionResult> DiscardSubscription(string endpoint)
    {
        await m_subscriptionStore.DiscardSubscriptionAsync(endpoint);

        return NoContent();
    }
}