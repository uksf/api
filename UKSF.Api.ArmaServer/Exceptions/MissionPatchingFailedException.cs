using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Exceptions;

[Serializable]
public class MissionPatchingFailedException : UksfException
{
    public MissionPatchingFailedException(string message, ValidationReportDataset validation) : base(message, 400, 1, validation) { }
}
