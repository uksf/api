using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Exceptions;

[Serializable]
public class FolderException : UksfException
{
    public FolderException(string message) : base(message, 400) { }
}

[Serializable]
public class FolderNotFoundException : NotFoundException
{
    public FolderNotFoundException(string message) : base(message) { }
}

[Serializable]
public class FolderAccessDeniedException : AccessDeniedException { }
