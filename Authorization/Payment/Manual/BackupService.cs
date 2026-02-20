using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Crypto;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Manual.Data;
using IT.WebServices.Fragments.Authorization.Payment.Manual;
using System.Linq;

namespace IT.WebServices.Authorization.Payment.Manual
{
    [Authorize(Roles = RoleAbilities.ROLE_CAN_BACKUP)]
    public class BackupService : BackupInterface.BackupInterfaceBase
    {
        private readonly ISubscriptionRecordProvider dataProvider;
        private readonly ILogger<BackupService> logger;

        public BackupService(ISubscriptionRecordProvider dataProvider, ILogger<BackupService> logger)
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
                    var dr = new ManualBackupDataRecord()
                    {
                        SubscriptionRecord = r
                    };

                    AesHelper.Encrypt(encKey, out var iv, dr.ToByteString().ToByteArray(), out var encData);

                    await responseStream.WriteAsync(new BackupAllDataResponse()
                    {
                        EncryptedRecord = new EncryptedSubscriptionBackupDataRecord()
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
                    Guid userId = r.Record.SubscriptionRecord.UserID.ToGuid();
                    Guid subId = r.Record.SubscriptionRecord.SubscriptionID.ToGuid();
                    idsLoaded.Add(subId);

                    try
                    {
                        if (await dataProvider.Exists(userId, subId))
                        {
                            if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.MissingOnly)
                            {
                                res.NumSubscriptionsSkipped++;
                                continue;
                            }

                            await dataProvider.Save(r.Record.SubscriptionRecord);
                            res.NumSubscriptionsOverwriten++;
                        }
                        else
                        {
                            await dataProvider.Save(r.Record.SubscriptionRecord);
                            res.NumSubscriptionsRestored++;
                        }
                    }
                    catch { }
                }

                if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.Wipe)
                {
                    await foreach (var tuple in dataProvider.GetAllSubscriptionIds())
                    {
                        if (!idsLoaded.Contains(tuple.subId))
                        {
                            await dataProvider.Delete(tuple.userId, tuple.subId);
                            res.NumSubscriptionsWiped++;
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
