using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class InvalidLoaScopeException(string scope) : UksfException($"'{scope}' is an invalid LOA scope", 400);
