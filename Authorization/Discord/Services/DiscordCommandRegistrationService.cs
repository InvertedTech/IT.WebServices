using IT.WebServices.Authorization.Discord.Decorators;
using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Models.Commands;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace IT.WebServices.Authorization.Discord.Services
{
    public class DiscordCommandRegistrationService : IHostedService
    {
        private readonly DiscordRestClient _client;
        private readonly DiscordBotSettings _settings;
        private readonly ILogger<DiscordCommandRegistrationService> _logger;

        public DiscordCommandRegistrationService(IOptions<DiscordBotSettings> options, DiscordRestClient client, ILogger<DiscordCommandRegistrationService> logger) 
        {
            _settings = options.Value;
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
