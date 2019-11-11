namespace UKSFWebsite.Api.Models.Command {
    public enum ChainOfCommandMode {
        FULL,
        NEXT_COMMANDER,
        NEXT_COMMANDER_EXCLUDE_SELF,
        COMMANDER_AND_ONE_ABOVE,
        COMMANDER_AND_SR10,
        COMMANDER_AND_TARGET_COMMANDER,
        SR10,
        TARGET_COMMANDER
    }
}
