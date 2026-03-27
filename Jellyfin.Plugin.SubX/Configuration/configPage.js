(function () {
    const pluginId = 'de3ffde6-649f-4bd2-8dc2-b761c1ab97af';

    function byId(page, id) {
        return page.querySelector('#' + id);
    }

    document.addEventListener('pageshow', async function (event) {
        const page = event.target;
        if (page.id !== 'SubXConfigPage') {
            return;
        }

        Dashboard.showLoadingMsg();

        try {
            const config = await ApiClient.getPluginConfiguration(pluginId);
            byId(page, 'CookieHeader').value = config.CookieHeader || '';
            byId(page, 'UserAgent').value = config.UserAgent || '';
            byId(page, 'OnlySpanish').checked = config.OnlySpanish !== false;
            byId(page, 'EnableDebugLogging').checked = !!config.EnableDebugLogging;
        } catch (error) {
            Dashboard.processErrorResponse(error);
        } finally {
            Dashboard.hideLoadingMsg();
        }
    });

    document.addEventListener('submit', async function (event) {
        const form = event.target;
        if (form.id !== 'SubXConfigForm') {
            return;
        }

        event.preventDefault();
        const page = form.closest('#SubXConfigPage');
        Dashboard.showLoadingMsg();

        try {
            const config = await ApiClient.getPluginConfiguration(pluginId);
            config.CookieHeader = byId(page, 'CookieHeader').value;
            config.UserAgent = byId(page, 'UserAgent').value;
            config.OnlySpanish = byId(page, 'OnlySpanish').checked;
            config.EnableDebugLogging = byId(page, 'EnableDebugLogging').checked;

            const result = await ApiClient.updatePluginConfiguration(pluginId, config);
            Dashboard.processPluginConfigurationUpdateResult(result);
        } catch (error) {
            Dashboard.processErrorResponse(error);
        } finally {
            Dashboard.hideLoadingMsg();
        }
    });
})();
