using IT.WebServices.Authorization.Discord.Models.Interactions;

namespace IT.WebServices.Authorization.Discord.Handlers
{
    public interface ISlashCommandHandler
    {
        ValueTask<InteractionResponse> HandleAsync(DiscordInteraction interaction);
    }
}