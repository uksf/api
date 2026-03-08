namespace UKSF.Api.ArmaMissions.Exceptions;

[Serializable]
public class PboOperationException(string message) : Exception(message);
