using IT.WebServices.Authorization.Discord.Decorators;
using IT.WebServices.Authorization.Discord.Handlers;
using IT.WebServices.Authorization.Discord.Models.Interactions;
using System.Reflection;

namespace IT.WebServices.Authorization.Discord.Helpers
{
    public class DiscordCommandRouter
    {
        private readonly Dictionary<string, Type> _handlers;
        private readonly IServiceProvider _serviceProvider;

        public DiscordCommandRouter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            _handlers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.GetCustomAttribute<SlashCommandAttribute>() != null
                                && t.IsAssignableTo(typeof(ISlashCommandHandler)))
                .ToDictionary(t => t.GetCustomAttribute<SlashCommandAttribute>()!.Name);
        }

        public async ValueTask<InteractionResponse> RouteAsync(DiscordInteraction interaction)
        {
            var name = interaction.Data?.Name;

            if (name == null || !_handlers.TryGetValue(name, out var handlerType))
                return UnknownCommandResponse();

            var handler = (ISlashCommandHandler)_serviceProvider.GetRequiredService(handlerType);
            return await handler.HandleAsync(interaction);
        }

        private static InteractionResponse UnknownCommandResponse() => new()
        {
            Type = 4,
            Data = new InteractionCallbackData { Content = "Unknown command.", Flags = 64 }
        };
    }
}
