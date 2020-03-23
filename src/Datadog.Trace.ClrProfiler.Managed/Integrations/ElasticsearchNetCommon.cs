// Modified by SignalFx
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class ElasticsearchNetCommon
    {
        public const string OperationName = "elasticsearch.query";
        public const string ServiceName = "elasticsearch";
        public const string SpanType = "elasticsearch";
        public const string ComponentValue = "elasticsearch-net";
        public const string ElasticsearchActionKey = "elasticsearch.action";
        public const string ElasticsearchMethodKey = "elasticsearch.method";
        public const string ElasticsearchUrlKey = "elasticsearch.url";
        public const string ElasticsearchPathKey = "elasticsearch.path";

        public static readonly Type CancellationTokenType = typeof(CancellationToken);
        public static readonly Type RequestPipelineType = Type.GetType("Elasticsearch.Net.IRequestPipeline, Elasticsearch.Net");
        public static readonly Type RequestDataType = Type.GetType("Elasticsearch.Net.RequestData, Elasticsearch.Net");

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(ElasticsearchNetCommon));

        public static Scope CreateScope(Tracer tracer, string integrationName, object pipeline, object requestData)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string requestName = pipeline.GetProperty("RequestParameters")
                                         .GetValueOrDefault()
                                        ?.GetType()
                                         .Name
                                         .Replace("RequestParameters", string.Empty);

            var pathAndQuery = requestData.GetProperty<string>("PathAndQuery").GetValueOrDefault() ??
                               requestData.GetProperty<string>("Path").GetValueOrDefault();

            string method = requestData.GetProperty("Method").GetValueOrDefault()?.ToString();
            var url = requestData.GetProperty("Uri").GetValueOrDefault()?.ToString();

            var serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                var operationName = requestName ?? OperationName;
                scope = tracer.StartActive(operationName, serviceName: serviceName);
                var span = scope.Span;
                span.SetTag(Tags.InstrumentationName, ComponentValue);
                span.SetTag(Tags.DbType, SpanType);
                span.SetTag(Tags.SpanKind, SpanKinds.Client);
                span.SetTag(ElasticsearchMethodKey, method);
                span.SetTag(ElasticsearchUrlKey, url);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(integrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        public static Scope CreateRequestScope(Tracer tracer, string integrationName, object httpMethod, object path, object requestParameters)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationName))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            string requestName = requestParameters?.GetType()
                                         .Name
                                         .Replace("RequestParameters", string.Empty);

            var serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                var operationName = requestName ?? OperationName;
                scope = tracer.StartActive(operationName, serviceName: serviceName);
                var span = scope.Span;
                span.SetTag(Tags.InstrumentationName, ComponentValue);
                span.SetTag(Tags.SpanKind, SpanKinds.Client);
                span.SetTag(ElasticsearchMethodKey, httpMethod.ToString());
                span.SetTag(ElasticsearchPathKey, path.ToString());

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(integrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        public static bool ShouldTagStatement(Span span)
        {
            if (span == null || !Tracer.Instance.Settings.TagElasticsearchQueries)
            {
                return false;
            }

            string operationName = span.OperationName;
            if (operationName.Contains("ChangePassword") ||
                span.OperationName.Contains("PutUser") ||
                span.OperationName.Contains("UserAccessToken"))
            {
                return false;
            }

            return true;
        }

        public static object GetWrittenBytes(object postData)
        {
            return postData?.GetProperty("WrittenBytes").GetValueOrDefault();
        }

        public static bool AttemptWrittenBytes(Span span, object requestData, out object postData, out object writtenBytes)
        {
            postData = null;
            writtenBytes = null;

            if (!ShouldTagStatement(span))
            {
                return false;
            }

            postData = requestData.GetProperty("PostData")
                                  .GetValueOrDefault();
            if (postData == null)
            {
                return false;
            }

            writtenBytes = GetWrittenBytes(postData);
            return true;
        }

        public static object GetConnectionSettings(object requestDataOrTransport)
        {
            return requestDataOrTransport.GetProperty("ConnectionSettings").GetValueOrDefault() ??
                   requestDataOrTransport.GetProperty("Settings").GetValueOrDefault();
        }

        public static MethodInfo GetWriteMethodInfo(string methodName, object postData)
        {
            var postDataType = postData.GetType();
            return postDataType.GetMethod(methodName);
        }

        public static void SetDbStatement(Span span, object writtenBytes)
        {
            string data = System.Text.Encoding.UTF8.GetString((byte[])writtenBytes);
            string statement = data.Length > 1024 ? data.Substring(0, 1024) : data;
            span.SetTag(Tags.DbStatement, statement);
        }

        public static void SetDbStatementFromRequestData(this Span span, object requestData)
        {
            object postData;
            object writtenBytes;
            if (!AttemptWrittenBytes(span, requestData, out postData, out writtenBytes))
            {
                return;
            }

            if (writtenBytes == null)
            {
                object connectionSettings = GetConnectionSettings(requestData);
                var methodInfo = GetWriteMethodInfo("Write", postData);
                using (var stream = new MemoryStream())
                {
                    object[] args = new object[] { stream, connectionSettings };
                    methodInfo.Invoke(postData, args);
                    writtenBytes = stream.ToArray();
                }
            }

            SetDbStatement(span, writtenBytes);
        }

        public static async Task SetDbStatementFromRequestDataAsync(this Span span, object requestData)
        {
            object postData;
            object writtenBytes;
            if (!AttemptWrittenBytes(span, requestData, out postData, out writtenBytes))
            {
                return;
            }

            if (writtenBytes == null)
            {
                object connectionSettings = GetConnectionSettings(requestData);
                var methodInfo = GetWriteMethodInfo("WriteAsync", postData);
                using (var stream = new MemoryStream())
                {
                    object[] args = new object[] { stream, connectionSettings, null };
                    await (Task)(methodInfo.Invoke(postData, args));
                    writtenBytes = stream.ToArray();
                }
            }

            SetDbStatement(span, writtenBytes);
        }

        public static void SetDbStatementFromPostData(this Span span, object postData, object transport)
        {
            if (!ShouldTagStatement(span))
            {
                return;
            }

            object writtenBytes = GetWrittenBytes(postData);

            if (writtenBytes == null)
            {
                var connectionSettings = GetConnectionSettings(transport);
                var methodInfo = GetWriteMethodInfo("Write", postData);
                using (var stream = new MemoryStream())
                {
                    object[] args = new object[] { stream, connectionSettings };
                    methodInfo.Invoke(postData, args);
                    writtenBytes = stream.ToArray();
                }
            }

            SetDbStatement(span, writtenBytes);
        }

        public static async Task SetDbStatementFromPostDataAsync(this Span span, object postData, object transport)
        {
            if (!ShouldTagStatement(span))
            {
                return;
            }

            object writtenBytes = GetWrittenBytes(postData);

            if (writtenBytes == null)
            {
                var connectionSettings = GetConnectionSettings(transport);
                var methodInfo = GetWriteMethodInfo("WriteAsync", postData);
                using (var stream = new MemoryStream())
                {
                    object[] args = new object[] { stream, connectionSettings, null };
                    await (Task)(methodInfo.Invoke(postData, args));
                    writtenBytes = stream.ToArray();
                }
            }

            SetDbStatement(span, writtenBytes);
        }
    }
}
