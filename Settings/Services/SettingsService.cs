using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using IT.WebServices.AuditLog;
using IT.WebServices.Authentication;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.AuditLog;
using IT.WebServices.Fragments.Authentication;
using IT.WebServices.Fragments.Authorization;
using IT.WebServices.Fragments.Generic;
using IT.WebServices.Fragments.Settings;
using IT.WebServices.Helpers;
using IT.WebServices.Settings.Services.Data;
using IT.WebServices.Settings.Services.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

// TODO: Make Error Messages And Types Better
// TODO: Add Validation
namespace IT.WebServices.Settings.Services
{
    public class SettingsService : SettingsInterface.SettingsInterfaceBase, ISettingsService
    {
        private readonly OfflineHelper offlineHelper;
        private readonly ILogger<SettingsService> logger;
        private readonly ISettingsDataProvider dataProvider;
        private readonly AuditLogHelper auditLogHelper;
        private static bool hasEnsuredStockSettings = false;
        private static SemaphoreSlim stockSettingsSemaphore = new SemaphoreSlim(1, 1);

        public SettingsService(
            OfflineHelper offlineHelper,
            ILogger<SettingsService> logger,
            ISettingsDataProvider dataProvider,
            AuditLogHelper auditLogHelper
        )
        {
            this.offlineHelper = offlineHelper;
            this.logger = logger;
            this.dataProvider = dataProvider;
            this.auditLogHelper = auditLogHelper;

            EnsureStockSettings().Wait();
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE)]
        public override Task<GetAdminDataResponse> GetAdminData(
            GetAdminDataRequest request,
            ServerCallContext context
        )
        {
            return GetAdminDataInternal();
        }

