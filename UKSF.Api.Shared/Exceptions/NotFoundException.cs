using System;

namespace UKSF.Api.Shared.Exceptions
{
    [Serializable]
    public class NotFoundException : UksfException
    {
        protected NotFoundException(string message) : base(message, 404) { }
    }
}
