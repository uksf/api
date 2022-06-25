using System;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Personnel.Exceptions
{
    [Serializable]
    public class AgeNotAllowedException : UksfException
    {
        public AgeNotAllowedException() : base("Application cannot be accepted due to age requirements. Speak to ELCOM", 400) { }
    }
}
