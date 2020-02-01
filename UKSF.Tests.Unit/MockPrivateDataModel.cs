using System;

// ReSharper disable NotAccessedField.Local

namespace UKSF.Tests.Unit {
    public class MockPrivateDataModel : MockDataModel {
        private readonly int count;
        private readonly string description;
        private readonly DateTime timestamp;

        public MockPrivateDataModel(string id, string description, int count, DateTime timestamp) : base(id) {
            this.description = description;
            this.count = count;
            this.timestamp = timestamp;
        }
    }
}
