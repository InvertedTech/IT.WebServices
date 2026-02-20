using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using IT.WebServices.Authentication;
using IT.WebServices.Content.CMS.Services.Data;
using IT.WebServices.Fragments.Content;
using Asset = IT.WebServices.Fragments.Content.Asset;
using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using IT.WebServices.Crypto;

namespace IT.WebServices.Content.CMS.Services
{
    [Authorize(Roles = RoleAbilities.ROLE_CAN_BACKUP)]
    public class AssetBackupService : Asset.AssetBackupInterface.AssetBackupInterfaceBase
    {
        private readonly IAssetDataProvider dataProvider;
        private readonly ILogger logger;

        public AssetBackupService(IAssetDataProvider dataProvider, ILogger<AssetBackupService> logger)
        {
            this.dataProvider = dataProvider;
            this.logger = logger;
        }

        public override async Task BackupAllData(Asset.BackupAllDataRequest request, IServerStreamWriter<Asset.BackupAllDataResponse> responseStream, ServerCallContext context)
        {
            try
            {
                var encKey = EcdhHelper.DeriveKeyServer(request.ClientPublicJwk.DecodeJsonWebKey(), out string serverPubKey);
                await responseStream.WriteAsync(new Asset.BackupAllDataResponse() { ServerPublicJwk = serverPubKey });

                await foreach (var r in dataProvider.GetAll())
                {
                    var dr = new Asset.AssetBackupDataRecord()
                    {
                        Data = r
                    };

                    AesHelper.Encrypt(encKey, out var iv, dr.ToByteString().ToByteArray(), out var encData);

                    await responseStream.WriteAsync(new Asset.BackupAllDataResponse()
                    {
                        EncryptedRecord = new Asset.EncryptedAssetBackupDataRecord()
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

        public override async Task<Asset.RestoreAllDataResponse> RestoreAllData(IAsyncStreamReader<Asset.RestoreAllDataRequest> requestStream, ServerCallContext context)
        {
            Asset.RestoreAllDataResponse res = new();
            HashSet<Guid> idsLoaded = new HashSet<Guid>();

            await requestStream.MoveNext();
            if (requestStream.Current.RequestOneofCase != Asset.RestoreAllDataRequest.RequestOneofOneofCase.Mode)
                return res;

            var restoreMode = requestStream.Current.Mode;

            try
            {
                await foreach (var r in requestStream.ReadAllAsync())
                {
                    Guid id = r.Record.Data.AssetIDGuid;
                    idsLoaded.Add(id);

                    try
                    {
                        if (await dataProvider.Exists(id))
                        {
                            if (restoreMode == Asset.RestoreAllDataRequest.Types.RestoreMode.MissingOnly)
                            {
                                res.NumRecordsSkipped++;
                                continue;
                            }

                            await dataProvider.Save(r.Record.Data);
                            res.NumRecordsOverwriten++;
                        }
                        else
                        {
                            await dataProvider.Save(r.Record.Data);
                            res.NumRecordsRestored++;
                        }
                    }
                    catch { }
                }

                if (restoreMode == Asset.RestoreAllDataRequest.Types.RestoreMode.Wipe)
                {
                    await foreach (var r in dataProvider.GetAll())
                    {
                        Guid id = r.AssetIDGuid;
                        if (!idsLoaded.Contains(id))
                        {
                            await dataProvider.Delete(id);
                            res.NumRecordsWiped++;
                        }
                    }
                }
            }
            catch
            {
            }

            return res;
        }
    }
}
