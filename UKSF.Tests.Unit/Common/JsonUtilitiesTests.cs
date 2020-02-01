using System;
using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson;
using UKSF.Common;
using Xunit;

namespace UKSF.Tests.Unit.Common {
    public class JsonUtilitiesTests {
        [Fact]
        public void ShouldCopyComplexObject() {
            MockDataModel mockDataModel1 = new MockDataModel {Name = "1"};
            MockDataModel mockDataModel2 = new MockDataModel {Name = "2"};
            MockDataModel mockDataModel3 = new MockDataModel {Name = "3"};
            MockComplexDataModel mockComplexDataModel = new MockComplexDataModel {Name = "Test", Data = mockDataModel1, List = new List<string> {"a", "b", "c"}, DataList = new List<MockDataModel> {mockDataModel1, mockDataModel2, mockDataModel3}};

            MockComplexDataModel subject = mockComplexDataModel.Copy<MockComplexDataModel>();

            subject.id.Should().Be(mockComplexDataModel.id);
            subject.Name.Should().Be(mockComplexDataModel.Name);
            subject.Data.Should().NotBe(mockDataModel1);
            subject.List.Should().HaveCount(3).And.Contain(new List<string> {"a", "b", "c"});
            subject.DataList.Should().HaveCount(3).And.NotContain(new List<MockDataModel> {mockDataModel1, mockDataModel2, mockDataModel3});
        }

        [Fact]
        public void ShouldCopyObject() {
            MockDataModel mockDataModel = new MockDataModel {Name = "Test"};

            MockDataModel subject = mockDataModel.Copy<MockDataModel>();

            subject.id.Should().Be(mockDataModel.id);
            subject.Name.Should().Be(mockDataModel.Name);
        }

        [Fact]
        public void ShouldSerializePrivateFields() {
            MockPrivateDataModel mockPrivateDataModel = new MockPrivateDataModel("5e35dafaed582b5a4cec346e", "A thing", 4, DateTime.Parse("16/01/1996")) { Name = "Charlie"};

            string subject = mockPrivateDataModel.DeepJsonSerializeObject();

            subject.Should().Be("{\"count\":4,\"description\":\"A thing\",\"timestamp\":\"1996-01-16T00:00:00\",\"Name\":\"Charlie\",\"id\":\"5e35dafaed582b5a4cec346e\"}");
        }
    }
}
