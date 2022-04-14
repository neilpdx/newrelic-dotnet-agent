// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class TaskFriendlySyncContextValidator
    {
        private const string AsyncTransactionsMissingSupportUrl =
            "https://docs.newrelic.com/docs/agents/net-agent/troubleshooting/missing-async-metrics";

        public static CanWrapResponse CanWrapAsyncMethod(string assemblyName, string typeName, string methodName)
        {
            return LegacyAspSyncContextConfigured()
                ? new CanWrapResponse(false, LegacyAspPipelineNotSupportedMessage(assemblyName, typeName, methodName))
                : new CanWrapResponse(true);
        }

        private static string LegacyAspPipelineNotSupportedMessage(string assemblyName, string typeName, string methodName)
        {
            return $"The method {methodName} in class {typeName} from assembly {assemblyName} will not be instrumented.  Some async instrumentation is not supported on .NET 4.5 and greater unless you change your application configuration to use the new ASP pipeline. For details see: {AsyncTransactionsMissingSupportUrl}";
        }

        private static bool LegacyAspSyncContextConfigured()
        {
			return false;
        }

    }
}
