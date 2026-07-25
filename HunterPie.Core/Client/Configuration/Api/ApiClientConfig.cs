using HunterPie.Core.Architecture;
using HunterPie.Core.Settings;
using HunterPie.Core.Settings.Annotations;
using HunterPie.Core.Settings.Common;
using HunterPie.Core.Settings.Types;

namespace HunterPie.Core.Client.Configuration.Api;

[Configuration(
    name: "API_STRING",
    icon: "ICON_HUNTERPIE",
    group: CommonConfigurationGroups.CLIENT)]
public class ApiClientConfig : ISettings
{
    [ConfigurationProperty("ENABLE_API_SERVER", requiresRestart: true, group: ApiConfigurationGroups.API_SERVER)]
    public Observable<bool> Enable { get; set; } = true;

    [ConfigurationProperty("API_SERVER_BIND_ALL_INTERFACES", requiresRestart: true, group: ApiConfigurationGroups.API_SERVER)]
    public Observable<bool> BindAllInterfaces { get; set; } = true;

    [ConfigurationProperty("API_SERVER_PORT", requiresRestart: true, group: ApiConfigurationGroups.API_SERVER)]
    public Range Port { get; set; } = new(7273, 65535, 1024, 1);

    [ConfigurationProperty("API_SERVER_AUTH_TOKEN", group: ApiConfigurationGroups.API_SERVER)]
    public Secret AuthToken { get; set; } = new();

    [ConfigurationProperty("API_SERVER_BROADCAST_INTERVAL", group: ApiConfigurationGroups.API_SERVER)]
    public Range BroadcastInterval { get; set; } = new(200, 1000, 50, 10);
}
