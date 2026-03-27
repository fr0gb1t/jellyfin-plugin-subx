const SubXConfig = {
    pluginUniqueId: 'de3ffde6-649f-4bd2-8dc2-b761c1ab97af'
};

function byId(view, id) {
    return view.querySelector('#' + id);
}

export default function (view) {
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(SubXConfig.pluginUniqueId).then(function (config) {
            byId(view, 'CookieHeader').value = config.CookieHeader || '';
            byId(view, 'UserAgent').value = config.UserAgent || '';
            byId(view, 'OnlySpanish').checked = config.OnlySpanish !== false;
            byId(view, 'EnableDebugLogging').checked = !!config.EnableDebugLogging;
            Dashboard.hideLoadingMsg();
        }).catch(function (error) {
            Dashboard.hideLoadingMsg();
            Dashboard.processErrorResponse(error);
        });
    });

    view.querySelector('#SubXConfigForm').addEventListener('submit', function (event) {
        event.preventDefault();
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(SubXConfig.pluginUniqueId).then(function (config) {
            config.CookieHeader = byId(view, 'CookieHeader').value.trim();
            config.UserAgent = byId(view, 'UserAgent').value.trim();
            config.OnlySpanish = !!byId(view, 'OnlySpanish').checked;
            config.EnableDebugLogging = !!byId(view, 'EnableDebugLogging').checked;

            return ApiClient.updatePluginConfiguration(SubXConfig.pluginUniqueId, config);
        }).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
        }).catch(function (error) {
            Dashboard.hideLoadingMsg();
            Dashboard.processErrorResponse(error);
        });

        return false;
    });
}