        public async Task<GetAdminDataResponse> GetAdminDataInternal()
        {
            var record = await dataProvider.Get();
            if (record == null)
                return new() { };

            return new() { Public = record.Public, Private = record.Private };
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE)]
        public override async Task<GetAdminNewerDataResponse> GetAdminNewerData(
            GetAdminNewerDataRequest request,
            ServerCallContext context
        )
        {
            var record = await dataProvider.Get();
            if (record == null)
                return new() { };

            if (record?.Public?.VersionNum == request.VersionNum)
                record = null;

            return new() { Public = record?.Public, Private = record?.Private };
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_OWNER_OR_SERVICE)]
        public override Task<GetOwnerDataResponse> GetOwnerData(
            GetOwnerDataRequest request,
            ServerCallContext context
        )
        {
            return GetOwnerDataInternal();
        }

        public async Task<GetOwnerDataResponse> GetOwnerDataInternal()
        {
            var record = await dataProvider.Get();
            if (record == null)
                return new() { };

            return new()
            {
                Public = record?.Public,
                Private = record?.Private,
                Owner = record?.Owner,
            };
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_OWNER_OR_SERVICE)]
        public override async Task<GetOwnerNewerDataResponse> GetOwnerNewerData(
            GetOwnerNewerDataRequest request,
            ServerCallContext context
        )
        {
            var record = await dataProvider.Get();
            if (record == null)
                return new() { };

            if (record?.Public?.VersionNum == request.VersionNum)
                record = null;

            return new()
            {
                Public = record?.Public,
                Private = record?.Private,
                Owner = record?.Owner,
            };
        }

        [AllowAnonymous]
        public override async Task<GetPublicDataResponse> GetPublicData(
            GetPublicDataRequest request,
            ServerCallContext context
        )
        {
            var record = await dataProvider.Get();
            if (record == null)
                return new() { };

            return new() { Public = record?.Public };
        }

        [AllowAnonymous]
        public override async Task<GetPublicNewerDataResponse> GetPublicNewerData(
            GetPublicNewerDataRequest request,
            ServerCallContext context
        )
        {
            var record = await dataProvider.Get();
            if (record == null)
                return new() { };

            if (record?.Public?.VersionNum == request.VersionNum)
                record.Public = null;

            return new() { Public = record?.Public };
        }

        [Authorize(Roles = RoleAbilities.ROLE_OWNER)]
        public override async Task<ModifyCMSOwnerDataResponse> ModifyCMSOwnerData(
            ModifyCMSOwnerDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Owner.CMS = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"CMS Owner Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyCMSPrivateDataResponse> ModifyCMSPrivateData(
            ModifyCMSPrivateDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Private.CMS = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"CMS Private Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyCMSPublicDataResponse> ModifyCMSPublicData(
            ModifyCMSPublicDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Public.CMS = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"CMS Public Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_OWNER)]
        public override async Task<ModifyCommentsOwnerDataResponse> ModifyCommentsOwnerData(
            ModifyCommentsOwnerDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Owner.Comments = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Comments Owner Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyCommentsPrivateDataResponse> ModifyCommentsPrivateData(
            ModifyCommentsPrivateDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Private.Comments = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Comments Private Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyCommentsPublicDataResponse> ModifyCommentsPublicData(
            ModifyCommentsPublicDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Public.Comments = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Comments Public Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_OWNER)]
        public override async Task<ModifyNotificationOwnerDataResponse> ModifyNotificationOwnerData(
            ModifyNotificationOwnerDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Owner.Notification = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Notification Owner Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyNotificationPrivateDataResponse> ModifyNotificationPrivateData(
            ModifyNotificationPrivateDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Private.Notification = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Notification Private Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyNotificationPublicDataResponse> ModifyNotificationPublicData(
            ModifyNotificationPublicDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Public.Notification = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Notification Public Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_OWNER)]
        public override async Task<ModifyPersonalizationOwnerDataResponse> ModifyPersonalizationOwnerData(
            ModifyPersonalizationOwnerDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Owner.Personalization = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Personalization Owner Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyPersonalizationPrivateDataResponse> ModifyPersonalizationPrivateData(
            ModifyPersonalizationPrivateDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Private.Personalization = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Personalization Private Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyPersonalizationPublicDataResponse> ModifyPersonalizationPublicData(
            ModifyPersonalizationPublicDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Public.Personalization = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Personalization Public Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_OWNER)]
        public override async Task<ModifySubscriptionOwnerDataResponse> ModifySubscriptionOwnerData(
            ModifySubscriptionOwnerDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Owner.Subscription = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Subscription Owner Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifySubscriptionPrivateDataResponse> ModifySubscriptionPrivateData(
            ModifySubscriptionPrivateDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Private.Subscription = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Subscription Private Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifySubscriptionPublicDataResponse> ModifySubscriptionPublicData(
            ModifySubscriptionPublicDataRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Public.Subscription = request.Data;

                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();

                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Subscription Public Data Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        private async Task EnsureStockSettings()
        {
            if (hasEnsuredStockSettings)
                return;

            await stockSettingsSemaphore.WaitAsync();

            if (hasEnsuredStockSettings)
                return;

            hasEnsuredStockSettings = true;

            try
            {
                if (await dataProvider.Get() != null)
                    return;

                var date = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow);

                var record = new SettingsRecord()
                {
                    Public = new()
                    {
                        VersionNum = 1,
                        ModifiedOnUTC = date,
                        Comments = new()
                        {
                            AllowLinks = false,
                            ExplicitModeEnabled = true,
                            DefaultOrder = Fragments.Comment.CommentOrder.Liked,
                            DefaultRestriction = new()
                            {
                                Minimum = Fragments
                                    .Comment
                                    .CommentRestrictionMinimumEnum
                                    .Subscriber,
                            },
                        },
                        Personalization = new()
                        {
                            Title = "Creator Site",
                            MetaDescription = "My site description",
                            DefaultToDarkMode = true,
                        },
                        Subscription = new()
                        {
                            AllowOther = true,
                            Fortis = new()
                            {
                                Enabled = false,
                            },
                            //Stripe = new()
                            //{
                            //    Enabled = false,
                            //},
                            Paypal = new() { Enabled = false },
                            Crypto = new() { Enabled = false },
                            Manual = new() { Enabled = true },
                        },
                        CMS = new()
                        {
                            DefaultLayout = Fragments.Content.LayoutEnum.List,
                            Menu = new()
                            {
                                AudioMenuLinkName = "Listen",
                                PictureMenuLinkName = "Photos",
                                VideoMenuLinkName = "Watch",
                                WrittenMenuLinkName = "Read",
                            },
                        },
                        Merch = new() {
                            Shopify = new()
                        }
                    },
                    Private = new()
                    {
                        Comments = new() { },
                        Personalization = new() { },
                        Subscription = new() { },
                        Merch = new()
                        {
                            Shopify = new()
                        }
                    },
                    Owner = new()
                    {
                        Comments = new() { },
                        Personalization = new() { },
                        Subscription = new()
                        {
                            Fortis = new(),
                            //Stripe = new(),
                            Paypal = new(),
                        },
                        Merch = new()
                        {
                            Shopify = new()
                        }
                    },
                };

                record.Public.Subscription.Tiers.Add(
                    new SubscriptionTier()
                    {
                        Name = "Basic",
                        Description = "You're basic bro...",
                        Color = "orange",
                        AmountCents = 500,
                    }
                );
                record.Public.Subscription.Tiers.Add(
                    new SubscriptionTier()
                    {
                        Name = "Bronze",
                        Description = "meh...",
                        Color = "bronze",
                        AmountCents = 1000,
                    }
                );
                record.Public.Subscription.Tiers.Add(
                    new SubscriptionTier()
                    {
                        Name = "Silver",
                        Description = "Nice...",
                        Color = "silver",
                        AmountCents = 2500,
                    }
                );
                record.Public.Subscription.Tiers.Add(
                    new SubscriptionTier()
                    {
                        Name = "Gold",
                        Description = "You rock...",
                        Color = "gold",
                        AmountCents = 5000,
                    }
                );

                record.Private.Comments.BlackList.AddRange(new[] { "fuck", "shit", "cunt" });

                await dataProvider.Save(record);
            }
            finally
            {
                stockSettingsSemaphore.Release();
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyEventPublicSettingsResponse> ModifyEventPublicSettings(
            ModifyEventPublicSettingsRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Request Data Is Null") };
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());

                var record = await dataProvider.Get();
                record.Public.Events = request.Data;
                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();
                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Event Public Settings Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyEventPrivateSettingsResponse> ModifyEventPrivateSettings(
            ModifyEventPrivateSettingsRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                var record = await dataProvider.Get();
                record.Private.Events = request.Data;
                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();
                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Event Private Settings Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyEventOwnerSettingsResponse> ModifyEventOwnerSettings(
            ModifyEventOwnerSettingsRequest request,
            ServerCallContext context
        )
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                var record = await dataProvider.Get();
                record.Owner.Events = request.Data;
                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();
                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Event Owner Settings Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);
                return new() { Error = null };
            }
            catch
            {
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnknown, "Unknown error occurred") };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyMerchPublicSettingsResponse> ModifyMerchPublicSettings(ModifyMerchPublicSettingsRequest request, ServerCallContext context)
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Request Body Must Be Provided") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                var record = await dataProvider.Get();
                record.Public.Merch = request.Data;
                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();
                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Merch Public Settings Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);

                return new ModifyMerchPublicSettingsResponse
                {
                    Error = null,
                };
            } catch (Exception ex)
            {
                return new ModifyMerchPublicSettingsResponse()
                {
                    Error = new APIError()
                    {
                        Reason = APIErrorReason.ErrorReasonUnknown,
                        Message = ex.Message,
                    }
                };
            }
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER)]
        public override async Task<ModifyMerchOwnerSettingsResponse> ModifyMerchOwnerSettings(ModifyMerchOwnerSettingsRequest request, ServerCallContext context)
        {
            try
            {
                if (request.Data == null)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Request Body Must Be Provided") };

                var userToken = ONUserHelper.ParseUser(context.GetHttpContext());
                var record = await dataProvider.Get();
                record.Owner.Merch = request.Data;
                record.Public.VersionNum++;
                record.Public.ModifiedOnUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.UtcNow
                );
                record.Private.ModifiedBy = userToken.Id.ToString();
                await dataProvider.Save(record);
                await auditLogHelper.TryLogEvent(
                    new AuditLogEntry()
                    {
                        Action = ActionType.ActionSettingsChanged,
                        Summary = $"Merch Public Settings Modified",
                        Actor = userToken.ToAuditActor(),
                        Metadata =
                        {
                            { "ModifiedData", request.Data.ToString() },
                        },
                    }, logger);

                return new ModifyMerchOwnerSettingsResponse
                {
                    Error = null,
                };
            }
            catch (Exception ex)
            {
                return new ModifyMerchOwnerSettingsResponse()
                {
                    Error = new APIError()
                    {
                        Reason = APIErrorReason.ErrorReasonUnknown,
                        Message = ex.Message,
                    }
                };
            }
        }
    }
}
