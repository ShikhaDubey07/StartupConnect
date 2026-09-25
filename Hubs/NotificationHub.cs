using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace StartupConnect.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    // Hub specifically for pushing notifications.
    // SignalR automatically maps connections to the authenticated user's NameIdentifier (userId).
}
