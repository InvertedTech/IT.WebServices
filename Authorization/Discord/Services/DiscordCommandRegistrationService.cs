using IT.WebServices.Authorization.Discord.Decorators;
using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Models.Commands;
using System.Reflection;

namespace IT.WebServices.Authorization.Discord.Services
{
    public class DiscordCommandRegistrationService : IHostedService
    {
        private readonly DiscordRestClient _client;
        private readonly DiscordSettings _settings;
        private readonly ILogger<DiscordCommandRegistrationService> _logger;

        public DiscordCommandRegistrationService(DiscordSettings settings, DiscordRestClient client, ILogger<DiscordCommandRegistrationService> logger)
        {
            _settings = settings;
            _client = client;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Loading Application Commands...");

            var commands = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => t.GetCustomAttribute<SlashCommandAttribute>() != null)
                    .Select(t => BuildApplicationCommand(t))
                    .ToList();
            var commandsCount = commands.Count;

            _logger.LogInformation($"{commandsCount} Commands Found....");
            _logger.LogInformation($"Registering {commandsCount} Commands...");

            if (string.IsNullOrEmpty(_settings.GuildId))
            {
                _logger.LogWarning("GuildId is not configured — skipping command registration.");
                return;
            }

            await _client.RegisterCommandsAsync(_settings.GuildId, commands);

            _logger.LogInformation("Registered Commands");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static DiscordApplicationCommand BuildApplicationCommand(Type handlerType)
        {
            var cmd = handlerType.GetCustomAttribute<SlashCommandAttribute>()!;
            var options = handlerType.GetCustomAttributes<SlashCommandOptionAttribute>().ToList();

            return new DiscordApplicationCommand
            {
                Name = cmd.Name,
                Description = cmd.Description,
                DefaultMemberPermissions = cmd.AdminOnly ? "8" : null,
                Options = options.Select(o => new DiscordCommandOption
                {
                    Name = o.Name,
                    Description = o.Description,
                    Type = (int)o.Type,
                    Required = o.Required
                }).ToList()
            };
        }
    }
}
