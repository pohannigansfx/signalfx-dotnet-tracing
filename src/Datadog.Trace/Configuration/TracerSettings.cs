// Modified by SignalFx
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public class TracerSettings
    {
        /// <summary>
        /// The default agent/collector url value <see cref="EndpointUrl"/>.
        /// </summary>
        public const string DefaultEndpointUrl = "http://localhost:9080/v1/trace";

        /// <summary>
        /// The default API for <see cref="ApiType"/>.
        /// </summary>
        public const string DefaultApiType = "zipkin";

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
        /// </summary>
        public TracerSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public TracerSettings(IConfigurationSource source)
        {
            Environment = source?.GetString(ConfigurationKeys.Environment);

            ServiceName = source?.GetString(ConfigurationKeys.ServiceName);

            TraceEnabled = source?.GetBool(ConfigurationKeys.TraceEnabled) ??
                           // default value
                           true;

            var disabledIntegrationNames = source?.GetString(ConfigurationKeys.DisabledIntegrations)
                                                 ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                           Enumerable.Empty<string>();

            DisabledIntegrationNames = new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase);

            var endpointUrl = source?.GetString(ConfigurationKeys.EndpointUrl) ?? DefaultEndpointUrl;
            EndpointUrl = new Uri(endpointUrl);

            var agentHost = source?.GetString(ConfigurationKeys.AgentHost) ??
                            // backwards compatibility for names used in the past
                            source?.GetString("SIGNALFX_TRACE_AGENT_HOSTNAME") ??
                            source?.GetString("DATADOG_TRACE_AGENT_HOSTNAME") ??
                            // default value
                            EndpointUrl.Host;

            var agentPort = source?.GetInt32(ConfigurationKeys.AgentPort) ??
                            // backwards compatibility for names used in the past
                            source?.GetInt32("DATADOG_TRACE_AGENT_PORT") ??
                            // default value
                            EndpointUrl.Port;

            var agentUri = source?.GetString(ConfigurationKeys.AgentUri) ??
                           // default value
                           $"{EndpointUrl.Scheme}://{agentHost}:{agentPort}";

            AgentUri = new Uri(agentUri);

            var agentPath = source?.GetString(ConfigurationKeys.AgentPath) ?? EndpointUrl.PathAndQuery;

            // We still want tests and users to be able to configure Uri elements, so reform Endpoint Url
            EndpointUrl = new Uri(AgentUri, agentPath);

            ApiType = source?.GetString(ConfigurationKeys.ApiType) ?? DefaultApiType;

            AnalyticsEnabled = source?.GetBool(ConfigurationKeys.GlobalAnalyticsEnabled) ??
                               // default value
                               false;

            LogsInjectionEnabled = source?.GetBool(ConfigurationKeys.LogsInjectionEnabled) ??
                                   // default value
                                   false;

            var maxTracesPerSecond = source?.GetInt32(ConfigurationKeys.MaxTracesSubmittedPerSecond);

            if (maxTracesPerSecond != null)
            {
                // Ensure our flag for the rate limiter is enabled
                RuleBasedSampler.OptInTracingWithoutLimits();
            }
            else
            {
                maxTracesPerSecond = 100; // default
            }

            MaxTracesSubmittedPerSecond = maxTracesPerSecond.Value;

            Integrations = new IntegrationSettingsCollection(source);

            GlobalTags = source?.GetDictionary(ConfigurationKeys.GlobalTags) ??
                         // default value (empty)
                         new ConcurrentDictionary<string, string>();

            GlobalTags.Add(Tags.Language, TracerConstants.Language);
            GlobalTags.Add(Tags.Version, TracerConstants.AssemblyVersion);

            DogStatsdPort = source?.GetInt32(ConfigurationKeys.DogStatsdPort) ??
                            // default value
                            8125;

            TracerMetricsEnabled = source?.GetBool(ConfigurationKeys.TracerMetricsEnabled) ??
                                   // default value
                                   false;

            CustomSamplingRules = source?.GetString(ConfigurationKeys.CustomSamplingRules);

            GlobalSamplingRate = source?.GetDouble(ConfigurationKeys.GlobalSamplingRate);

            DiagnosticSourceEnabled = source?.GetBool(ConfigurationKeys.DiagnosticSourceEnabled) ??
                                      // default value
                                      true;

            TagMongoCommands = source?.GetBool(ConfigurationKeys.TagMongoCommands) ?? true;

            TagElasticsearchQueries = source?.GetBool(ConfigurationKeys.TagElasticsearchQueries) ?? true;

            TagRedisCommands = source?.GetBool(ConfigurationKeys.TagRedisCommands) ?? true;

            SanitizeSqlStatements = source?.GetBool(ConfigurationKeys.SanitizeSqlStatements) ?? true;

            AdditionalDiagnosticListeners = source?.GetString(ConfigurationKeys.AdditionalDiagnosticListeners)
                                                 ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(s => s.Trim()).ToArray() ??
                                            new string[0];

            AppendUrlPathToName = source?.GetBool(ConfigurationKeys.AppendUrlPathToName) ??
                           // default value
                           false;

            UseWebServerResourceAsOperationName = source?.GetBool(ConfigurationKeys.UseWebServerResourceAsOperationName) ??
                // default value
                true;
        }

        /// <summary>
        /// Gets or sets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        public string Environment { get; set; }

        /// <summary>
        /// Gets or sets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        public bool TraceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug is enabled for a tracer.
        /// This property is obsolete. Manage the debug setting through GlobalSettings.
        /// </summary>
        /// <seealso cref="GlobalSettings.DebugEnabled"/>
        [Obsolete]
        public bool DebugEnabled { get; set; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
        public HashSet<string> DisabledIntegrationNames { get; set; }

        /// <summary>
        /// Gets or sets the Uri where the Tracer can connect to the Agent.
        /// Default is <c>"http://localhost:9080"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        public Uri AgentUri { get; set; }

        /// <summary>
        /// Gets or sets the Uri where the Tracer's Zipkin ApiType can report trace data.
        /// Default is <c>"http://localhost:9080/v1/trace"</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.EndpointUrl"/>
        /// <seealso cref="ConfigurationKeys.AgentUri"/>
        /// <seealso cref="ConfigurationKeys.AgentHost"/>
        /// <seealso cref="ConfigurationKeys.AgentPort"/>
        /// <seealso cref="ConfigurationKeys.AgentPath"/>
        public Uri EndpointUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating API type
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ApiType"/>
        public string ApiType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether default Analytics are enabled.
        /// Settings this value is a shortcut for setting
        /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
        /// See the documentation for more details.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
        public bool AnalyticsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        public bool LogsInjectionEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.MaxTracesSubmittedPerSecond"/>
        public int MaxTracesSubmittedPerSecond { get; set; }

        /// <summary>
        /// Gets or sets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        public string CustomSamplingRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        public double? GlobalSamplingRate { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="Integrations"/> keyed by integration name.
        /// </summary>
        public IntegrationSettingsCollection Integrations { get; }

        /// <summary>
        /// Gets or sets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        public IDictionary<string, string> GlobalTags { get; set; }

        /// <summary>
        /// Gets or sets the port where the DogStatsd server is listening for connections.
        /// Default is <c>8125</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DogStatsdPort"/>
        public int DogStatsdPort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        public bool TracerMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether MongoDb integration
        /// should tag the command BsonDocument as db.statement.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagMongoCommands"/>
        public bool TagMongoCommands { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Elasticsearch integration
        /// should tag PostData as db.statement.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagElasticsearchQueries"/>
        public bool TagElasticsearchQueries { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Redis integrations
        /// should tag commands as db.statement.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagRedisCommands"/>
        public bool TagRedisCommands { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to sanitize SQL db.statement
        /// </summary>
        /// <seealso cref="ConfigurationKeys.SanitizeSqlStatements"/>
        public bool SanitizeSqlStatements { get; set; }

        /// <summary>
        /// Gets or sets the list of <see cref="System.Diagnostics.DiagnosticListener"/> to subscribe to the
        /// ASP.NET Core instrumentation's observer.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AdditionalDiagnosticListeners"/>
        public string[] AdditionalDiagnosticListeners { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the use
        /// of <see cref="System.Diagnostics.DiagnosticSource"/> is enabled.
        /// </summary>
        public bool DiagnosticSourceEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the absolute URL path should be
        /// appended to the span name.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AppendUrlPathToName"/>
        public bool AppendUrlPathToName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the resource name is going to be used as the span name.
        /// This applies to "AspNetMvc" and "AspNetWebApi" instrumentations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.UseWebServerResourceAsOperationName"/>
        /// <seealso cref="ExtensionMethods.SpanExtensions.DecorateWebServerSpan(Span, string, string, string, string)"/>
        public bool UseWebServerResourceAsOperationName { get; set; }

        /// <summary>
        /// Create a <see cref="TracerSettings"/> populated from the default sources
        /// returned by <see cref="CreateDefaultConfigurationSource"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        public static TracerSettings FromDefaultSources()
        {
            var source = CreateDefaultConfigurationSource();
            return new TracerSettings(source);
        }

        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        public static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            return GlobalSettings.CreateDefaultConfigurationSource();
        }

        internal bool IsIntegrationEnabled(string name)
        {
            bool disabled = Integrations[name].Enabled == false || DisabledIntegrationNames.Contains(name);
            return TraceEnabled && !disabled;
        }

        internal double? GetIntegrationAnalyticsSampleRate(string name, bool enabledWithGlobalSetting)
        {
            var integrationSettings = Integrations[name];
            var analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
        }
    }
}
