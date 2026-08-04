using IT.WebServices.Fragments.Authorization.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IT.WebServices.Authorization.Events.Generic
{
    public class GenericEventProviderProvider
    {
        private readonly List<IGenericEventProvider> genericProviders;

        public GenericEventProviderProvider(IEnumerable<IGenericEventProvider> genericProviders)
        {
            this.genericProviders = genericProviders.ToList();
        }

        public IGenericEventProvider[] AllProviders => genericProviders.ToArray();

        public IGenericEventProvider[] AllEnabledProviders => genericProviders.Where(p => p.IsEnabled).ToArray();

        public IGenericEventProvider GetProcessor(GenericEventRecord record)
        {
            var provider = genericProviders.FirstOrDefault(p => p.ProcessorName == record.ProcessorName);
            if (provider == null)
                throw new NotImplementedException($"GenericEventProvider '{record.ProcessorName}' not found");

            return provider;
        }
    }
}
