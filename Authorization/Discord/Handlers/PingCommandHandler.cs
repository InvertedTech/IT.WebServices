using IT.WebServices.Authorization.Discord.Decorators;
using IT.WebServices.Authorization.Discord.Models.Interactions;

namespace IT.WebServices.Authorization.Discord.Handlers
{
    [SlashCommand("ping", "sends a pong response")]
    public class PingCommandhandler : ISlashCommandHandler
    {
        public ValueTask<InteractionResponse> HandleAsync(DiscordInteraction interaction)
        {
            return ValueTask.FromResult(new InteractionResponse
            {
                Type = 4,
                Data = new InteractionCallbackData { Content = "Pong!" }
            });
        }
    }
}
