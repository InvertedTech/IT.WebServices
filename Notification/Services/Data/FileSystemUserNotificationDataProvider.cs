//using Google.Protobuf;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using IT.WebServices.Models;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Threading.Tasks;
//using IT.WebServices.Fragments.Notification;
//using IT.WebServices.Helpers;

//namespace IT.WebServices.Notification.Services.Data
//{
//    public class FileSystemUserNotificationDataProvider : IUserNotificationDataProvider
//    {
//        private readonly DirectoryInfo dataDir;
//        private readonly ILogger logger;

//        public FileSystemUserNotificationDataProvider(IOptions<AppSettings> settings, ILogger<FileSystemUserNotificationDataProvider> logger)
//        {
//            this.logger = logger;

//            var root = new DirectoryInfo(settings.Value.DataStore);
//            root.Create();
//            dataDir = root.CreateSubdirectory("user");
//        }

//        public Task<bool> Delete(Guid userId)
//        {
//            var fd = GetDataFilePath(userId);
//            if (!fd.Exists)
//                return Task.FromResult(false);

//            fd.Delete();

//            return Task.FromResult(true);
//        }

//        public Task<bool> Exists(Guid userId)
//        {
//            var fd = GetDataFilePath(userId);
//            return Task.FromResult(fd.Exists);
//        }

//        public async IAsyncEnumerable<UserNotificationSettingsRecord> GetAll()
//        {
//            foreach (var fd in GetAllDataFiles())
//                yield return UserNotificationSettingsRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fd.FullName));
//        }

//        public async Task<UserNotificationSettingsRecord> GetById(Guid userId)
//        {
//            var fd = GetDataFilePath(userId);
//            if (!fd.Exists)
//                return null;

//            return UserNotificationSettingsRecord.Parser.ParseFrom(await File.ReadAllBytesAsync(fd.FullName));
//        }

//        public async Task Save(UserNotificationSettingsRecord user)
//        {
//            var id = user.UserIDGuid;
//            var fd = GetDataFilePath(id);
//            await File.WriteAllBytesAsync(fd.FullName, user.ToByteArray());
//        }

//        private IEnumerable<FileInfo> GetAllDataFiles()
//        {
//            return dataDir.EnumerateFiles("*", SearchOption.AllDirectories);
//        }

//        private FileInfo GetDataFilePath(Guid userID)
//        {
//            return dataDir.CreateGuidFileInfo(userID);
//        }
//    }
//}
