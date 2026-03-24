using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IT.WebServices.Models;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IT.WebServices.Helpers;

namespace IT.WebServices.Authentication.Services.Data
{
    public class FileSystemUserDataProvider : IUserDataProvider
    {
        private readonly DirectoryInfo dataDir;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<string, Guid> emailIndex = new();
        private readonly ConcurrentDictionary<string, Guid> loginIndex = new();

        public FileSystemUserDataProvider(IOptions<AppSettings> settings, ILogger<FileSystemUserDataProvider> logger)
        {
            this.logger = logger;

            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataDir = root.CreateSubdirectory("auth").CreateSubdirectory("data");

            LoadIndex().Wait();
        }

        private async Task LoadIndex()
        {
            await foreach (var r in GetAll())
            {
                loginIndex.TryAdd(r.Normal.Public.Data.UserName.ToLower(), r.UserIDGuid);

                emailIndex.TryAdd(r.Normal.Private.Data.Email.ToLower(), r.UserIDGuid);
            }
        }

        public Task<bool> ChangeEmailIndex(string email, Guid id)
        {
            var toDel = emailIndex.Where(kv => kv.Value == id).Select(kv => kv.Key).ToArray();
            foreach (var e in toDel)
                emailIndex.TryRemove(e, out var dummy);

            emailIndex.TryAdd(email.ToLower(), id);

            return Task.FromResult(true);
        }

        public Task<bool> ChangeLoginIndex(string oldLoginName, string newLoginName, Guid id)
        {
            if (!loginIndex.ContainsKey(oldLoginName.ToLower()))
                return Task.FromResult(false);

            if (!loginIndex.TryAdd(newLoginName.ToLower(), id))
                return Task.FromResult(false);

            loginIndex.TryRemove(oldLoginName.ToLower(), out var dummy);

            return Task.FromResult(true);
        }

        public async Task<bool> Create(UserRecord user)
        {
            var id = user.UserIDGuid;
            var fd = GetDataFilePath(id);

            if (fd.Exists)
                return false;

            if (!loginIndex.TryAdd(user.Normal.Public.Data.UserName.ToLower(), id))
                return false;

            await File.WriteAllBytesAsync(fd.FullName, user.ToByteArray());

            return true;
        }

        public async Task<bool> Delete(Guid userId)
        {
            var fd = GetDataFilePath(userId);
            if (!fd.Exists)
                return false;

            var rec = UserRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fd.FullName));
            fd.Delete();

            loginIndex.TryRemove(rec.Normal.Public.Data.UserName.ToLower(), out var dummy);

            emailIndex.TryRemove(rec.Normal.Private.Data.Email.ToLower(), out var dummy2);

            return true;
        }

        public Task<bool> Exists(Guid userId)
        {
            var fd = GetDataFilePath(userId);
            return Task.FromResult(fd.Exists);
        }

        public Task<bool> EmailExists(string email)
        {
            return Task.FromResult(emailIndex.TryGetValue(email.ToLower(), out var dummy));
        }

        public Task<bool> LoginExists(string loginName)
        {
            return Task.FromResult(loginIndex.TryGetValue(loginName.ToLower(), out var dummy));
        }

        public async IAsyncEnumerable<UserRecord> GetAll()
        {
            foreach (var fd in GetAllDataFiles())
                yield return UserRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fd.FullName));
        }

        public Task<Guid[]> GetAllIds()
        {
            return Task.FromResult(GetAllDataFiles().Select(f => Guid.Parse(f.Name)).ToArray());
        }

        public async Task<UserRecord> GetById(Guid userId)
        {
            var fd = GetDataFilePath(userId);
            if (!fd.Exists)
                return null;

            return UserRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fd.FullName));
        }

        public async Task<UserRecord> GetByEmail(string email)
        {
            if (emailIndex.TryGetValue(email.ToLower(), out var id))
                return await GetById(id);

            return null;
        }

        public async Task<UserRecord> GetByLogin(string loginName)
        {
            if (loginIndex.TryGetValue(loginName.ToLower(), out var id))
                return await GetById(id);

            return null;
        }

        public async Task<UserRecord> GetByOldUserID(string oldUserId)
        {
            await foreach(var record in GetAll())
                if (record.Normal.Private.Data.OldUserID == oldUserId)
                    return record;

            return null;
        }

        public async Task Save(UserRecord user)
        {
            user.Normal.Public.Data.UserName = user.Normal.Public.Data.UserName.ToLower();

            var id = user.UserIDGuid;
            var fd = GetDataFilePath(id);
            await File.WriteAllBytesAsync(fd.FullName, user.ToByteArray());

            loginIndex.AddOrUpdate(user.Normal.Public.Data.UserName.ToLower(), id, (k, v) => id);

            emailIndex.AddOrUpdate(user.Normal.Private.Data.Email, id, (k, v) => id);
        }

        private IEnumerable<FileInfo> GetAllDataFiles()
        {
            return dataDir.EnumerateFiles("*", SearchOption.AllDirectories);
        }

        private FileInfo GetDataFilePath(Guid userID)
        {
            return dataDir.CreateGuidFileInfo(userID);
        }

        public async Task<Guid> GetIdByMicrosoftAuthProviderUserId(string userId)
        {
            await foreach (var record in GetAll())
                if (record.Server.AuthProviders?.Microsoft?.UserId == userId)
                    return record.UserIDGuid;

            return Guid.Empty;
        }

        public async Task<Guid> GetIdByDiscordAuthProviderUserId(string userId)
        {
            await foreach (var record in GetAll())
                if (record.Server.AuthProviders?.Discord?.DiscordId == userId)
                    return record.UserIDGuid;

            return Guid.Empty;
        }
    }
}
