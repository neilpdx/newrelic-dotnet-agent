﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class ErrorTraceWcfIisHosted : IClassFixture<RemoteServiceFixtures.WcfAppIisHosted>
    {
        private readonly RemoteServiceFixtures.WcfAppIisHosted _fixture;

        public ErrorTraceWcfIisHosted(RemoteServiceFixtures.WcfAppIisHosted fixture, ITestOutputHelper testLogger)
        {
            _fixture = fixture;
            _fixture.TestLogger = testLogger;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var newRelicConfig = _fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(newRelicConfig);

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "attributes" }, "include", "service.request.*");

                    var webConfig = Path.Combine(_fixture.DestinationApplicationDirectoryPath, "web.config");

                    CommonUtils.ModifyOrCreateXmlAttribute(webConfig, "", new[] { "configuration", "system.serviceModel", "serviceHostingEnvironment" }, "aspNetCompatibilityEnabled", "false");

                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(webConfig, new[] { "configuration", "errorCollector" }, "ignoreErrors");
                    CommonUtils.DeleteXmlNodeFromNewRelicConfig(webConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes");

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreErrors", "");
                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(newRelicConfig, new[] { "configuration", "errorCollector" }, "ignoreStatusCodes", "");
                },
                exerciseApplication: () =>
                {
                    _fixture.ThrowException();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // error metrics
                new Assertions.ExpectedMetric {metricName = @"Errors/all", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Errors/allWeb", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.ThrowException", callCount = 1},

                // other
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Apdex" },
                new Assertions.ExpectedMetric { metricName = @"ApdexAll" },
                new Assertions.ExpectedMetric { metricName = @"Apdex/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.ThrowException" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.ThrowException", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"DotNet/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.ThrowException", metricScope = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.ThrowException", callCount = 1 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
            };

            var expectedAttributes = new Dictionary<string, string>
            {
                { "errorType", "System.Exception" },
                { "errorMessage", "ExceptionMessage" },
            };

            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.class", "System.Exception" },
                { "error.message", "ExceptionMessage" },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces()
                .Where(trace => trace.Path == @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.ThrowException")
                .ToList();

            var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.True(errorTraces.Any(), "No error trace found."),
                () => Assert.True(errorTraces.Count == 1, $"Expected 1 errors traces but found {errorTraces.Count}"),
                () => Assert.Equal("WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted.IMyService.ThrowException", errorTraces[0].Path),
                () => Assert.Equal("System.Exception", errorTraces[0].ExceptionClassName),
                () => Assert.Equal("ExceptionMessage", errorTraces[0].Message),
                () => Assert.NotEmpty(errorTraces[0].Attributes.StackTrace),
                () => Assert.True(transactionEvents.Any(), "No transaction events found."),
                () => Assert.True(transactionEvents.Count == 1, $"Expected 1 transaction event but found {transactionEvents.Count}"),
                () => Assertions.TransactionEventHasAttributes(expectedAttributes, TransactionEventAttributeType.Intrinsic, transactionEvents[0]),
                () => Assert.Single(errorEvents),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.Intrinsic, errorEvents[0].Events[0])
            );
        }
    }
}