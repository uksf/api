﻿using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Extensions;

[Serializable]
public class TokenRefreshFailedException : UksfException
{
    public TokenRefreshFailedException(string message) : base(message, 401) { }
}
