using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.AuditLog;
using IT.WebServices.Authentication;
using IT.WebServices.Content.CMS.Services.Data;
using IT.WebServices.Content.CMS.Services.Helpers;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.AuditLog;
using IT.WebServices.Fragments.Content;
using IT.WebServices.Fragments.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Content.CMS.Services
{
    [Authorize]
    public class ContentService : ContentInterface.ContentInterfaceBase
    {
        private readonly ILogger logger;
        private readonly IContentDataProvider dataProvider;
        private readonly StatsClient statsClient;
        private readonly IAuditLogService auditLogHelper;

        public ContentService(ILogger<ContentService> logger, IContentDataProvider dataProvider, StatsClient statsClient, IAuditLogService auditLogHelper)
        {
            this.logger = logger;
            this.dataProvider = dataProvider;
            this.statsClient = statsClient;
            this.auditLogHelper = auditLogHelper;
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_PUBLISH)]
        public override async Task<AnnounceContentResponse> AnnounceContent(AnnounceContentRequest request, ServerCallContext context)
        {
            if (request.AnnounceOnUTC == null)
            {
                var error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonValidationFailed, "Validation Failed");
                error.Validation.Add(new ValidationIssue
                {
                    Field = "AnnounceOnUTC",
                    Message = "AnnounceOnUTC is required",
                    Code = "required"
                });

                return new AnnounceContentResponse
                {
                    Error = error
                };
            }

            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var contentId = request.ContentID.ToGuid();
            var record = await dataProvider.GetById(contentId);
            if (record == null)
            {
                return new AnnounceContentResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Content Not Found")
                };
            }

            // TODO: Make Less Verbose
            var announceDateChanged = new AuditFieldChange
            {
                FieldName = "AnnounceOnUTC",
                BeforeValue = record.Public.AnnounceOnUTC?.ToDateTimeOffset().ToString("o") ?? "null",
            };
            var announceByChanged = new AuditFieldChange
            {
                FieldName = "AnnouncedBy",
                BeforeValue = record.Private.AnnouncedBy ?? "null",
            };
            record.Public.AnnounceOnUTC = request.AnnounceOnUTC;
            record.Private.AnnouncedBy = user.Id.ToString();

            announceDateChanged.AfterValue = record.Public.AnnounceOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            announceByChanged.AfterValue = record.Private.AnnouncedBy ?? "null";

            var fieldChanges = new List<AuditFieldChange>();
            if (announceDateChanged.BeforeValue != announceDateChanged.AfterValue)
                fieldChanges.Add(announceDateChanged);
            if (announceByChanged.BeforeValue != announceByChanged.AfterValue)
                fieldChanges.Add(announceByChanged);

            var auditEntry =
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentChanged,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                };
            auditEntry.Changes.AddRange(fieldChanges);

            await dataProvider.Save(record);

            await auditLogHelper.TryLogEvent(auditEntry);

            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_CREATE_CONTENT)]
        public override async Task<CreateContentResponse> CreateContent(CreateContentRequest request, ServerCallContext context)
        {

            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
            {
                return new CreateContentResponse()
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not Authenticated")
                };
            }

            if (!IsValid(request.Public, request.Private))
            {
                return new CreateContentResponse()
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonValidationFailed, "Invalid Request Body")
                };
            }

            var record = new ContentRecord
            {
                Public = new()
                {
                    ContentID = Guid.NewGuid().ToString(),
                    CreatedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                    Data = request.Public,
                },
                Private = new()
                {
                    CreatedBy = user.Id.ToString(),
                    Data = request.Private,
                },
            };

            await dataProvider.Save(record);
            var createdSnapshot = record.Public.Data?.ToString() ?? "null";
            await auditLogHelper.TryLogEvent(
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentCreated,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                    Changes = { BuildTextChange("Data", "null", createdSnapshot) },
                }
            );

            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_PUBLISH)]
        public override async Task<DeleteContentResponse> DeleteContent(DeleteContentRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var contentId = request.ContentID.ToGuid();
            var record = await dataProvider.GetById(contentId);
            if (record == null)
            {
                return new DeleteContentResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Content Not Found")
                };
            }

            record.Public.DeletedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
            record.Private.DeletedBy = user.Id.ToString();
            var deletedOnAfter = record.Public.DeletedOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var deletedByAfter = record.Private.DeletedBy ?? "null";

            await dataProvider.Save(record);
            await auditLogHelper.TryLogEvent(
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentDeleted,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                    Changes =
                    {
                        new AuditFieldChange { FieldName = "DeletedOnUTC", BeforeValue = "null", AfterValue = deletedOnAfter },
                        new AuditFieldChange { FieldName = "DeletedBy", BeforeValue = "null", AfterValue = deletedByAfter }
                    },
                }
            );
            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        [Authorize(Roles = RoleAbilities.ROLE_IS_ADMIN_OR_OWNER_OR_SERVICE_OR_BOT)]
        public override async Task DumpAllContentAdmin(DumpAllContentAdminRequest request, IServerStreamWriter<ContentRecord> responseStream, ServerCallContext context)
        {
            await foreach (var rec in dataProvider.GetAll())
            {
                if (request.ContentType != ContentType.ContentNone)
                {
                    if (rec.Public.Data.GetContentType() != request.ContentType)
                        continue;
                }

                await responseStream.WriteAsync(rec);
            }
        }

        [AllowAnonymous]
        public override async Task<GetAllContentResponse> GetAllContent(GetAllContentRequest request, ServerCallContext context)
        {
            var possiblyIDs = request.PossibleContentIDs.ToList();
            var searchCatId = request.CategoryId;
            var searchChanId = request.ChannelId;
            var searchAuthorId = request.AuthorId;
            var searchTag = request.Tag;
            var searchLiveOnly = request.OnlyLive;
            var searchPublishedAfterUTC = request.PublishedAfterUTC;
            var searchPublishedBeforeUTC = request.PublishedBeforeUTC;

            if (!possiblyIDs.Any())
                possiblyIDs = null;
            if (string.IsNullOrWhiteSpace(searchCatId))
                searchCatId = null;
            if (string.IsNullOrWhiteSpace(searchChanId))
                searchChanId = null;
            if (string.IsNullOrWhiteSpace(searchAuthorId))
                searchAuthorId = null;
            if (string.IsNullOrWhiteSpace(searchTag))
                searchTag = null;

            var res = new GetAllContentResponse();

            List<ContentListRecord> list = new();
            await foreach (var rec in dataProvider.GetAll())
            {
                if (!CanShowInList(rec, null))
                    continue;

                if (possiblyIDs != null)
                    if (!possiblyIDs.Contains(rec.Public.ContentID))
                        continue;

                if (request.SubscriptionSearch != null)
                {
                    if (rec.Public.Data.SubscriptionLevel < request.SubscriptionSearch.MinimumLevel)
                        continue;
                    if (rec.Public.Data.SubscriptionLevel > request.SubscriptionSearch.MaximumLevel)
                        continue;
                }

                if (searchCatId != null)
                    if (!rec.Public.Data.CategoryIds.Contains(searchCatId))
                        continue;

                if (searchChanId != null)
                    if (!rec.Public.Data.ChannelIds.Contains(searchChanId))
                        continue;

                if (searchAuthorId != null)
                    if (rec.Public.Data.AuthorID != searchAuthorId)
                        continue;

                if (searchTag != null)
                    if (!rec.Public.Data.Tags.Select(t => t.ToLower()).Contains(searchTag.ToLower()))
                        continue;

                if (searchLiveOnly)
                    if (!(rec.Public.Data.Video?.IsLive ?? false))
                        continue;

                if (searchPublishedAfterUTC != null)
                    if (rec.Public.PublishOnUTC < searchPublishedAfterUTC)
                        continue;

                if (searchPublishedBeforeUTC != null)
                    if (rec.Public.PublishOnUTC > searchPublishedBeforeUTC)
                        continue;

                var listRec = rec.Public.ToContentListRecord();

                if (request.ContentType != ContentType.ContentNone)
                {
                    if (listRec.ContentType != request.ContentType)
                        continue;
                }

                list.Add(listRec);
            }

            res.Records.AddRange(list.OrderByDescending(r => r.PublishOnUTC).OrderByDescending(r => r.PinnedOnUTC));
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res.Records.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_CREATE_CONTENT)]
        public override async Task<GetAllContentAdminResponse> GetAllContentAdmin(GetAllContentAdminRequest request, ServerCallContext context)
        {
            var possiblyIDs = request.PossibleContentIDs.ToList();
            var searchCatId = request.CategoryId;
            var searchChanId = request.ChannelId;
            var searchTag = request.Tag;
            var searchLiveOnly = request.OnlyLive;

            if (!possiblyIDs.Any())
                possiblyIDs = null;
            if (string.IsNullOrWhiteSpace(searchCatId))
                searchCatId = null;
            if (string.IsNullOrWhiteSpace(searchChanId))
                searchChanId = null;
            if (string.IsNullOrWhiteSpace(searchTag))
                searchTag = null;

            var res = new GetAllContentAdminResponse();

            List<ContentListRecord> list = new();
            await foreach (var rec in dataProvider.GetAll())
            {
                if (request.Deleted)
                {
                    if (rec.Public.DeletedOnUTC == null)
                        continue;
                }
                else
                {
                    if (rec.Public.DeletedOnUTC != null)
                        continue;
                }

                if (possiblyIDs != null)
                    if (!possiblyIDs.Contains(rec.Public.ContentID))
                        continue;

                if (request.SubscriptionSearch != null)
                {
                    if (rec.Public.Data.SubscriptionLevel < request.SubscriptionSearch.MinimumLevel)
                        continue;
                    if (rec.Public.Data.SubscriptionLevel > request.SubscriptionSearch.MaximumLevel)
                        continue;
                }

                if (searchCatId != null)
                    if (!rec.Public.Data.CategoryIds.Contains(searchCatId))
                        continue;

                if (searchChanId != null)
                    if (!rec.Public.Data.ChannelIds.Contains(searchChanId))
                        continue;

                if (searchTag != null)
                    if (!rec.Public.Data.Tags.Select(t => t.ToLower()).Contains(searchTag.ToLower()))
                        continue;

                if (searchLiveOnly)
                    if (!(rec.Public.Data.Video?.IsLive ?? false))
                        continue;

                var listRec = rec.Public.ToContentListRecord();

                if (request.ContentType != ContentType.ContentNone)
                {
                    if (listRec.ContentType != request.ContentType)
                        continue;
                }

                list.Add(listRec);
            }

            res.Records.AddRange(list.OrderByDescending(r => r.CreatedOnUTC));
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res.Records.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        [AllowAnonymous]
        public override async Task<GetContentResponse> GetContent(GetContentRequest request, ServerCallContext context)
        {
            try
            {
                var user = ONUserHelper.ParseUser(context.GetHttpContext());

                Guid contentId = request.ContentID.ToGuid();
                if (contentId == Guid.Empty)
                    return new GetContentResponse();

                var rec = await dataProvider.GetById(contentId);
                if (rec == null)
                    return new();

                if (!CanShowInList(rec, user))
                    return new();

                if (!CanShowContent(rec, user))
                    ClearPublicData(rec.Public.Data);

                await statsClient.RecordView(contentId, user);

                return new() { Record = rec.Public };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error trying to GetContent");
            }

            return new();
        }

        [AllowAnonymous]
        public override async Task<GetContentByUrlResponse> GetContentByUrl(GetContentByUrlRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            string contentUrl = request.ContentUrl;
            if (string.IsNullOrWhiteSpace(contentUrl))
                return new();

            var rec = await dataProvider.GetByURL(contentUrl);
            if (rec == null)
                return new();

            if (!CanShowInList(rec, user))
                return new();

            if (!CanShowContent(rec, user))
                ClearPublicData(rec.Public.Data);

            return new() { Record = rec.Public };
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_CREATE_CONTENT)]
        public override async Task<GetContentAdminResponse> GetContentAdmin(GetContentAdminRequest request, ServerCallContext context)
        {
            Guid contentId = request.ContentID.ToGuid();
            if (contentId == Guid.Empty)
                return new();

            var rec = await dataProvider.GetById(contentId);
            if (rec == null)
                return new();

            return new() { Record = rec };
        }

        [AllowAnonymous]
        public override async Task<GetRecentCategoriesResponse> GetRecentCategories(GetRecentCategoriesRequest request, ServerCallContext context)
        {
            int num = Math.Min((int)request.NumCategories, 100);

            List<string> allCategories = new();
            await foreach (var rec in dataProvider.GetAll().Where(r => r.Public.PublishOnUTC != null).OrderByDescending(r => r.Public.PublishOnUTC))
            {
                allCategories.AddRange(rec.Public.Data.CategoryIds.Where(c => !allCategories.Contains(c)));
                if (allCategories.Count > num)
                    break;
            }

            var res = new GetRecentCategoriesResponse();
            res.CategoryIds.AddRange(allCategories.Take(num));
            return res;
        }

        [AllowAnonymous]
        public override async Task<GetRecentTagsResponse> GetRecentTags(GetRecentTagsRequest request, ServerCallContext context)
        {
            int num = Math.Min((int)request.NumTags, 100);

            List<string> allTags = new();
            await foreach (var rec in dataProvider.GetAll().Where(r => r.Public.PublishOnUTC != null).OrderByDescending(r => r.Public.PublishOnUTC))
            {
                allTags.AddRange(rec.Public.Data.Tags.Where(t => !allTags.Contains(t)));
                if (allTags.Count > num)
                    break;
            }

            var res = new GetRecentTagsResponse();
            res.Tags.AddRange(allTags.Take(num));
            return res;
        }

        [AllowAnonymous]
        public override async Task<GetRelatedContentResponse> GetRelatedContent(GetRelatedContentRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            Guid contentId = request.ContentID.ToGuid();
            if (contentId == Guid.Empty)
                return new();

            var curRec = await dataProvider.GetById(contentId);
            if (curRec == null)
                return new();

            if (!CanShowInList(curRec, user))
                return new();

            var res = new GetRelatedContentResponse();

            List<ContentListRecord> list = new();
            await foreach (var rec in dataProvider.GetAll())
            {
                if (!CanShowInList(rec, null))
                    continue;

                if (rec.Public.ContentID == request.ContentID)
                    continue;

                var listRec = rec.Public.ToContentListRecord();

                if (curRec.Public.Data.GetContentType() != ContentType.ContentNone)
                {
                    if (listRec.ContentType != curRec.Public.Data.GetContentType())
                        continue;
                }

                list.Add(listRec);
            }

            res.Records.AddRange(list.OrderByDescending(r => r.PublishOnUTC));
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res.Records.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_CREATE_CONTENT)]
        public override async Task<ModifyContentResponse> ModifyContent(ModifyContentRequest request, ServerCallContext context)
        {
            if (!IsValid(request.Public, request.Private))
            {
                return new ModifyContentResponse()
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonValidationFailed, "Invalid Body")
                };
            }


            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var contentId = request.ContentID.ToGuid();
            var record = await dataProvider.GetById(contentId);
            if (record == null)
                return new ModifyContentResponse()
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Content Not Found")
                };
            var beforePublicData = record.Public.Data?.ToString() ?? "null";
            var beforePrivateData = record.Private.Data?.ToString() ?? "null";
            record.Public.Data = request.Public;
            record.Private.Data = request.Private;
            record.Public.ModifiedOnUTC = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
            record.Private.ModifiedBy = user.Id.ToString();
            var afterPublicData = record.Public.Data?.ToString() ?? "null";
            var afterPrivateData = record.Private.Data?.ToString() ?? "null";

            await dataProvider.Save(record);
            await auditLogHelper.TryLogEvent(
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentChanged,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                    Changes =
                    {
                        BuildTextChange(
                            "RecordData",
                            $"Public:{Environment.NewLine}{beforePublicData}{Environment.NewLine}Private:{Environment.NewLine}{beforePrivateData}",
                            $"Public:{Environment.NewLine}{afterPublicData}{Environment.NewLine}Private:{Environment.NewLine}{afterPrivateData}"
                        )
                    },
                }
            );
            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_PUBLISH)]
        public override async Task<PublishContentResponse> PublishContent(PublishContentRequest request, ServerCallContext context)
        {
            if (request.PublishOnUTC == null)
            {
                var error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonValidationFailed, "Invalid Body");
                error.AddValidationIssue("PublishOnUTC", "PublishOnUTC is required", "required");
                return new()
                {
                    Error = error,
                };
            }
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var contentId = request.ContentID.ToGuid();
            var record = await dataProvider.GetById(contentId);
            if (record == null)
                return new PublishContentResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Content Not Found")
                };
            var publishOnBefore = record.Public.PublishOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var publishedByBefore = record.Private.PublishedBy ?? "null";

            record.Public.PublishOnUTC = request.PublishOnUTC;
            record.Private.PublishedBy = user.Id.ToString();
            var publishOnAfter = record.Public.PublishOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var publishedByAfter = record.Private.PublishedBy ?? "null";

            await dataProvider.Save(record);
            await auditLogHelper.TryLogEvent(
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentPublished,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                    Changes =
                    {
                        new AuditFieldChange { FieldName = "PublishOnUTC", BeforeValue = publishOnBefore, AfterValue = publishOnAfter },
                        new AuditFieldChange { FieldName = "PublishedBy", BeforeValue = publishedByBefore, AfterValue = publishedByAfter }
                    },
                }
            );
            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        [AllowAnonymous]
        public override async Task<SearchContentResponse> SearchContent(SearchContentRequest request, ServerCallContext context)
        {
            var searchQueryBits = Array.Empty<string>();
            var searchCatId = request.CategoryId;
            var searchChanId = request.ChannelId;
            var searchTag = request.Tag;
            var searchLiveOnly = request.OnlyLive;

            if (!string.IsNullOrWhiteSpace(request.Query))
                searchQueryBits = request.Query.ToLower().Replace("\"", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray();

            if (string.IsNullOrWhiteSpace(searchCatId))
                searchCatId = null;
            if (string.IsNullOrWhiteSpace(searchChanId))
                searchChanId = null;
            if (string.IsNullOrWhiteSpace(searchTag))
                searchTag = null;

            var res = new SearchContentResponse();

            List<ContentListRecord> list = new();
            await foreach (var rec in dataProvider.GetAll())
            {
                if (!CanShowInList(rec, null))
                    continue;

                if (request.SubscriptionSearch != null)
                {
                    if (rec.Public.Data.SubscriptionLevel < request.SubscriptionSearch.MinimumLevel)
                        continue;
                    if (rec.Public.Data.SubscriptionLevel > request.SubscriptionSearch.MaximumLevel)
                        continue;
                }

                if (searchCatId != null)
                    if (!rec.Public.Data.CategoryIds.Contains(searchCatId))
                        continue;

                if (searchChanId != null)
                    if (!rec.Public.Data.ChannelIds.Contains(searchChanId))
                        continue;

                if (searchTag != null)
                    if (!rec.Public.Data.Tags.Select(t => t.ToLower()).Contains(searchTag.ToLower()))
                        continue;

                if (searchLiveOnly)
                    if (!(rec.Public.Data.Video?.IsLive ?? false))
                        continue;

                var listRec = rec.Public.ToContentListRecord();

                if (request.ContentType != ContentType.ContentNone)
                {
                    if (listRec.ContentType != request.ContentType)
                        continue;
                }

                if (searchQueryBits.Length > 0)
                {
                    if (!MeetsQuery(searchQueryBits, request.Query, rec))
                        continue;
                }

                list.Add(listRec);
            }

            res.Records.AddRange(list.OrderByDescending(r => r.PublishOnUTC));
            res.PageTotalItems = (uint)res.Records.Count;

            if (request.PageSize > 0)
            {
                res.PageOffsetStart = request.PageOffset;

                var page = res.Records.Skip((int)request.PageOffset).Take((int)request.PageSize).ToList();
                res.Records.Clear();
                res.Records.AddRange(page);
            }

            res.PageOffsetEnd = res.PageOffsetStart + (uint)res.Records.Count;

            return res;
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_PUBLISH)]
        public override async Task<UnannounceContentResponse> UnannounceContent(UnannounceContentRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var contentId = request.ContentID.ToGuid();
            var record = await dataProvider.GetById(contentId);
            if (record == null)
            {
                return new UnannounceContentResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Content Not Found")
                };
            }
            var announceOnBefore = record.Public.AnnounceOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var announcedByBefore = record.Private.AnnouncedBy ?? "null";

            record.Public.AnnounceOnUTC = null;
            record.Private.AnnouncedBy = user.Id.ToString();
            var announceOnAfter = record.Public.AnnounceOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var announcedByAfter = record.Private.AnnouncedBy ?? "null";

            await dataProvider.Save(record);
            await auditLogHelper.TryLogEvent(
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentChanged,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                    Changes =
                    {
                        new AuditFieldChange { FieldName = "AnnounceOnUTC", BeforeValue = announceOnBefore, AfterValue = announceOnAfter },
                        new AuditFieldChange { FieldName = "AnnouncedBy", BeforeValue = announcedByBefore, AfterValue = announcedByAfter }
                    },
                }
            );
            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_PUBLISH)]
        public override async Task<UndeleteContentResponse> UndeleteContent(UndeleteContentRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var contentId = request.ContentID.ToGuid();
            var record = await dataProvider.GetById(contentId);
            if (record == null)
            {
                return new UndeleteContentResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Content Not Found")
                };
            }
            var deletedOnBefore = record.Public.DeletedOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var deletedByBefore = record.Private.DeletedBy ?? "null";

            record.Public.DeletedOnUTC = null;
            record.Private.DeletedBy = user.Id.ToString();
            var deletedOnAfter = record.Public.DeletedOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var deletedByAfter = record.Private.DeletedBy ?? "null";

            await dataProvider.Save(record);
            await auditLogHelper.TryLogEvent(
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentChanged,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                    Changes =
                    {
                        new AuditFieldChange { FieldName = "DeletedOnUTC", BeforeValue = deletedOnBefore, AfterValue = deletedOnAfter },
                        new AuditFieldChange { FieldName = "DeletedBy", BeforeValue = deletedByBefore, AfterValue = deletedByAfter }
                    },
                }
            );
            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        [Authorize(Roles = RoleAbilities.ROLE_CAN_PUBLISH)]
        public override async Task<UnpublishContentResponse> UnpublishContent(UnpublishContentRequest request, ServerCallContext context)
        {
            var user = ONUserHelper.ParseUser(context.GetHttpContext());

            var contentId = request.ContentID.ToGuid();
            var record = await dataProvider.GetById(contentId);
            if (record == null)
            {
                return new UnpublishContentResponse
                {
                    Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Content Not Found")
                };
            }
            var publishOnBefore = record.Public.PublishOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var publishedByBefore = record.Private.PublishedBy ?? "null";

            record.Public.PublishOnUTC = null;
            record.Private.PublishedBy = user.Id.ToString();
            var publishOnAfter = record.Public.PublishOnUTC?.ToDateTimeOffset().ToString("o") ?? "null";
            var publishedByAfter = record.Private.PublishedBy ?? "null";

            await dataProvider.Save(record);
            await auditLogHelper.TryLogEvent(
                new AuditLogEntry
                {
                    ContextName = "Content",
                    Action = ActionType.ActionContentChanged,
                    Actor = user.ToAuditActor(),
                    Targets = { new AuditTarget { TargetID = record.Public.ContentID, Type = TargetType.TargetContent } },
                    Changes =
                    {
                        new AuditFieldChange { FieldName = "PublishOnUTC", BeforeValue = publishOnBefore, AfterValue = publishOnAfter },
                        new AuditFieldChange { FieldName = "PublishedBy", BeforeValue = publishedByBefore, AfterValue = publishedByAfter }
                    },
                }
            );
            return new() { Record = record, Error = GenericErrorExtensions.CreateNoError() };
        }

        private static AuditFieldChange BuildTextChange(string fieldName, string before, string after)
        {
            return new AuditFieldChange
            {
                FieldName = fieldName,
                BeforeValue = before ?? "null",
                AfterValue = after ?? "null",
            };
        }

        private bool CanShowContent(ContentRecord rec, ONUser user)
        {
            if (user?.RoleAbilities.IsWriterOrHigher ?? false)
                return true;

            if (!CanShowInList(rec, user))
                return false;

            var recLevel = rec.Public.Data.SubscriptionLevel;
            if (recLevel > (user?.SubscriptionLevel ?? 0))
                return false;

            return true;
        }

        private bool CanShowInList(ContentRecord rec, ONUser user)
        {
            if (rec.Public.DeletedOnUTC != null)
                return false;

            if (user?.RoleAbilities.CanCreateContent ?? false)
                return true;

            if (rec.Public.PublishOnUTC == null || rec.Public.PublishOnUTC > Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow))
                return false;

            return true;
        }

        private void ClearPublicData(ContentPublicData data)
        {
            switch (data.ContentDataOneofCase)
            {
                case ContentPublicData.ContentDataOneofOneofCase.Audio:
                    data.Audio.AudioAssetID = "";
                    data.Audio.HtmlBody = "";
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Picture:
                    data.Picture.HtmlBody = "";
                    data.Picture.ImageAssetIDs.Clear();
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Video:
                    data.Video.HtmlBody = "";
                    data.Video.RumbleVideoId = "";
                    data.Video.YoutubeVideoId = "";
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Written:
                    data.Written.HtmlBody = "";
                    break;
            }
        }

        private bool IsValid(ContentPublicData pubData, ContentPrivateData privData)
        {
            if (pubData == null)
                return false;
            if (privData == null)
                return false;
            if (string.IsNullOrWhiteSpace(pubData.Title))
                return false;

            switch (pubData.ContentDataOneofCase)
            {
                case ContentPublicData.ContentDataOneofOneofCase.Audio:
                    if (!IsValid(pubData.Audio, privData.Audio))
                        return false;
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Picture:
                    if (!IsValid(pubData.Picture, privData.Picture))
                        return false;
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Video:
                    if (!IsValid(pubData.Video, privData.Video))
                        return false;
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Written:
                    if (!IsValid(pubData.Written, privData.Written))
                        return false;
                    break;
                default:
                    return false;
            }

            return true;
        }

        private bool IsValid(AudioContentPublicData pubData, AudioContentPrivateData privData)
        {
            if (privData == null)
                return false;

            if (!IsValidAssetId(pubData.AudioAssetID))
                return false;

            return true;
        }

        private bool IsValid(PictureContentPublicData pubData, PictureContentPrivateData privData)
        {
            if (privData == null)
                return false;

            if (pubData.ImageAssetIDs == null)
                return false;

            foreach (var id in pubData.ImageAssetIDs)
                if (!IsValidAssetId(id))
                    return false;

            return true;
        }

        private bool IsValid(VideoContentPublicData pubData, VideoContentPrivateData privData)
        {
            if (privData == null)
                return false;

            if (string.IsNullOrWhiteSpace(pubData.RumbleVideoId) && string.IsNullOrWhiteSpace(pubData.YoutubeVideoId))
                return false;

            return true;
        }

        private bool IsValid(WrittenContentPublicData pubData, WrittenContentPrivateData privData)
        {
            if (privData == null)
                return false;

            return true;
        }

        public bool IsValidAssetId(string idStr)
        {
            if (string.IsNullOrWhiteSpace(idStr))
                return false;

            return true;
        }

        private bool MeetsQuery(string[] searchQueryBits, string searchQuery, ContentRecord rec)
        {
            if (MeetsQuery(searchQueryBits, rec.Public.Data.Title.ToLower()))
                return true;

            if (MeetsQuery(searchQueryBits, rec.Public.Data.Description.ToLower()))
                return true;

            switch (rec.Public.Data.ContentDataOneofCase)
            {
                case ContentPublicData.ContentDataOneofOneofCase.Audio:
                    if (MeetsQuery(searchQueryBits, rec.Public.Data.Audio.HtmlBody.ToLower()))
                        return true;
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Picture:
                    if (MeetsQuery(searchQueryBits, rec.Public.Data.Picture.HtmlBody.ToLower()))
                        return true;
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Written:
                    if (MeetsQuery(searchQueryBits, rec.Public.Data.Written.HtmlBody.ToLower()))
                        return true;
                    break;
                case ContentPublicData.ContentDataOneofOneofCase.Video:
                    if (MeetsQuery(searchQueryBits, rec.Public.Data.Video.HtmlBody.ToLower()))
                        return true;
                    if (rec.Public.Data.Video.YoutubeVideoId == searchQuery)
                        return true;
                    if (rec.Public.Data.Video.RumbleVideoId == searchQuery)
                        return true;
                    break;
                default:
                    break;
            }

            return false;
        }

        private bool MeetsQuery(string[] searchQueryBits, string haystack)
        {
            foreach (string bit in searchQueryBits)
                if (haystack.Contains(bit))
                    return true;
            return false;
        }
    }
}
