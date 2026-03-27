(function () {
    const pluginId = 'de3ffde6-649f-4bd2-8dc2-b761c1ab97af';

    function byId(id) {
        return document.getElementById(id);
    }

    document.addEventListener('pageshow', async function (event) {
        const page = event.target;
        if (page.id !== 'SubXConfigPage') {
            return;
        }

        const config = await ApiClient.getPluginConfiguration(pluginId);
        byId('CookieHeader').value = config.CookieHeader || '';
        byId('UserAgent').value = config.UserAgent || '';
        byId('OnlySpanish').checked = config.OnlySpanish !== false;
        byId('EnableDebugLogging').checked = !!config.EnableDebugLogging;
    });

    document.addEventListener('submit', async function (event) {
        if (event.target.id !== 'SubXConfigForm') {
            return;
        }

        event.preventDefault();
        const config = await ApiClient.getPluginConfiguration(pluginId);
        config.CookieHeader = byId('CookieHeader').value;
        config.UserAgent = byId('UserAgent').value;
        config.OnlySpanish = byId('OnlySpanish').checked;
        config.EnableDebugLogging = byId('EnableDebugLogging').checked;

        await ApiClient.updatePluginConfiguration(pluginId, config);
        Dashboard.processPluginConfigurationUpdateResult();
    });
})();
