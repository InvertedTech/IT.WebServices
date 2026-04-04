using Google.Protobuf;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Helpers;
using IT.WebServices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IT.WebServices.Authentication.Services.Data
{
    public class FileSystemProfilePicDataProvider : IProfilePicDataProvider
    {
        private readonly DirectoryInfo dataDir;
        private readonly ILogger logger;

        public FileSystemProfilePicDataProvider(IOptions<AppSettings> settings, ILogger<FileSystemProfilePicDataProvider> logger)
        {
            this.logger = logger;

            var root = new DirectoryInfo(settings.Value.DataStore);
            root.Create();
            dataDir = root.CreateSubdirectory("auth").CreateSubdirectory("profile");
        }

        public Task<bool> Delete(Guid userId)
        {
            var fd = GetDataFilePath(userId);
            if (!fd.Exists)
                return Task.FromResult(false);

            fd.Delete();

            return Task.FromResult(true);
        }

        public async Task<byte[]?> GetById(Guid userId)
        {
            var fd = GetDataFilePath(userId);
            if (!fd.Exists)
                return null;

            return await File.ReadAllBytesAsync(fd.FullName);
        }

        public async Task Save(Guid userId, byte[] bytes)
        {
            var fd = GetDataFilePath(userId);
            await File.WriteAllBytesAsync(fd.FullName, bytes);
        }

        private FileInfo GetDataFilePath(Guid userId)
        {
            return dataDir.CreateGuidFileInfo(userId);
        }
    }
}
