namespace UKSFWebsite.Api.Models.Game {
    // https://community.bistudio.com/wiki/getClientStateNumber
    public enum GameServerState {
        NOT_RUNNING = -2, // Process is not running, but a status exists
        NOT_RESPONDING = -1, // Process is running. but no status hs been received in past 30 seconds
        NONE,
        CREATED,
        CONNECTED, // Server launched, mission not loaded (no players in)
        LOGGED_IN,
        MISSION_SELECTED,
        MISSION_ASKED,
        ROLE_ASSIGNED,
        MISSION_RECEIVED,
        GAME_LOADED,
        BRIEFING_SHOWN,
        BRIEFING_READ,
        GAME_FINISHED,
        DEBRIEFING_READ
    }
}
