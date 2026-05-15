using Microsoft.AspNetCore.SignalR;

namespace SwiftDeploy.Services
{
    public class DeploymentHub : Hub
    {
        public async Task Subscribe(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }

        public async Task Unsubscribe(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }
    }
}
