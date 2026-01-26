using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Authorization.Events
{
    public sealed partial class EventRecord : pb::IMessage<EventRecord>
    {
        public EventRecord(AdminCreateRecurringEventRequest request, string userId, string recurrenceHash)
        {
            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var id = Guid.NewGuid();
            EventId = id.ToString();
            OneOfType = EventRecordOneOfType.EventOneOfRecurring;
            RecurringPublic = new RecurringEventPublicRecord
            {
                EventId = id.ToString(), 
                Title = request.Data.Title,
                Description = request.Data.Description,
                // Location = request.Data.Venue?.Name ?? "",
                TemplateStartOnUTC = request.Data.StartTimeUTC,
                TemplateEndOnUTC = request.Data.EndTimeUTC,
                Tags = { request.Data.Tags },
                TicketClasses = { request.Data.TicketClasses },
                Recurrence = request.RecurrenceRule,
                RecurrenceHash = recurrenceHash,
                CreatedOnUTC = now,
                ModifiedOnUTC = now,
                Venue = request.Data.Venue,
            };
            RecurringPrivate = new RecurringEventPrivateRecord
            {
                CreatedById = userId.ToString(),
                ModifiedById = userId.ToString(),
                ExtraMetadata = { request.Data.ExtraData },
            };
        }

        public EventPublicRecord GetPublicRecord()
        {
            switch (EventPublicRecordOneOfCase)
            {
                case EventPublicRecordOneOfOneofCase.SinglePublic:
                    return new EventPublicRecord()
                    {
                        SinglePublic = SinglePublic,
                    };
                case EventPublicRecordOneOfOneofCase.RecurringPublic:
                    return new EventPublicRecord()
                    {
                        RecurringPublic = RecurringPublic,
                    };
                default:
                    return null;
            }
        }
    }
}
