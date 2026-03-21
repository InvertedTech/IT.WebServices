using IT.WebServices.Authorization.Discord.Models.Commands;

namespace IT.WebServices.Authorization.Discord.Decorators
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SlashCommandOptionAttribute(string name, string description, OptionType type, bool required = false) : Attribute
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public OptionType Type { get; } = type;
        public bool Required { get; } = required;
    }
}
