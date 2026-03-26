using IT.WebServices.Merch.Generic.Data;
using IT.WebServices.Helpers;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DIExtensions
    {
        public static IServiceCollection AddMerchBaseClasses(this IServiceCollection services)
        {
            services.AddSingleton<MySQLHelper>();

            services.AddSingleton<FileSystemGenericMerchRecordProvider>();
            services.AddSingleton<IGenericMerchRecordProvider, MemCachedFileSystemGenericMerchRecordProvider>();

            return services;
        }
    }
}
