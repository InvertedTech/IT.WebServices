using Google.Protobuf;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Tax.Data;
using IT.WebServices.Authorization.Payment.Tax.Helpers;
using IT.WebServices.Crypto;
using IT.WebServices.Fragments.Authorization.Payment.Tax;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IT.WebServices.Authorization.Payment.Tax.Services
{
    [Authorize(Roles = RoleAbilities.ROLE_CAN_BACKUP)]
    public class BackupService : BackupInterface.BackupInterfaceBase
    {
        private readonly ISalesTaxRecordProvider dataProvider;
        private readonly ILogger log;

        public BackupService(ISalesTaxRecordProvider dataProvider, ILogger<BackupService> log)
        {
            this.dataProvider = dataProvider;
            this.log = log;
        }

        public override async Task BackupAllData(BackupAllDataRequest request, IServerStreamWriter<BackupAllDataResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                if (userToken == null || !userToken.Roles.Contains(RoleAbilities.ROLE_BACKUP))
                    return;

                var encKey = EcdhHelper.DeriveKeyServer(request.ClientPublicJwk.DecodeJsonWebKey(), out string serverPubKey);
                await responseStream.WriteAsync(new() { ServerPublicJwk = serverPubKey });

                await foreach (var r in dataProvider.GetAll())
                {
                    var dr = new TaxBackupDataRecord()
                    {
                        SalesTaxRecord = r
                    };

                    AesHelper.Encrypt(encKey, out var iv, dr.ToByteString().ToByteArray(), out var encData);

                    await responseStream.WriteAsync(new BackupAllDataResponse()
                    {
                        EncryptedRecord = new()
                        {
                            EncryptionIV = ByteString.CopyFrom(iv),
                            Data = ByteString.CopyFrom(encData)
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in BackupAllData");
            }
        }

        public override async Task<RestoreAllDataResponse> RestoreAllData(IAsyncStreamReader<RestoreAllDataRequest> requestStream, ServerCallContext context)
        {
            log.LogWarning("*** RestoreAllData - Entrance ***");

            RestoreAllDataResponse res = new RestoreAllDataResponse();
            List<SalesTaxTuple> tuplesLoaded = new List<SalesTaxTuple>();

            await requestStream.MoveNext();
            if (requestStream.Current.RequestOneofCase != RestoreAllDataRequest.RequestOneofOneofCase.Mode)
            {
                log.LogWarning("*** RestoreAllData - Mode missing ***");
                return res;
            }

            var restoreMode = requestStream.Current.Mode;

            try
            {
                await foreach (var r in requestStream.ReadAllAsync())
                {
                    var record = r.Record.SalesTaxRecord;
                    tuplesLoaded.Add(record.ToTuple());

                    try
                    {
                        if (await dataProvider.Exists(record.CountryCode, record.PostalCode))
                        {
                            if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.MissingOnly)
                            {
                                res.NumRecordsSkipped++;
                                continue;
                            }

                            await dataProvider.Save(record);
                            res.NumRecordsOverwriten++;
                        }
                        else
                        {
                            await dataProvider.Save(record);
                            res.NumRecordsRestored++;
                        }
                    }
                    catch { }
                }

                if (restoreMode == RestoreAllDataRequest.Types.RestoreMode.Wipe)
                {
                    await foreach (var record in dataProvider.GetAll())
                    {
                        var tuple = record.ToTuple();

                        if (!tuplesLoaded.Contains(tuple))
                        {
                            await dataProvider.Delete(record.CountryCode, record.PostalCode);
                            res.NumRecordsWiped++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning("*** RestoreAllData - ERROR ***");
                log.LogWarning($"*** RestoreAllData - ERROR: {ex.Message} ***");
            }

            log.LogWarning("*** RestoreAllData - Exit ***");

            return res;
        }
    }
}
