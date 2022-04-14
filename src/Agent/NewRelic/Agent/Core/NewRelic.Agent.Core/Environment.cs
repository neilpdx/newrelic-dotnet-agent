// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using Newtonsoft.Json;
using NewRelic.Agent.Configuration;
using NewRelic.Core;

namespace NewRelic.Agent.Core
{
    [JsonConverter(typeof(EnvironmentConverter))]
    public class Environment
    {
        private readonly List<object[]> _environmentMap = new List<object[]>();

        private readonly IProcessStatic _processStatic;

        public ulong? TotalPhysicalMemory { get; }
        public string AppDomainAppPath { get; }

        public Environment(ISystemInfo systemInfo, IProcessStatic processStatic, IConfigurationService configurationService)
        {
            _processStatic = processStatic;

            try
            {
                TotalPhysicalMemory = systemInfo.GetTotalPhysicalMemoryBytes();

                AddVariable("Framework", () => "dotnet");

                var fileVersionInfo = TryGetFileVersionInfo();
                AddVariable("Product Name", () => fileVersionInfo?.ProductName);

                AddVariable("OS", () => System.Environment.OSVersion?.VersionString);

                // This API is only supported on .net FX 4.7 + so limiting it
                // to .net core since that is the one affected. 
                AddVariable(".NET Version", () => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.ToString());
                AddVariable("Processor Architecture", () => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString());

                AddVariable("Total Physical System Memory", () => TotalPhysicalMemory);

                var process = TryGetCurrentProcess();
                AddVariable("StartTime", () => process?.StartTime.ToString("o"));
                AddVariable("MainModule.FileVersionInfo", () => process?.FileVersionInfo.ToString());

                AddVariable("GCSettings.IsServerGC", () => System.Runtime.GCSettings.IsServerGC);
                AddVariable("AppDomain.FriendlyName", () => AppDomain.CurrentDomain.FriendlyName);

                // If we have a name, report it and its source...
                if (configurationService.Configuration.ApplicationNames.Any())
                {
                    AddVariable("Initial Application Names", () => String.Join(", ", configurationService.Configuration.ApplicationNames));
                    AddVariable("Initial Application Names Source", () => configurationService.Configuration.ApplicationNamesSource);
                }

                AddVariable("Initial NewRelic Config", () => configurationService.Configuration.NewRelicConfigFilePath);

                // If we found an app config, report it...
                if (!String.IsNullOrEmpty(configurationService.Configuration.AppSettingsConfigFilePath))
                    AddVariable("Application Config", () => configurationService.Configuration.AppSettingsConfigFilePath);

                AddVariable("Plugin List", GetLoadedAssemblyNames);

#if DEBUG
				AddVariable("Debug Build", () => true.ToString());
#endif
            }
            catch (Exception ex)
            {
                Log.Debug($"The .NET agent is unable to collect environment information for the machine: {ex}");
            }
        }

        public void AddVariable(string name, Func<object> valueGetter)
        {
            var value = null as object;
            try
            {
                value = valueGetter();
            }
            catch (Exception ex)
            {
                Log.Warn($"Error getting value for environment variable {name}: {ex}");
            }

            _environmentMap.Add(new[] { name, value });
        }

        private IProcess TryGetCurrentProcess()
        {
            try
            {
                return _processStatic.GetCurrentProcess();
            }
            catch (Exception ex)
            {
                Log.Warn(ex);
                return null;
            }
        }

        private static FileVersionInfo TryGetFileVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                return FileVersionInfo.GetVersionInfo(assembly.Location);
            }
            catch (Exception ex)
            {
                Log.Warn(ex);
                return null;
            }
        }

        public static string TryGetAppPath(Func<string> pathGetter)
        {
            try
            {
                var path = pathGetter();

                if (path == null)
                    return null;

                if (path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                    path = path.Substring(0, path.Length - 1);

                var index = path.LastIndexOf(System.IO.Path.DirectorySeparatorChar + "wwwroot", StringComparison.InvariantCultureIgnoreCase);
                if (index > 0)
                    path = path.Substring(0, index);

                index = path.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
                if (index > 0 && index < path.Length - 1)
                    path = path.Substring(index + 1);

                return path;
            }
            catch (Exception ex)
            {
                Log.Warn(ex);
                return null;
            }
        }


        private static IEnumerable<string> GetLoadedAssemblyNames()
        {
            var versionZero = new Version(0, 0, 0, 0);
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly != null)
                .Where(assembly => assembly.GetName().Version != versionZero)

                .Select(assembly => assembly.FullName)
                .ToList();
        }

        public class EnvironmentConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var environment = value as Environment;
                if (environment == null)
                    throw new NullReferenceException("environment");

                var serialized = JsonConvert.SerializeObject(environment._environmentMap);
                writer.WriteRawValue(serialized);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override bool CanConvert(Type objectType)
            {
                return true;
            }
        }
    }
}
