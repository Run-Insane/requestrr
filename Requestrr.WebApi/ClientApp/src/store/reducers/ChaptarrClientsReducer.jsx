import { CHAPTARR_SET_CLIENT } from "../actions/ChaptarrClientActions";


export default function ChaptarrClientsReducer(state = {}, action) {
    if (action.type === CHAPTARR_SET_CLIENT) {
        return {
            ...state,
            chaptarr: action.payload.chaptarr,
            client: "Chaptarr"
        };
    }

    return { ...state };
}
