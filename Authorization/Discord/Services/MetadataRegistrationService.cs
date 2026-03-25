using IT.WebServices.Authorization.Discord.Decorators;
using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Authorization.Discord.Models.Commands;
using IT.WebServices.Authorization.Discord.Models.LinkedRoles;
using System.Reflection;

namespace IT.WebServices.Authorization.Discord.Services
{
    public class MetadataRegistrationService : IHostedService
    {
        private readonly DiscordRestClient _client;
        private readonly DiscordSettings _settings;
        private readonly ILogger<MetadataRegistrationService> _logger;

        public MetadataRegistrationService(DiscordSettings settings, DiscordRestClient client, ILogger<MetadataRegistrationService> logger)
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

            _logger.LogInformation("Registering Linked Role Metadata...");
            await _client.RegisterRoleMetadataAsync(BuildRoleMetadataSchema());
            _logger.LogInformation("Registered Linked Role Metadata");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static IEnumerable<RoleMetadataSchema> BuildRoleMetadataSchema()
        {
            return new[]
            {
                new RoleMetadataSchema
                {
                    Type = RoleConnectionMetadataType.BOOLEAN_EQUAL,
                    Key = "is_subscriber",
                    Name = "Subscriber",
                    Description = "Is an active subscriber"
                },
                new RoleMetadataSchema
                {
                    Type = RoleConnectionMetadataType.INTEGER_GREATER_THAN_OR_EQUAL,
                    Key = "sub_level",
                    Name = "Subscription Level",
                    Description = "Amount Subbed To For Role (Cents)"
                },
                new RoleMetadataSchema
                {
                    Type = RoleConnectionMetadataType.DATETIME_LESS_THAN_OR_EQUAL,
                    Key = "member_since",
                    Name = "Member Since",
                    Description = "Date the account was created"
                }
            };
        }

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
