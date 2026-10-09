using IT.WebServices.Fragments.Settings;
using IT.WebServices.Settings;
using System.Threading.Tasks;

namespace IT.WebServices.Mcp
{
    /// <summary>
    /// Reads the global MCP switches from the settings record.
    /// A missing MCP record (older deployments) means everything is off.
    /// </summary>
    public class McpSettings
    {
        private readonly ISettingsService settingsService;

        public McpSettings(ISettingsService settingsService)
        {
            this.settingsService = settingsService;
        }

        public async Task<McpPrivateRecord> GetAsync()
        {
            var data = await settingsService.GetAdminDataInternal();
            return data?.Private?.MCP ?? new McpPrivateRecord();
        }

        public async Task<bool> IsEnabledAsync() => (await GetAsync()).Enabled;

        public async Task<bool> CanReadAsync(McpSection section)
        {
            var s = await GetAsync();
            return s.Enabled && IsSectionOn(s, section);
        }

        public async Task<bool> CanWriteAsync(McpSection section)
        {
            var s = await GetAsync();
            return s.Enabled && !s.ReadOnly && IsSectionOn(s, section);
        }

        private static bool IsSectionOn(McpPrivateRecord s, McpSection section) => section switch
        {
            McpSection.Cms => s.CmsEnabled,
            McpSection.Comments => s.CommentsEnabled,
            McpSection.Pages => s.PagesEnabled,
            _ => false
        };
    }

    public enum McpSection { Cms, Comments, Pages }
}
