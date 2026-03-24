using IT.WebServices.Fragments.Authorization.Discord;

namespace IT.WebServices.Authorization.Discord.Data
{
    public interface IDiscordTicketDataProvider
    {
        Task<bool> Create(DiscordTicketRecord record);
        Task<bool> Delete(Guid ticketId);
        IAsyncEnumerable<DiscordTicketRecord> GetAll();
        Task<DiscordTicketRecord> GetById(Guid ticketId);
        Task Save(DiscordTicketRecord record);
    }
}
