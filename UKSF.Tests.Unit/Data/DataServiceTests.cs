using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Win32.TaskScheduler;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Data;
using UKSF.Api.Data.Utility;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Utility;
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class DataServiceTests {
        [Fact]
        public void ShouldCreateCollection() {
            // Mock<DataService<ConfirmationCode, IConfirmationCodeDataService>> mockDataService = new Mock<DataService<ConfirmationCode, IConfirmationCodeDataService>>();
            // Mock<IMongoDatabase> mockMongoDatabase = new Mock<IMongoDatabase>();
            // Mock<IDataEventBus<IConfirmationCodeDataService>> mockDataEventBus = new Mock<IDataEventBus<IConfirmationCodeDataService>>();
            // ConfirmationCodeDataService confirmationCodeDataService = new ConfirmationCodeDataService(mockMongoDatabase.Object, mockDataEventBus.Object);
            //
            // List<ConfirmationCode> confirmationCodeData = new List<ConfirmationCode>();
            //
            // mockMongoDatabase.Setup(x => x.GetCollection<ConfirmationCode>("confirmationCodes")).Returns<List<ConfirmationCode>>(confirmationCodeData);
        }
    }
}
