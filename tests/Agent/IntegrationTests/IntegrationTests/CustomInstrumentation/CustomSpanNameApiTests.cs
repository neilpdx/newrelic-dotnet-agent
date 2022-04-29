// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class CustomSpanNameApiTestsFW462 : CustomSpanNameApiTests<ConsoleDynamicMethodFixtureFW462>
    {
        public CustomSpanNameApiTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class CustomSpanNameApiTestsCore31 : CustomSpanNameApiTests<ConsoleDynamicMethodFixtureCore31>
    {
        public CustomSpanNameApiTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public abstract class CustomSpanNameApiTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const string LibraryClassName = "MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.AttributeInstrumentation";

        protected readonly TFixture Fixture;

        public CustomSpanNameApiTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.AddCommand($"AttributeInstrumentation MakeOtherTransactionWithThreadedCallToInstrumentedMethod");
            Fixture.AddCommand("RootCommands DelaySeconds 5");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>();

           // expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"OtherTransaction/all", callCount = ForceNewTransaction ? 2 : 1 });
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"OtherTransaction/Custom/{LibraryClassName}/MakeOtherTransactionWithThreadedCallToInstrumentedMethod", callCount = 1 });
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/MakeOtherTransactionWithThreadedCallToInstrumentedMethod", callCount = 1 });

            //if (ForceNewTransaction)
            //{
            //    expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"OtherTransaction/Custom/{LibraryClassName}/SpanOrTransactionBasedOnConfig", callCount = 1 });
            //}
            expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/SpanOrTransactionBasedOnConfig", callCount = 1 });

            var metrics = Fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }
    }
}
