(function () {
    const pluginId = '6d3e5d26-3fa8-4b35-8fc7-e9dbe06a4001';

    function byId(id) {
        return document.getElementById(id);
    }

    document.addEventListener('pageshow', async function (event) {
        const page = event.target;
        if (page.id !== 'SubdivxConfigPage') {
            return;
        }

        const config = await ApiClient.getPluginConfiguration(pluginId);
        byId('UseBridge').checked = !!config.UseBridge;
        byId('BridgeBaseUrl').value = config.BridgeBaseUrl || '';
        byId('BridgeApiKey').value = config.BridgeApiKey || '';
        byId('CookieHeader').value = config.CookieHeader || '';
        byId('CfClearance').value = config.CfClearance || '';
        byId('SdxCookie').value = config.SdxCookie || '';
        byId('UserAgent').value = config.UserAgent || '';
        byId('OnlySpanish').checked = config.OnlySpanish !== false;
        byId('EnableDebugLogging').checked = !!config.EnableDebugLogging;
    });

    document.addEventListener('submit', async function (event) {
        if (event.target.id !== 'SubdivxConfigForm') {
            return;
        }

        event.preventDefault();
        const config = await ApiClient.getPluginConfiguration(pluginId);
        config.UseBridge = byId('UseBridge').checked;
        config.BridgeBaseUrl = byId('BridgeBaseUrl').value;
        config.BridgeApiKey = byId('BridgeApiKey').value;
        config.CookieHeader = byId('CookieHeader').value;
        config.CfClearance = byId('CfClearance').value;
        config.SdxCookie = byId('SdxCookie').value;
        config.UserAgent = byId('UserAgent').value;
        config.OnlySpanish = byId('OnlySpanish').checked;
        config.EnableDebugLogging = byId('EnableDebugLogging').checked;

        await ApiClient.updatePluginConfiguration(pluginId, config);
        Dashboard.processPluginConfigurationUpdateResult();
    });
})();
