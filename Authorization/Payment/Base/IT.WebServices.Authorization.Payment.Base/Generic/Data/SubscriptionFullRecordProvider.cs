using IT.WebServices.Fragments.Generic;
using IT.WebServices.Helpers;
using IT.WebServices.Fragments.Authorization.Payment;

namespace IT.WebServices.Authorization.Payment.Generic.Data
{
    public class SubscriptionFullRecordProvider : IGenericSubscriptionFullRecordProvider
    {
        private readonly IGenericPaymentRecordProvider paymentProvider;
        private readonly IGenericSubscriptionRecordProvider subProvider;

        public SubscriptionFullRecordProvider(IGenericPaymentRecordProvider paymentProvider, IGenericSubscriptionRecordProvider subProvider)
        {
            this.paymentProvider = paymentProvider;
            this.subProvider = subProvider;
        }

        public Task Delete(Guid userId, Guid subId)
        {
            return Task.WhenAll(
                    subProvider.Delete(userId, subId),
                    paymentProvider.DeleteAll(userId, subId)
                );
        }

        public async IAsyncEnumerable<GenericSubscriptionFullRecord> GetAll()
        {
            await foreach (var sub in subProvider.GetAll())
            {
                var full = new GenericSubscriptionFullRecord()
                {
                    SubscriptionRecord = sub
                };

                await Hydrate(full);

                yield return full;
            }
        }

        public async IAsyncEnumerable<GenericSubscriptionFullRecord> GetAllByUserId(Guid userId)
        {
            await foreach (var sub in subProvider.GetAllByUserId(userId))
            {
                var full = new GenericSubscriptionFullRecord()
                {
                    SubscriptionRecord = sub
                };

                await Hydrate(full);

                yield return full;
            }
        }

        public async Task<GenericSubscriptionFullRecord?> GetBySubscriptionId(Guid userId, Guid subId)
        {
            var sub = await subProvider.GetById(userId, subId);
            if (sub == null)
                return null;

            var full = new GenericSubscriptionFullRecord()
            {
                SubscriptionRecord = sub
            };

            await Hydrate(full);

            return full;
        }

        public async Task Save(GenericSubscriptionFullRecord full)
        {
            if (full.SubscriptionRecord == null)
                return;

            await paymentProvider.DeleteAll(full.SubscriptionRecord.UserID.ToGuid(), full.SubscriptionRecord.InternalSubscriptionID.ToGuid());

            var tasks = new List<Task> { subProvider.Save(full.SubscriptionRecord) };

            foreach (var p in full.Payments)
                tasks.Add(paymentProvider.Save(p));

            await Task.WhenAll(tasks);
        }

        private async Task Hydrate(GenericSubscriptionFullRecord full)
        {
            var sub = full.SubscriptionRecord;

            full.Payments.AddRange(await paymentProvider.GetAllBySubscriptionId(sub.UserID.ToGuid(), sub.InternalSubscriptionID.ToGuid()).ToList());

            full.CalculateRecords();
        }
    }
}
