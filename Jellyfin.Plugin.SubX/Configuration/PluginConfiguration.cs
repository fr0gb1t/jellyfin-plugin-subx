using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SubX.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public string CookieHeader { get; set; } = string.Empty;

    public string UserAgent { get; set; } = "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0";

    public bool OnlySpanish { get; set; } = true;

    public bool EnableDebugLogging { get; set; }
}
