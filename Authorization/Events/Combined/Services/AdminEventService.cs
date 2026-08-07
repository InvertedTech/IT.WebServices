using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IT.WebServices.Authentication;
using IT.WebServices.AuditLog;
using IT.WebServices.Authorization.Events.Generic;
using IT.WebServices.Authorization.Events.Generic.Data;
using IT.WebServices.Fragments;
using IT.WebServices.Fragments.AuditLog;
using IT.WebServices.Fragments.Authorization.Events;
using IT.WebServices.Fragments.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IT.WebServices.Authorization.Events.Combined.Services
{
    [Authorize(Roles = RoleAbilities.ROLE_IS_EVENT_MANAGER_OR_HIGHER)]
    public class AdminEventService : AdminEventInterface.AdminEventInterfaceBase
    {
        public readonly ILogger Log;
        private readonly IGenericEventRecordProvider eventProvider;
        private readonly IAuditLogService auditLogHelper;
        private readonly GenericEventProviderProvider genericEventProviderProvider;

        public AdminEventService(ILogger<AdminEventService> log, IGenericEventRecordProvider eventProvider, IAuditLogService auditLogHelper, GenericEventProviderProvider genericEventProviderProvider)
        {
            Log = log;
            this.eventProvider = eventProvider;
            this.auditLogHelper = auditLogHelper;
            this.genericEventProviderProvider = genericEventProviderProvider;
        }

        public override async Task<AdminCreateEventResponse> AdminCreateEvent(AdminCreateEventRequest request, ServerCallContext context)
        {
            if (request.Data == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Request Data Is Null") };

            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not Logged In") };

            var now = Timestamp.FromDateTime(DateTime.UtcNow);

            var record = new GenericEventRecord
            {
                EventID = Guid.NewGuid().ToString(),
                Title = request.Data.Title,
                Description = request.Data.Description,
                Location = request.Data.Location,
                VenueID = request.Data.VenueID,
                MaxTickets = request.Data.MaxTickets,
                InternalNotes = request.Data.InternalNotes,
                CreatedOnUTC = now,
                ModifiedOnUTC = now,
                CreatedByID = user.Id.ToString(),
                ModifiedByID = user.Id.ToString(),
            };

            if (request.Data.StartOnUTC != null)
                record.StartOnUTC = request.Data.StartOnUTC;
            if (request.Data.EndOnUTC != null)
                record.EndOnUTC = request.Data.EndOnUTC;
            if (request.Data.Recurrence != null)
                record.Recurrence = request.Data.Recurrence;

            record.Tags.AddRange(request.Data.Tags);
            record.TicketClasses.AddRange(request.Data.TicketClasses);
            record.ExtraMetadata.Add(request.Data.ExtraMetadata);

            if (request.Data.SyncToEventbrite)
            {
                var eventbriteProvider = genericEventProviderProvider.AllProviders.FirstOrDefault(p => p.ProcessorName == "eventbrite");
                if (eventbriteProvider == null || !eventbriteProvider.IsEnabled)
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonProviderUnavailable, "Eventbrite Is Not Enabled") };

                try
                {
                    var res = await eventbriteProvider.CreateEvent(record, context.CancellationToken);
                    record.ProcessorName = eventbriteProvider.ProcessorName;
                    record.ProcessorEventID = res.ProcessorEventID;
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "Failed to sync new event to Eventbrite");
                    return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonProviderError, $"Failed To Sync To Eventbrite: {ex.Message}") };
                }
            }

            await eventProvider.Save(record);

            await auditLogHelper.TryLogEvent(new AuditLogEntry()
            {
                Action = ActionType.ActionEventCreated,
                Summary = $"Event '{record.Title}' Created",
                Actor = user.ToAuditActor(),
                Metadata =
                {
                    { "EventID", record.EventID },
                },
            });

            return new() { Record = record };
        }

        public override async Task<AdminCancelEventResponse> AdminCancelEvent(AdminCancelEventRequest request, ServerCallContext context)
        {
            var eventId = request.EventID.ToGuid();
            if (eventId == Guid.Empty)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Invalid EventID") };

            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not Logged In") };

            var record = await eventProvider.GetById(eventId);
            if (record == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Event Not Found") };

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            record.IsCanceled = true;
            record.CanceledForReason = request.CancellationReason;
            record.CanceledByID = user.Id.ToString();
            record.CanceledOnUTC = now;
            record.ModifiedByID = user.Id.ToString();
            record.ModifiedOnUTC = now;

            await eventProvider.Save(record);

            await auditLogHelper.TryLogEvent(new AuditLogEntry()
            {
                Action = ActionType.ActionEventCanceled,
                Summary = $"Event '{record.Title}' Canceled",
                Actor = user.ToAuditActor(),
                Metadata =
                {
                    { "EventID", record.EventID },
                    { "Reason", request.CancellationReason },
                },
            });

            return new() { Error = null };
        }

        public override async Task<AdminEditEventResponse> AdminEditEvent(AdminEditEventRequest request, ServerCallContext context)
        {
            var eventId = request.EventID.ToGuid();
            if (eventId == Guid.Empty)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidRequest, "Invalid EventID") };

            if (request.Data == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonInvalidContent, "Request Data Is Null") };

            var user = ONUserHelper.ParseUser(context.GetHttpContext());
            if (user == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonUnauthenticated, "Not Logged In") };

            var record = await eventProvider.GetById(eventId);
            if (record == null)
                return new() { Error = GenericErrorExtensions.CreateError(APIErrorReason.ErrorReasonNotFound, "Event Not Found") };

            record.Title = request.Data.Title;
            record.Description = request.Data.Description;
            record.Location = request.Data.Location;
            record.VenueID = request.Data.VenueID;
            record.MaxTickets = request.Data.MaxTickets;
            record.InternalNotes = request.Data.InternalNotes;

            if (request.Data.StartOnUTC != null)
                record.StartOnUTC = request.Data.StartOnUTC;
            if (request.Data.EndOnUTC != null)
                record.EndOnUTC = request.Data.EndOnUTC;
            if (request.Data.Recurrence != null)
                record.Recurrence = request.Data.Recurrence;

            record.Tags.Clear();
            record.Tags.AddRange(request.Data.Tags);

            record.TicketClasses.Clear();
            record.TicketClasses.AddRange(request.Data.TicketClasses);

            record.ExtraMetadata.Clear();
            record.ExtraMetadata.Add(request.Data.ExtraMetadata);

            record.ModifiedByID = user.Id.ToString();
            record.ModifiedOnUTC = Timestamp.FromDateTime(DateTime.UtcNow);

            await eventProvider.Save(record);

            await auditLogHelper.TryLogEvent(new AuditLogEntry()
            {
                Action = ActionType.ActionEventChanged,
                Summary = $"Event '{record.Title}' Edited",
                Actor = user.ToAuditActor(),
                Metadata =
                {
                    { "EventID", record.EventID },
                },
            });

            return new() { Record = record };
        }
    }
}
