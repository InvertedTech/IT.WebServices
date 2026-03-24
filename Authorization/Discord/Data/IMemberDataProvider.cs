using IT.WebServices.Fragments.Authorization.Discord;

namespace IT.WebServices.Authorization.Discord.Data
{
    public interface IMemberDataProvider
    {
        Task<bool> Create(DiscordMemberRecord member);
        Task<bool> Delete(Guid userId);
        Task<DiscordMemberRecord> GetByUserId (Guid userId);
        Task<DiscordMemberRecord> GetByDiscordId(string discordId);
        IAsyncEnumerable<DiscordMemberRecord> GetAll();
        Task<bool> Exists(Guid userId);
        Task Save(DiscordMemberRecord member);
    }
}
