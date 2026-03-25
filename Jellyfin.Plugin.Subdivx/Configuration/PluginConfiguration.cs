using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Subdivx.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool UseBridge { get; set; }

    public string BridgeBaseUrl { get; set; } = string.Empty;

    public string BridgeApiKey { get; set; } = string.Empty;

    public string CookieHeader { get; set; } = string.Empty;

    public string CfClearance { get; set; } = string.Empty;

    public string SdxCookie { get; set; } = string.Empty;

    public string UserAgent { get; set; } = "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0";

    public bool OnlySpanish { get; set; } = true;

    public bool EnableDebugLogging { get; set; }
}
