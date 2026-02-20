using Google.Protobuf;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Crypto;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Authorization.Payment
{
    [Authorize(Roles = RoleAbilities.ROLE_CAN_BACKUP)]
    public class BackupService : Fragments.Authorization.Payment.BackupInterface.BackupInterfaceBase
    {
        private readonly IGenericSubscriptionFullRecordProvider fullProvider;
        private readonly IGenericSubscriptionRecordProvider subProvider;
        private readonly ILogger logger;

        public BackupService(IGenericSubscriptionFullRecordProvider fullProvider, IGenericSubscriptionRecordProvider subProvider, ILogger<BackupService> logger)
        {
            this.fullProvider = fullProvider;
            this.subProvider = subProvider;
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

                await foreach (var r in fullProvider.GetAll())
                {
                    var dr = new PaymentBackupDataRecord()
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
                    Guid userId = r.Record.SubscriptionRecord.SubscriptionRecord.UserID.ToGuid();
                    Guid subId = r.Record.SubscriptionRecord.SubscriptionRecord.InternalSubscriptionID.ToGuid();
                    idsLoaded.Add(subId);

                    try
                    {
                        if (await subProvider.Exists(userId, subId))
                        {
                            if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.MissingOnly)
                            {
                                res.NumSubscriptionsSkipped++;
                                continue;
                            }

                            await fullProvider.Save(r.Record.SubscriptionRecord);
                            res.NumSubscriptionsOverwriten++;
                        }
                        else
                        {
                            await fullProvider.Save(r.Record.SubscriptionRecord);
                            res.NumSubscriptionsRestored++;
                        }
                    }
                    catch { }
                }

                if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.Wipe)
                {
                    await foreach (var tuple in subProvider.GetAllSubscriptionIds())
                    {
                        if (!idsLoaded.Contains(tuple.subId))
                        {
                            await fullProvider.Delete(tuple.userId, tuple.subId);
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
