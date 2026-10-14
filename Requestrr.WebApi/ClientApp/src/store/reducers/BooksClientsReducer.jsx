import { GET_SETTINGS, SET_DISABLED_CLIENT } from "../actions/BooksClientsActions";


export default function BooksClientsReducer(state = {}, action) {
    if (action.type === GET_SETTINGS) {
        return {
            ...state,
            client: action.payload.client,
            chaptarr: {
                hostname: action.payload.chaptarr.hostname,
                baseUrl: action.payload.chaptarr.baseUrl,
                port: action.payload.chaptarr.port,
                apiKey: action.payload.chaptarr.apiKey,
                useSSL: action.payload.chaptarr.useSSL,
                rootFolder: action.payload.chaptarr.rootFolder,
                searchNewRequests: action.payload.chaptarr.searchNewRequests,
                searchProvider: action.payload.chaptarr.searchProvider
            }
        };
    } else if (action.type === SET_DISABLED_CLIENT) {
        return {
            ...state,
            client: "Disabled"
        };
    }

    return { ...state };
}
