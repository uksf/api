namespace UKSF.Api.Core.Models;

public enum ChainOfCommandMode
{
    Full,
    Next_Commander,
    Next_Commander_Exclude_Self,
    Commander_And_One_Above,
    Commander_And_Personnel,
    Commander_And_Target_Commander,
    Personnel,
    Target_Commander
}
