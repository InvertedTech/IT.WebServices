using IT.WebServices.Authorization.Discord.Helpers;
using IT.WebServices.Fragments.Authorization.Discord;
using IT.WebServices.Helpers;
using MySql.Data.MySqlClient;

namespace IT.WebServices.Authorization.Discord.Data
{
    internal class SqlMemberDataProvider : IMemberDataProvider
    {
        public readonly MySQLHelper sql;
        private readonly ILogger log;

        public SqlMemberDataProvider(MySQLHelper sql, ILogger<SqlMemberDataProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }
        public async Task<bool> Create(DiscordMemberRecord member)
        {
            var exists = await Exists(member.UserIdGuid);
            if (exists)
                return false;

            await InsertOrUpdate(member);
            return true;
        }

        public async Task<bool> Delete(Guid userId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Discord_Member
                    WHERE
                        UserId = @UserId
                ";

                var parameters = new MySqlParameter[1]
                {
                    new MySqlParameter("UserId", userId.ToString())
                };

                await sql.RunCmd(query, parameters) ;

                return true;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlMemberDataProvider.Delete");
                return false;
            }
        }

        public async Task<bool> Exists(Guid userId)
        {
            try
            {
                const string query = @"
                    SELECT
                        1
                    FROM
                        Discord_Member
                    WHERE
                        UserId = @UserId
                ";

                var parameters = new MySqlParameter[1]
                {
                    new MySqlParameter("UserId", userId.ToString())
                };

                using var rdr = await sql.ReturnReader(query, parameters);
                if (await rdr.ReadAsync()) 
                    return true;

                return false;
            } catch (Exception ex)
            {
                return false;
            }
        }

        public async IAsyncEnumerable<DiscordMemberRecord> GetAll()
        {
            const string query = @"
                    SELECT
                        *
                    FROM
                        Discord_Member
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseDiscordMemberRecord();

                yield return record;
            }
        }

        public async Task<DiscordMemberRecord> GetByDiscordId(string discordId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Discord_Member
                    WHERE
                        DiscordId = @DiscordId;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("DiscordId", discordId)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseDiscordMemberRecord();
                    return record;
                }

                return null;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlDiscordMemberDataProvider.GetByDiscordId");
                return null;
            }
        }

        public async Task<DiscordMemberRecord> GetByUserId(Guid userId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Discord_Member
                    WHERE
                        UserId = @UserId;
                ";
                var parameters = new MySqlParameter[]
{
                    new MySqlParameter("UserId", userId.ToString())
};

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseDiscordMemberRecord();
                    return record;
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlDiscordMemberDataProvider.GetByUserId");
                return null;
            }
        }

        public Task Save(DiscordMemberRecord member)
        {
            return InsertOrUpdate(member);
        }

        private async Task InsertOrUpdate(DiscordMemberRecord member)
        {
            try
            {
                const string query = @"
                    INSERT INTO Discord_Member
                            (UserID,  DiscordUserId,  DiscordUserName,  Tiers,  BannedReason,  BannedOnUTC,  BannedByDiscordId,
                             CreatedOnUTC,  CreatedById,  ModifiedOnUTC,  ModifiedById,  InternalSubscriptionId,
                             AccessToken,  RefreshToken,  AccessTokenExpiresOnUTC)
                    VALUES (@UserID, @DiscordUserId, @DiscordUserName, @Tiers, @BannedReason, @BannedOnUTC, @BannedByDiscordId,
                            @CreatedOnUTC, @CreatedById, @ModifiedOnUTC, @ModifiedById, @InternalSubscriptionId,
                            @AccessToken, @RefreshToken, @AccessTokenExpiresOnUTC)
                    ON DUPLICATE KEY UPDATE
                            DiscordUserId = @DiscordUserId,
                            DiscordUserName = @DiscordUserName,
                            Tiers = @Tiers,
                            BannedReason = @BannedReason,
                            BannedOnUTC = @BannedOnUTC,
                            BannedByDiscordId = @BannedByDiscordId,
                            CreatedOnUTC = @CreatedOnUTC,
                            CreatedById = @CreatedById,
                            ModifiedOnUTC = @ModifiedOnUTC,
                            ModifiedById = @ModifiedById,
                            InternalSubscriptionId = @InternalSubscriptionId,
                            AccessToken = @AccessToken,
                            RefreshToken = @RefreshToken,
                            AccessTokenExpiresOnUTC = @AccessTokenExpiresOnUTC
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", member.UserId),
                    new MySqlParameter("DiscordUserId", member.Public.DiscordUserId),
                    new MySqlParameter("DiscordUserName", member.Public.DiscordUserName),
                    new MySqlParameter("Tiers", string.Join(",", member.Public.Tiers)),
                    new MySqlParameter("BannedReason", member.Public.BannedReason),
                    new MySqlParameter("BannedOnUTC", member.Public.BannedOnUTC?.ToDateTime()),
                    new MySqlParameter("BannedByDiscordId", member.Private.BannedByDiscordId),
                    new MySqlParameter("CreatedOnUTC", member.Public.CreatedOnUTC.ToDateTime()),
                    new MySqlParameter("CreatedById", member.Private.CreatedById),
                    new MySqlParameter("ModifiedOnUTC", member.Public.ModifiedOnUTC?.ToDateTime()),
                    new MySqlParameter("ModifiedById", member.Private.ModifiedById),
                    new MySqlParameter("InternalSubscriptionId", member.Private.InternalSubscriptionId),
                    new MySqlParameter("AccessToken", string.IsNullOrEmpty(member.Private.AccessToken) ? DBNull.Value : member.Private.AccessToken),
                    new MySqlParameter("RefreshToken", string.IsNullOrEmpty(member.Private.RefreshToken) ? DBNull.Value : member.Private.RefreshToken),
                    new MySqlParameter("AccessTokenExpiresOnUTC", member.Private.AccessTokenExpiresOnUTC == null ? DBNull.Value : member.Private.AccessTokenExpiresOnUTC.ToDateTime()),
                };

                await sql.RunCmd(query, parameters);
                return;
            } catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlMemberDataProvider.InsertOrUpdate");
                return;
            }
        }
    }
}
