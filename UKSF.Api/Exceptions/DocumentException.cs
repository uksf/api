using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class DocumentException : UksfException
{
    public DocumentException(string message) : base(message, 400) { }
}

[Serializable]
public class DocumentNotFoundException : NotFoundException
{
    public DocumentNotFoundException(string message) : base(message) { }
}

[Serializable]
public class DocumentAccessDeniedException : AccessDeniedException { }
