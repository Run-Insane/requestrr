export const CHAPTARR_SET_CLIENT = "booksClients:set_chaptarr_client";


export function setChaptarrClient(settings) {
    return {
        type: CHAPTARR_SET_CLIENT,
        payload: settings
    };
};


export function setChaptarrConnectionSettings(connectionSettings) {
    return (dispatch, getState) => {
        const state = getState();

        let chaptarr = {
            ...state.books.chaptarr,
            hostname: connectionSettings.hostname,
            baseUrl: connectionSettings.baseUrl,
            port: connectionSettings.port,
            apiKey: connectionSettings.apiKey,
            useSSL: connectionSettings.useSSL,
            rootFolder: connectionSettings.rootFolder,
            searchNewRequests: connectionSettings.searchNewRequests,
            searchProvider: connectionSettings.searchProvider
        };

        dispatch(setChaptarrClient({
            chaptarr: chaptarr
        }));

        return new Promise((resolve, reject) => {
            return { ok: false };
        });
    }
}


export function testChaptarrSettings(settings) {
    return (dispatch, getState) => {
        const state = getState();

        return fetch("../api/books/chaptarr/test", {
            method: "POST",
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json",
                "Authorization": `Bearer ${state.user.token}`
            },
            body: JSON.stringify({
                "Hostname": settings.hostname,
                "BaseUrl": settings.baseUrl,
                "Port": Number(settings.port),
                "ApiKey": settings.apiKey,
                "UseSSL": settings.useSSL
            })
        })
            .then(data => data.json())
            .then(data => {
                if (data.ok)
                    return { ok: true };
                return { ok: false, error: data };
            });
    }
}


export function saveChaptarrClient(saveModel) {
    return (dispatch, getState) => {
        const state = getState();

        return fetch("../api/books/chaptarr", {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'Authorization': `Bearer ${state.user.token}`
            },
            body: JSON.stringify({
                'Hostname': saveModel.chaptarr.hostname,
                'BaseUrl': saveModel.chaptarr.baseUrl,
                'Port': Number(saveModel.chaptarr.port),
                'ApiKey': saveModel.chaptarr.apiKey,
                'UseSSL': saveModel.chaptarr.useSSL,
                'RootFolder': saveModel.chaptarr.rootFolder,
                'SearchNewRequests': saveModel.chaptarr.searchNewRequests,
                'SearchProvider': saveModel.chaptarr.searchProvider
            })
        })
            .then(data => data.json())
            .then(data => {
                if (data.ok) {
                    let newChaptarr = {
                        ...state.books.chaptarr,
                        hostname: saveModel.chaptarr.hostname,
                        baseUrl: saveModel.chaptarr.baseUrl,
                        port: saveModel.chaptarr.port,
                        apiKey: saveModel.chaptarr.apiKey,
                        useSSL: saveModel.chaptarr.useSSL,
                        rootFolder: saveModel.chaptarr.rootFolder,
                        searchNewRequests: saveModel.chaptarr.searchNewRequests,
                        searchProvider: saveModel.chaptarr.searchProvider
                    };

                    dispatch(setChaptarrClient({
                        chaptarr: newChaptarr
                    }));
                    return { ok: true };
                }

                return { ok: false, error: data };
            });
    }
}
