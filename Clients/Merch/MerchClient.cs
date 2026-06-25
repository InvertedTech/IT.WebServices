using IT.WebServices.Authentication;
using IT.WebServices.Fragments.Merch;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IT.WebServices.Clients.Merch
{
    public class MerchClient
    {
        private readonly MerchInterface.MerchInterfaceClient client;
        private readonly ONUserHelper userHelper;
        private readonly ILogger log;

        public MerchClient(MerchInterface.MerchInterfaceClient client, ONUserHelper userHelper, ILogger<MerchClient> log)
        {
            this.client = client;
            this.userHelper = userHelper;
            this.log = log;
        }

        public async Task<GenericMerchRecord?> GetMerch(GetMerchRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.GetMerchAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res.Record;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in GetMerch");
                return null;
            }
        }

        public async Task<IEnumerable<GenericMerchRecord>?> SearchMerch(SearchMerchRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var res = await client.SearchMerchAsync(request, userHelper.GetGrpcCallOptions(cancellationToken));

                return res.Records;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error in SearchMerch");
                return null;
            }
        }
    }
}
