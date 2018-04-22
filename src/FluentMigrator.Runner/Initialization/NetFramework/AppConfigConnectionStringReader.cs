﻿#region License
// Copyright (c) 2018, FluentMigrator Project
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;

using FluentMigrator.Exceptions;

using JetBrains.Annotations;

using Microsoft.Extensions.Options;

#if NETFRAMEWORK
namespace FluentMigrator.Runner.Initialization.NetFramework
{
    /// <summary>
    /// A <see cref="IConnectionStringReader"/> implementation that uses the app or machine config
    /// </summary>
    public class AppConfigConnectionStringReader : IConnectionStringReader
    {
        private static readonly Regex _matchPwd = new Regex("(PWD=|PASSWORD=)([^;]*);", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [NotNull]
        private readonly INetConfigManager _configManager;

        [NotNull]
        private readonly IAnnouncer _announcer;

        [NotNull]
        private readonly AppConfigConnectionStringAccessorOptions _options;

        [CanBeNull]
        private readonly string _assemblyLocation;

        [CanBeNull]
        private ConnectionInfo _connectionInfo;

        internal AppConfigConnectionStringReader(
            [NotNull] INetConfigManager configManager,
            [NotNull] IAssemblySource assemblySource,
            [NotNull] IAnnouncer announcer,
            [NotNull] IOptions<AppConfigConnectionStringAccessorOptions> options)
        {
            _configManager = configManager;
            _announcer = announcer;
            _options = options.Value;
            var assemblies = assemblySource.Assemblies;
            var singleAssembly = assemblies.Count == 1 ? assemblies.Single() : null;
            _assemblyLocation = singleAssembly != null ? singleAssembly.Location : string.Empty;
        }

        [Obsolete]
        internal AppConfigConnectionStringReader(
            [NotNull] INetConfigManager configManager,
            [NotNull] string assemblyLocation,
            [NotNull] IAnnouncer announcer,
            [NotNull] IOptions<AppConfigConnectionStringAccessorOptions> options)
        {
            _configManager = configManager;
            _announcer = announcer;
            _options = options.Value;
            _assemblyLocation = assemblyLocation;
        }

        /// <inheritdoc />
        public int Priority { get; } = 0;

        /// <inheritdoc />
        public string GetConnectionString(string connectionStringOrName)
        {
            if (_connectionInfo != null)
            {
                return _connectionInfo.ConnectionString;
            }

            var result = LoadConnectionString(connectionStringOrName, _assemblyLocation);
            OutputResults(result);

            _connectionInfo = result;

            return result?.ConnectionString ?? connectionStringOrName;
        }

        [CanBeNull]
        private ConnectionInfo LoadConnectionString([CanBeNull] string connectionStringOrName, [CanBeNull] string assemblyLocation)
        {
            ConnectionInfo result = null;

            if (!string.IsNullOrEmpty(_options.ConnectionStringConfigPath))
            {
                result = LoadConnectionStringFromConfigurationFile(connectionStringOrName, _configManager.LoadFromFile(_options.ConnectionStringConfigPath));
            }

            if (result == null && !string.IsNullOrEmpty(assemblyLocation))
            {
                result = LoadConnectionStringFromConfigurationFile(connectionStringOrName, _configManager.LoadFromFile(assemblyLocation));
            }

            if (result == null)
            {
                result = LoadConnectionStringFromConfigurationFile(connectionStringOrName, _configManager.LoadFromMachineConfiguration());
            }

            if (result == null && !string.IsNullOrEmpty(connectionStringOrName))
            {
                result = new ConnectionInfo(name: null, connectionStringOrName, source: null);
            }

            return result;
        }

        [CanBeNull]
        private ConnectionInfo LoadConnectionStringFromConfigurationFile([CanBeNull] string connectionStringName, [NotNull] Configuration configurationFile)
        {
            var connections = configurationFile.ConnectionStrings.ConnectionStrings;

            if (string.IsNullOrEmpty(connectionStringName) || connections == null || connections.Count <= 0)
                return null;

            ConnectionStringSettings connectionString;

            if (string.IsNullOrEmpty(connectionStringName))
                connectionString = connections[_options.MachineName ?? Environment.MachineName];
            else
                connectionString = connections[connectionStringName];

            return ReadConnectionString(connectionString, configurationFile.FilePath);
        }

        [Pure]
        [CanBeNull]
        private ConnectionInfo ReadConnectionString(
            [CanBeNull] ConnectionStringSettings connectionSetting,
            string configurationFile)
        {
            if (connectionSetting == null)
                return null;
            return new ConnectionInfo(connectionSetting.Name, connectionSetting.ConnectionString, configurationFile);
        }

        private void OutputResults(ConnectionInfo info)
        {
            if (info == null)
                throw new UndeterminableConnectionException("Unable to resolve any connectionstring using parameters \"/connection\" and \"/configPath\"");

            var connectionString = _matchPwd.Replace(info.ConnectionString, "$1********;");
            string message;
            if (string.IsNullOrEmpty(info.Source))
            {
                if (string.IsNullOrEmpty(info.Name))
                {
                    message = $"Using connection string {connectionString}";
                }
                else
                {
                    message = $"Using database {info.Name} and connection string {connectionString}";
                }
            }
            else
            {
                message = $"Using connection {info.Name} from configuration file {info.Source}";
            }

            _announcer.Say(message);
        }

        private class ConnectionInfo
        {
            public ConnectionInfo([CanBeNull] string name, [NotNull] string connectionString, [CanBeNull] string source)
            {
                Name = name;
                ConnectionString = connectionString;
                Source = source;
            }

            [CanBeNull]
            public string Name { get; }

            [NotNull]
            public string ConnectionString { get; }

            [CanBeNull]
            public string Source { get; }
        }
    }
}
#endif
