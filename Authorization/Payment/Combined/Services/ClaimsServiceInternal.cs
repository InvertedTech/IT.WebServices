using IT.WebServices.Authentication;
using IT.WebServices.Authorization.Payment.Generic.Data;
using IT.WebServices.Fragments.Authorization;
using IT.WebServices.Fragments.Authorization.Payment;
using IT.WebServices.Fragments.Authorization.Payment.Manual;
using IT.WebServices.Helpers;
using Microsoft.Extensions.Logging;
using ManualD = IT.WebServices.Authorization.Payment.Manual.Data;

namespace IT.WebServices.Authorization.Payment.Combined.Services
{
    public class ClaimsServiceInternal : IClaimsProvider
    {
        private readonly ILogger<ClaimsService> logger;
        private readonly IGenericSubscriptionFullRecordProvider baseProvider;
        private readonly ManualD.ISubscriptionRecordProvider manualProvider;

        public ClaimsServiceInternal(ILogger<ClaimsService> logger, IGenericSubscriptionFullRecordProvider baseProvider, ManualD.ISubscriptionRecordProvider manualProvider)
        {
            this.logger = logger;
            this.baseProvider = baseProvider;
            this.manualProvider = manualProvider;
        }

        public async Task<ClaimRecord[]> GetOtherClaims(Guid userId)
        {
            var bestRecord = await GetBestSubscription(userId);

            if (bestRecord == null || bestRecord.AmountCents < 1)
                return Array.Empty<ClaimRecord>();

            if (bestRecord.PaidThruUTC.ToDateTime() < DateTime.UtcNow)
                return Array.Empty<ClaimRecord>();

            return bestRecord.ToClaimRecords();
        }

        private async Task<UnifiedSubscriptionRecord?> GetBestSubscription(Guid userId)
        {
            var manualRecs = await manualProvider.GetAllByUserId(userId).ToList();
            var baseRecs = await baseProvider.GetAllByUserId(userId).ToList();

            var recs = new List<UnifiedSubscriptionRecord>();
            recs.AddRange(baseRecs.Select(r => new UnifiedSubscriptionRecord(r)));
            recs.AddRange(manualRecs.Where(r => r.CanceledOnUTC == null).Select(r => new UnifiedSubscriptionRecord(r)));

            return recs.Where(r => r.PaidThruUTC?.ToDateTime() > DateTime.UtcNow).OrderByDescending(r => r.PaidThruUTC).OrderByDescending(r => r.AmountCents).FirstOrDefault();
        }

        public class UnifiedSubscriptionRecord
        {
            public UnifiedSubscriptionRecord(ManualSubscriptionRecord r)
            {
                PaidThruUTC = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow.AddMonths(1));
                AmountCents = r.AmountCents;
                Service = "manual";
            }

            public UnifiedSubscriptionRecord(GenericSubscriptionFullRecord r)
            {
                PaidThruUTC = r.PaidThruUTC;
                AmountCents = r.SubscriptionRecord.AmountCents;
                Service = r.ProcessorName;
            }

            public Google.Protobuf.WellKnownTypes.Timestamp PaidThruUTC { get; set; }
            public uint AmountCents { get; set; }
            public string Service { get; set; }

            public ClaimRecord[] ToClaimRecords()
            {
                return
                [
                    new ClaimRecord()
                    {
                        Name = ONUser.SubscriptionLevelType,
                        Value = AmountCents.ToString(),
                        ExpiresOnUTC = PaidThruUTC
                    },
                    new ClaimRecord()
                    {
                        Name = ONUser.SubscriptionProviderType,
                        Value = Service,
                        ExpiresOnUTC = PaidThruUTC
                    }
                ];
            }
        }
    }
}