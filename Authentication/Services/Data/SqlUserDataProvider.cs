using IT.WebServices.Authentication.Services.Helpers;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Data
{
    internal class SqlUserDataProvider : IUserDataProvider
    {
        public readonly MySQLHelper sql;
        private readonly ILogger log;

        public SqlUserDataProvider(MySQLHelper sql, ILogger<SqlUserDataProvider> log)
        {
            this.sql = sql;
            this.log = log;
        }

        public Task<bool> ChangeEmailIndex(string email, Guid id) => Task.FromResult(true);

        public Task<bool> ChangeLoginIndex(string oldLoginName, string newLoginName, Guid id) => Task.FromResult(true);

        public async Task<bool> Create(UserRecord user)
        {
            var tasks = new List<Task<bool>>()
            {
                Exists(user.UserIDGuid),
                LoginExists(user.Normal.Public.Data.UserName),
            };

            if (!string.IsNullOrEmpty(user.Normal.Private.Data.Email))
                tasks.Add(EmailExists(user.Normal.Private.Data.Email));

            await Task.WhenAll(tasks);

            if (tasks.Select(t => t.Result).Any(t => t))
                return false;

            await InsertOrUpdate(user);

            return true;
        }

        public async Task<bool> Delete(Guid userId)
        {
            try
            {
                const string query = @"
                    DELETE FROM
                        Auth_User
                    WHERE
                        UserID = @UserID
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString())
                };

                await sql.RunCmd(query, parameters);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> EmailExists(string email)
        {
            try
            {
                const string query = @"
                    SELECT
                        1
                    FROM
                        Auth_User
                    WHERE
                        Email = @Email
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("Email", email)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                    return true;

                return false;
            }
            catch (Exception)
            {
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
                        Auth_User
                    WHERE
                        UserID = @UserID
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString())
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                    return true;

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async IAsyncEnumerable<UserRecord> GetAll()
        {
            var dict = await GetAllTotp();

            const string query = @"
                    SELECT
                        *
                    FROM
                        Auth_User
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var record = rdr.ParseUserRecord();

                if (dict.TryGetValue(record.UserIDGuid, out var devices))
                    record.Server.TOTPDevices.AddRange(devices);

                yield return record;
            }
        }

        public async Task<Guid[]> GetAllIds()
        {
            List<Guid> ids = new List<Guid>();

            const string query = @"
                    SELECT
                        UserID
                    FROM
                        Auth_User
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                ids.Add(rdr.GetString(0).ToGuid());
            }

            return ids.ToArray();
        }

        public async Task<UserRecord> GetByEmail(string email)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Auth_User
                    WHERE
                        Email = @Email;

                    SELECT
                        t.*
                    FROM
                        Auth_User u
                        JOIN Auth_Totp t ON u.UserID = t.UserID
                    WHERE
                        u.Email = @Email;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("Email", email)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseUserRecord();

                    if (await rdr.NextResultAsync())
                        while (await rdr.ReadAsync())
                            record.Server.TOTPDevices.Add(rdr.ParseTOTPDevice().record);

                    return record;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<UserRecord> GetById(Guid userId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Auth_User
                    WHERE
                        UserID = @UserID;

                    SELECT
                        *
                    FROM
                        Auth_Totp
                    WHERE
                        UserID = @UserID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", userId.ToString())
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseUserRecord();

                    if (await rdr.NextResultAsync())
                        while (await rdr.ReadAsync())
                            record.Server.TOTPDevices.Add(rdr.ParseTOTPDevice().record);

                    return record;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<UserRecord> GetByLogin(string loginName)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Auth_User
                    WHERE
                        UserName = @UserName;

                    SELECT
                        t.*
                    FROM
                        Auth_User u
                        JOIN Auth_Totp t ON u.UserID = t.UserID
                    WHERE
                        u.UserName = @UserName;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserName", loginName)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseUserRecord();

                    if (await rdr.NextResultAsync())
                        while (await rdr.ReadAsync())
                            record.Server.TOTPDevices.Add(rdr.ParseTOTPDevice().record);

                    return record;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<UserRecord> GetByOldUserID(string oldUserId)
        {
            try
            {
                const string query = @"
                    SELECT
                        *
                    FROM
                        Auth_User
                    WHERE
                        OldUserID = @OldUserID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("OldUserID", oldUserId)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var record = rdr.ParseUserRecord();

                    return record;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<Guid> GetIdByMicrosoftAuthProviderUserId(string userId)
        {
            try
            {
                const string query = @"
                    SELECT
                        UserID
                    FROM
                        Auth_User
                    WHERE
                        MicrosoftAuthProviderUserID = @MicrosoftAuthProviderUserID;
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("MicrosoftAuthProviderUserID", userId)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                {
                    var str = rdr.GetString(0) ?? "";
                    if (Guid.TryParse(str, out var id))
                        return id;
                }

                return Guid.Empty;
            }
            catch (Exception ex)
            {
                log.LogError("SQL Conn: {connStr}", sql.connString);
                log.LogError(ex, "Error in GetIdByMicrosoftAuthProviderUserId");
                return Guid.Empty;
            }
        }

        public async Task<bool> LoginExists(string loginName)
        {
            try
            {
                const string query = @"
                    SELECT
                        1
                    FROM
                        Auth_User
                    WHERE
                        UserName = @UserName
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserName", loginName)
                };

                using var rdr = await sql.ReturnReader(query, parameters);

                if (await rdr.ReadAsync())
                    return true;

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Task Save(UserRecord user)
        {
            return InsertOrUpdate(user);
        }

        private async Task InsertOrUpdate(UserRecord user)
        {
            try
            {
                const string query = @"
                    INSERT INTO Auth_User
                            (UserID,  UserName,  DisplayName,  Bio,  Roles,  Email,  OldUserID,  PasswordHash,  PasswordSalt,  OldPassword,
                             OldPasswordAlgorithm,  FirstName,  LastName,  PostalCode,  MicrosoftAuthProviderUserId,
                             CreatedOnUTC,  CreatedBy,  ModifiedOnUTC,  ModifiedBy,  DisabledOnUTC,  DisabledBy)
                    VALUES (@UserID, @UserName, @DisplayName, @Bio, @Roles, @Email, @OldUserID, @PasswordHash, @PasswordSalt, @OldPassword,
                            @OldPasswordAlgorithm, @FirstName, @LastName, @PostalCode, @MicrosoftAuthProviderUserId,
                            @CreatedOnUTC, @CreatedBy, @ModifiedOnUTC, @ModifiedBy, @DisabledOnUTC, @DisabledBy)
                    ON DUPLICATE KEY UPDATE
                            UserName = @UserName,
                            DisplayName = @DisplayName,
                            Bio = @Bio,
                            Roles = @Roles,
                            Email = @Email,
                            OldUserID = @OldUserID,
                            PasswordHash = @PasswordHash,
                            PasswordSalt = @PasswordSalt,
                            OldPassword = @OldPassword,
                            OldPasswordAlgorithm = @OldPasswordAlgorithm,
                            FirstName = @FirstName,
                            LastName = @LastName,
                            PostalCode = @PostalCode,
                            MicrosoftAuthProviderUserId = @MicrosoftAuthProviderUserId,
                            CreatedOnUTC = @CreatedOnUTC,
                            CreatedBy = @CreatedBy,
                            ModifiedOnUTC = @ModifiedOnUTC,
                            ModifiedBy = @ModifiedBy,
                            DisabledOnUTC = @DisabledOnUTC,
                            DisabledBy = @DisabledBy
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("UserID", user.Normal.Public.UserID),
                    new MySqlParameter("UserName", user.Normal.Public.Data.UserName),
                    new MySqlParameter("DisplayName", user.Normal.Public.Data.DisplayName),
                    new MySqlParameter("Bio", user.Normal.Public.Data.Bio),
                    new MySqlParameter("Roles", string.Join(",", user.Normal.Private.Roles)),
                    new MySqlParameter("Email", user.Normal.Private.Data.Email),
                    new MySqlParameter("OldUserID", user.Normal.Private.Data.OldUserID),
                    new MySqlParameter("PasswordHash", user.Server.PasswordHash?.ToByteArray()),
                    new MySqlParameter("PasswordSalt", user.Server.PasswordSalt?.ToByteArray()),
                    new MySqlParameter("OldPassword", user.Server.OldPassword),
                    new MySqlParameter("OldPasswordAlgorithm", user.Server.OldPasswordAlgorithm),
                    new MySqlParameter("FirstName", user.Normal.Private.Data.FirstName),
                    new MySqlParameter("LastName", user.Normal.Private.Data.LastName),
                    new MySqlParameter("PostalCode", user.Normal.Private.Data.PostalCode),
                    new MySqlParameter("MicrosoftAuthProviderUserId", user.Server.AuthProviders?.Microsoft?.UserId is null ? DBNull.Value : user.Server.AuthProviders?.Microsoft?.UserId),
                    new MySqlParameter("CreatedOnUTC", user.Normal.Public.CreatedOnUTC.ToDateTime()),
                    new MySqlParameter("CreatedBy", user.Normal.Private.CreatedBy),
                    new MySqlParameter("ModifiedOnUTC", user.Normal.Public.ModifiedOnUTC?.ToDateTime()),
                    new MySqlParameter("ModifiedBy", user.Normal.Private.ModifiedBy),
                    new MySqlParameter("DisabledOnUTC", user.Normal.Public.DisabledOnUTC?.ToDateTime()),
                    new MySqlParameter("DisabledBy", user.Normal.Private.DisabledBy.Length == 36 ? user.Normal.Private.DisabledBy : null)
                };

                await sql.RunCmd(query, parameters);

                foreach (var d in user.Server.TOTPDevices)
                    await InsertOrUpdateTotp(d, user.UserIDGuid);
            }
            catch (Exception)
            {
            }
        }

        private async Task InsertOrUpdateTotp(TOTPDevice device, Guid userId)
        {
            try
            {
                const string query = @"
                    INSERT INTO Auth_Totp
                            (TotpID,  UserID,  DeviceName, `Key`,  CreatedOnUTC,  VerifiedOnUTC,  DisabledOnUTC)
                    VALUES (@TotpID, @UserID, @DeviceName, @Key,  @CreatedOnUTC, @VerifiedOnUTC, @DisabledOnUTC)
                    ON DUPLICATE KEY UPDATE
                            VerifiedOnUTC = @VerifiedOnUTC,
                            DisabledOnUTC = @DisabledOnUTC
                ";

                var parameters = new MySqlParameter[]
                {
                    new MySqlParameter("TotpID", device.TotpID),
                    new MySqlParameter("UserID", userId.ToString()),
                    new MySqlParameter("DeviceName", device.DeviceName),
                    new MySqlParameter("Key", device.Key.ToByteArray()),
                    new MySqlParameter("CreatedOnUTC", device.CreatedOnUTC.ToDateTime()),
                    new MySqlParameter("VerifiedOnUTC", device.VerifiedOnUTC?.ToDateTime()),
                    new MySqlParameter("DisabledOnUTC", device.DisabledOnUTC?.ToDateTime()),
                };

                await sql.RunCmd(query, parameters);
            }
            catch (Exception)
            {
            }
        }

        private async Task<Dictionary<Guid, List<TOTPDevice>>> GetAllTotp()
        {
            Dictionary<Guid, List<TOTPDevice>> dict = new();

            const string query = @"
                    SELECT
                        *
                    FROM
                        Auth_Totp
                    WHERE
                        DisabledOnUTC IS NULL
                ";

            using var rdr = await sql.ReturnReader(query);

            while (await rdr.ReadAsync())
            {
                var ret = rdr.ParseTOTPDevice();
                if (!dict.ContainsKey(ret.id))
                    dict[ret.id] = new();
                dict[ret.id].Add(ret.record);
            }

            return dict;
        }
    }
}
