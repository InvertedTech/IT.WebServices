using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Fragments.Authorization.Discord;
using IT.WebServices.Helpers;
using MySql.Data.MySqlClient;

namespace IT.WebServices.Authorization.Discord.Data
{
    internal class SqlDiscordTicketDataProvider : IDiscordTicketDataProvider
    {
        public readonly MySQLHelper sql;
        private readonly ILogger log;

        public SqlDiscordTicketDataProvider(MySQLHelper sql, ILogger<SqlDiscordTicketDataProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }
        public async Task<bool> Create(DiscordTicketRecord record)
        {
            try
            {
                await InsertOrUpdate(record);
                return true;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error In SqlDiscordTicketDataProvider.Create");
                return false;
            }
        }

        public async Task<bool> Delete(Guid ticketId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Discord_Ticket
                    WHERE
                        TicketId = @TicketId
                ";

                var parameters = new MySqlParameter[1]
                {
                    new MySqlParameter("TicketId", ticketId.ToString())
                };

                await sql.RunCmd(query, parameters);
                return true;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlDiscordTicketDataProvider.Delete");
                return false;
            }
        }

        public async IAsyncEnumerable<DiscordTicketRecord> GetAll()
        {
            const string query = @"
                SELECT
                    *
                FROM
                    Discord_Ticket
            ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseDiscordTicketRecord();
                yield return record;
            }
        }

        public async Task<DiscordTicketRecord> GetById(Guid ticketId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Discord_Ticket
                    WHERE
                        TicketId = @TicketId
                ";

                var parameters = new MySqlParameter[1]
                {
                    new MySqlParameter("TicketId", ticketId.ToString())
                };

                using var rdr = await sql.ReturnReader(query, parameters);
                
                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseDiscordTicketRecord();
                    return record;
                }

                return null;
            } catch (Exception ex) 
            {
                log.LogError(ex, "Error In SqlDiscordTicketDataProvider.GetById");
                return null;
            }
        }

        public Task Save(DiscordTicketRecord record)
        {
            return InsertOrUpdate(record);
        }

        private async Task InsertOrUpdate(DiscordTicketRecord record)
        {
            try
            {
                const string query = @"
                    INSERT INTO Discord_Ticket
                        (TicketID, UserId, ThreadId, Status, Subject,
                            Text, CreatedOnUTC, ClosedOnUTC, ClosedByDiscordId,
                            MessageIds)
                    VALUES (
                        @TicketId, @UserId, @ThreadId, @Status, @Subject,
                        @Text, @CreatedOnUTC, @ClosedOnUTC,
                        @ClosedByDiscordId, MessageIds
                    )
                    ON DUPLICATE KEY UPDATE
                        TicketId = @TicketId,
                        UserId = @UserId,
                        ThreadId = @ThreadId,
                        Subject = @Subject,
                        Status = @Status,
                        Text = @Text,
                        CreatedOnUTC = @CreatedOnUTC,
                        ClosedOnUTC = @ClosedOnUTC,
                        ClosedByDiscordId = @ClosedByDiscordId,
                        MessageIds = @MessageIds
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("TicketId", record.TicketId),
                    new MySqlParameter("UserId", record.UserId),
                    new MySqlParameter("ThreadId", record.ThreadId),
                    new MySqlParameter("Subject", record.Subject),
                    new MySqlParameter("Status", record.Status),
                    new MySqlParameter("Text", record.Text),
                    new MySqlParameter("CreatedOnUTC", record.CreatedOnUTC),
                    new MySqlParameter("ClosedOnUTC", record.ClosedOnUTC),
                    new MySqlParameter("ClosedByDiscordId", record.ClosedByDiscordId),
                    new MySqlParameter("MessageIds", record.Messages)           // TODO: Figure This Part Out
                };

                await sql.RunCmd(query, parameters);
                return;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlDiscordTicketDataProvider.InsertOrUpdate");
                return;
            }
        }
    }
}
