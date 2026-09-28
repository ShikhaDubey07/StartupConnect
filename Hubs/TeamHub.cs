using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace StartupConnect.Hubs;

/// <summary>
/// Real-time channel for team workspaces (chat messages, member/milestone updates).
/// Clients never choose who receives what: <see cref="Services.TeamService"/> resolves the team's
/// CURRENT members from the database on every send and targets them with Clients.Users(...), so a
/// removed member stops receiving events immediately (no stale group membership to clean up).
/// Messages are sent through the antiforgery-protected POST /Workspace/SendMessage endpoint.
/// </summary>
[Authorize]
public class TeamHub : Hub
{
    public const string Path = "/hubs/team";
    public const string MessageEvent = "teamMessage";
    public const string TeamChangedEvent = "teamChanged";
}
