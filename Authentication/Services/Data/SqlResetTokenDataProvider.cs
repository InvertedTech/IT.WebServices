using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Data
{
    internal class SqlResetTokenDataProvider : IResetTokenDataProvider
    {
        private readonly MySQLHelper sql;
        private readonly ILogger log;

        public SqlResetTokenDataProvider(MySQLHelper sql, ILogger<SqlResetTokenDataProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }

        public async Task SaveToken(Guid userId, string tokenHash, DateTime expiresOnUTC)
        {
            try
            {
                const string query = @"
                    INSERT INTO Auth_PasswordReset
                            (UserID,  TokenHash,  ExpiresOnUTC,  CreatedOnUTC)
                    VALUES (@UserID, @TokenHash, @ExpiresOnUTC, @CreatedOnUTC)
                    ON DUPLICATE KEY UPDATE
                            TokenHash = @TokenHash,
                            ExpiresOnUTC = @ExpiresOnUTC,
                            CreatedOnUTC = @CreatedOnUTC
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString()),
                    new MySqlParameter("TokenHash", tokenHash),
                    new MySqlParameter("ExpiresOnUTC", expiresOnUTC),
                    new MySqlParameter("CreatedOnUTC", DateTime.UtcNow),
                };

                await sql.RunCmd(query, parameters);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlResetTokenDataProvider.SaveToken");
            }
        }

        public async Task<ResetTokenRecord?> GetByTokenHash(string tokenHash)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Auth_PasswordReset
                    WHERE
                        TokenHash = @TokenHash
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("TokenHash", tokenHash)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    return new ResetTokenRecord()
                    {
                        UserID = (rdr["UserID"] as string)?.ToGuid() ?? Guid.Empty,
                        TokenHash = rdr["TokenHash"] as string ?? string.Empty,
                        ExpiresOnUTC = DateTime.SpecifyKind((DateTime)rdr["ExpiresOnUTC"], DateTimeKind.Utc),
                        CreatedOnUTC = DateTime.SpecifyKind((DateTime)rdr["CreatedOnUTC"], DateTimeKind.Utc),
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlResetTokenDataProvider.GetByTokenHash");
                return null;
            }
        }

        public async Task DeleteToken(Guid userId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Auth_PasswordReset
                    WHERE
                        UserID = @UserID
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString())
                };

                await sql.RunCmd(query, parameters);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in SqlResetTokenDataProvider.DeleteToken");
            }
        }
    }
}
