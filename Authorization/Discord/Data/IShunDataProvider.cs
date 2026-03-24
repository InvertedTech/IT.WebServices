using IT.WebServices.Fragments.Authorization.Discord;

namespace IT.WebServices.Authorization.Discord.Data
{
    public interface IShunDataProvider
    {
        Task<bool> Create(DiscordShunRecord record);
        IAsyncEnumerable<DiscordShunRecord> GetAll();
        Task<DiscordShunRecord> GetById(Guid id);
        Task Save(DiscordShunRecord record);
    }
}
