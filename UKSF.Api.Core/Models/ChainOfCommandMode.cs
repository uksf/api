namespace UKSF.Api.Core.Models;

public enum ChainOfCommandMode
{
    FULL,
    NEXT_COMMANDER,
    NEXT_COMMANDER_EXCLUDE_SELF,
    COMMANDER_AND_ONE_ABOVE,
    COMMANDER_AND_PERSONNEL,
    COMMANDER_AND_TARGET_COMMANDER,
    PERSONNEL,
    TARGET_COMMANDER
}