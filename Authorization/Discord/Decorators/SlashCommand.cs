using static Grpc.Core.ChannelOption;

namespace IT.WebServices.Authorization.Discord.Decorators
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SlashCommandAttribute(string name, string description, bool adminOnly = false) : Attribute
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public bool AdminOnly { get; } = adminOnly;
    }
}
