using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using IT.WebServices.Fragments.Generic;
using pb = global::Google.Protobuf;

namespace IT.WebServices.Fragments.Authorization.Events
{
    public sealed partial class EventTicketClass : pb::IMessage<EventTicketClass>
    {
        public bool HasRequestedAmount(int numToReserve)
        {
            var amountAvailable = (int)Public.AmountAvailable;
            var maxPerUser = (int)Public.MaxTicketsPerUser;
            return numToReserve > 0 && numToReserve <= amountAvailable;
        }

        public bool HitReservationLimit(int numToReserve, int numReservedAlready = 0)
        {
            var maxPerUser = (int)Public.MaxTicketsPerUser;
            return numToReserve > maxPerUser || numToReserve <= numReservedAlready;
        }

        public bool IsOnSale()
        {
            return SaleStartOnUTC <= Timestamp.FromDateTime(DateTime.UtcNow)
                && SaleEndOnUTC >= Timestamp.FromDateTime(DateTime.UtcNow);
        }
    }
}
