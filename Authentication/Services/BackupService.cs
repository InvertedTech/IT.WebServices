using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IT.WebServices.Authentication.Services.Data;
using IT.WebServices.Authentication.Services.Helpers;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IT.WebServices.Crypto;

namespace IT.WebServices.Authentication.Services
{
    [Authorize(Roles = RoleAbilities.ROLE_CAN_BACKUP)]
    public class BackupService : BackupInterface.BackupInterfaceBase
    {
        private readonly IUserDataProvider dataProvider;
        private readonly ILogger<BackupService> logger;

        public BackupService(IUserDataProvider dataProvider, ILogger<BackupService> logger)
        {
            this.dataProvider = dataProvider;
            this.logger = logger;
        }

        public override async Task BackupAllData(BackupAllDataRequest request, IServerStreamWriter<BackupAllDataResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null || !userToken.Roles.Contains(RoleAbilities.ROLE_BACKUP))
                    return;

                var encKey = EcdhHelper.DeriveKeyServer(request.ClientPublicJwk.DecodeJsonWebKey(), out string serverPubKey);
                await responseStream.WriteAsync(new BackupAllDataResponse() { ServerPublicJwk = serverPubKey });

                await foreach (var r in dataProvider.GetAll())
                {
                    var dr = new UserBackupDataRecord()
                    {
                        Data = r
                    };

                    AesHelper.Encrypt(encKey, out var iv, dr.ToByteString().ToByteArray(), out var encData);

                    await responseStream.WriteAsync(new BackupAllDataResponse()
                    {
                        EncryptedRecord = new EncryptedUserBackupDataRecord()
                        {
                            EncryptionIV = ByteString.CopyFrom(iv),
                            Data = ByteString.CopyFrom(encData)
                        }
                    });
                }
            }
            catch
            {
            }
        }

        [AllowAnonymous]
        public override async Task ExportUsers(ExportUsersRequest request, IServerStreamWriter<ExportUsersResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null || !(userToken.RoleAbilities.IsBackup || userToken.RoleAbilities.IsAdminOrHigher))
                    return;

                await foreach (var r in dataProvider.GetAll())
                    await responseStream.WriteAsync(new ExportUsersResponse() { UserRecord = r.Normal.Public });
            }
            catch
            {
            }
        }

        public override async Task<RestoreAllDataResponse> RestoreAllData(IAsyncStreamReader<RestoreAllDataRequest> requestStream, ServerCallContext context)
        {
            logger.LogWarning("*** RestoreAllData - Entrance ***");

            RestoreAllDataResponse res = new RestoreAllDataResponse();
            List<Guid> idsLoaded = new List<Guid>();

            await requestStream.MoveNext();
            if (requestStream.Current.RequestOneofCase != RestoreAllDataRequest.RequestOneofOneofCase.Mode)
            {
                logger.LogWarning("*** RestoreAllData - Mode missing ***");
                return res;
            }

            var restoreMode = requestStream.Current.Mode;

            try
            {
                await foreach (var r in requestStream.ReadAllAsync())
                {
                    Guid id = r.Record.Data.Normal.Public.UserID.ToGuid();
                    idsLoaded.Add(id);

                    try
                    {
                        if (await dataProvider.Exists(id))
                        {
                            if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.MissingOnly)
                            {
                                res.NumUsersSkipped++;
                                continue;
                            }

                            await dataProvider.Save(r.Record.Data);
                            res.NumUsersOverwriten++;
                        }
                        else
                        {
                            await dataProvider.Save(r.Record.Data);
                            Console.WriteLine(r.Record.Data.Normal.Private.Data.OldUserID);
                            res.NumUsersRestored++;
                        }
                    }
                    catch { }
                }

                if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.Wipe)
                {
                    foreach (var id in await dataProvider.GetAllIds())
                    {
                        if (!idsLoaded.Contains(id))
                        {
                            await dataProvider.Delete(id);
                            res.NumUsersWiped++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("*** RestoreAllData - ERROR ***");
                logger.LogWarning($"*** RestoreAllData - ERROR: {ex.Message} ***");
            }

            logger.LogWarning("*** RestoreAllData - Exit ***");

            return res;
        }
    }
}
