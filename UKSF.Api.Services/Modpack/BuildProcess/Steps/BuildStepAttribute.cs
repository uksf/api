using System;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStepAttribute : Attribute {
        public readonly string Name;

        public BuildStepAttribute(string name) => Name = name;
    }
}
