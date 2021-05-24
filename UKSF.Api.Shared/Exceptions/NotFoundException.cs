using System;

namespace UKSF.Api.Shared.Exceptions
{
    [Serializable]
    public class NotFoundException : UksfException
    {
        public NotFoundException(string message) : base(message, 404) { }
    }
}
