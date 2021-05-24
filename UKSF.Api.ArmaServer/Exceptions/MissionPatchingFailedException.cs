using System;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.ArmaServer.Exceptions
{
    [Serializable]
    public class MissionPatchingFailedException : UksfException
    {
        public MissionPatchingFailedException(string message, ValidationReportDataset validation) : base(message, 400, 1, validation) { }
    }
}
