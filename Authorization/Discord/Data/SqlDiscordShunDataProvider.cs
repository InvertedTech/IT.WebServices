using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Fragments.Authorization.Discord;
using IT.WebServices.Helpers;
using MySql.Data.MySqlClient;

namespace IT.WebServices.Authorization.Discord.Data
{
    internal class SqlDiscordShunDataProvider : IShunDataProvider
    {
        public readonly MySQLHelper sql;
        private readonly ILogger log;

        public SqlDiscordShunDataProvider(MySQLHelper sql, ILogger<SqlDiscordShunDataProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }
        public async Task<bool> Create(DiscordShunRecord record)
        {
            try
            {
                await InsertOrUpdate(record);
                return true;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlDiscordShunDataProvider.Create");
                return false;
            }
        }

        public async IAsyncEnumerable<DiscordShunRecord> GetAll()
        {
            const string query = @"
                SELECT
                    *
                 FROM
                    Discord_Shun
            ";

            using var rdr = await sql.ReturnReader(query);
            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseDiscordShunRecord();
                yield return record;
            }
        }

        public async Task<DiscordShunRecord> GetById(Guid id)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Discord_Shun
                    WHERE
                        ShunId = @ShunId
                ";

                var parameters = new MySqlParameter[1]
                {
                    new MySqlParameter("ShunId", id.ToString())
                };

                using var rdr = await sql.ReturnReader(query);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseDiscordShunRecord();
                    return record;
                }
                return null;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlDiscordShunDataProvider.InsertOrUpdate");
                return null;
            }
        }

        public Task Save(DiscordShunRecord record)
        {
            return InsertOrUpdate(record);
        }

        private async Task InsertOrUpdate(DiscordShunRecord record)
        {
            try
            {
                const string query = @"
                    INSERT INTO Discord_Shun(
                        ShunId, UserId, ShunnedByDiscordId, Reason, Status,
                        CreatedOnUTC, UnShunnedOnUTC, UnShunnedByDiscordId
                    ) VALUES (
                        @ShunId, @UserId, @ShunnedByDiscordId, @Reason, @Status,
                        @CreatedOnUTC, @UnShunnedOnUTC, @UnShunnedByDiscordId
                    )
                       ON DUPLICATE KEY UPDATE
                            ShunId = @ShunId,
                            UserId = @UserId,
                            ShunnedByDiscordId = @ShunnedByDiscordId,
                            Reason = @Reason,
                            Status = @Status,
                            CreatedOnUTC = @CreatedOnUTC,
                            UnShunnedOnUTC = @UnShunnedOnUTC,
                            UnShunnedByDiscordId = @UnShunnedByDiscordId
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("ShunId", record.ShunId),
                    new MySqlParameter("UserId", record.UserId),
                    new MySqlParameter("ShunnedByDiscordId", record.ShunnedByDiscordId),
                    new MySqlParameter("Reason", record.Reason),
                    new MySqlParameter("Status", record.Status),
                    new MySqlParameter("CreatedOnUTC", record.CreatedOnUTC),
                    new MySqlParameter("UnShunnedOnUTC", record.UnShunnedOnUTC),
                    new MySqlParameter("UnShunnedByDiscordId", record.UnShunnedByDiscordId)
                };

                await sql.RunCmd(query, parameters);
                return;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlDiscordShunDataProvider.InsertOrUpdate");
                return;
            }
        }
    }
}
