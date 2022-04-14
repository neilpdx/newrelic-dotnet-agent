// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Core.Logging;
using System.IO;

namespace NewRelic.Agent.Core.Configuration
{
    public interface IConfigurationManagerStatic
    {
        string AppSettingsFilePath { get; }
        string GetAppSetting(string key);
    }

    // sdaubin : Why do we have a mock in the agent code?  This is a code smell.
    public class ConfigurationManagerStaticMock : IConfigurationManagerStatic
    {
        private readonly Func<string, string> _getAppSetting;

        public ConfigurationManagerStaticMock(Func<string, string> getAppSetting = null)
        {
            _getAppSetting = getAppSetting ?? (variable => null);
        }

        public string AppSettingsFilePath => throw new NotImplementedException();

        public string GetAppSetting(string variable)
        {
            return _getAppSetting(variable);
        }
    }

    public class ConfigurationManagerStatic : IConfigurationManagerStatic
    {
        private bool localConfigChecksDisabled;

        public string AppSettingsFilePath => AppSettingsConfigResolveWhenUsed.AppSettingsFilePath;

        public string GetAppSetting(string key)
        {
            if (localConfigChecksDisabled || key == null) return null;

            // We're wrapping this in a try/catch to deal with the case where the necessary assemblies, in this case
            // Microsoft.Extensions.Configuration, aren't present in the application being instrumented
            try
            {
                return AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
            }
            catch (FileNotFoundException e)
            {
                if (Log.IsDebugEnabled) Log.Debug($"appsettings.json will not be searched for config values because this application does not reference: {e.FileName}.");
                localConfigChecksDisabled = true;
                return null;
            }
            catch (Exception e)
            {
                if (Log.IsDebugEnabled) Log.Debug($"appsettings.json will not be searched for config values because an error was encountered: {e}");
                localConfigChecksDisabled = true;
                return null;
            }
        }
    }
}
