// Modified by SignalFx
using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Traces an Elasticsearch pipeline
    /// </summary>
    public static class ElasticsearchNet7Integration
    {
        private const string IntegrationName = "ElasticsearchNet7";
        private const string Version7 = "7";
        private const string ElasticsearchAssemblyName = "Elasticsearch.Net";

        private const string TransportInterfaceTypeName = "Elasticsearch.Net.ITransport";
        private const string HttpMethodTypeName = "Elasticsearch.Net.HttpMethod";
        private const string PostDataTypeName = "Elasticsearch.Net.PostData";
        private const string RequestParametersInterfaceTypeName = "Elasticsearch.Net.IRequestParameters";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(ElasticsearchNet7Integration));

        /// <summary>
        /// Traces a synchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="transport">The transport for the original method</param>
        /// <param name="httpMethod">The request http method</param>
        /// <param name="path">The request path</param>
        /// <param name="postData">The post data</param>
        /// <param name="requestParameters">The request parameters</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = ElasticsearchAssemblyName,
            TargetAssembly = ElasticsearchAssemblyName,
            TargetType = TransportInterfaceTypeName,
            TargetSignatureTypes = new[] { "T", HttpMethodTypeName, ClrNames.String, PostDataTypeName, RequestParametersInterfaceTypeName },
            TargetMinimumVersion = Version7,
            TargetMaximumVersion = Version7)]
        public static object Request<TResponse>(
            object transport,
            object httpMethod,
            object path,
            object postData,
            object requestParameters,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            Console.WriteLine("REQUEST!!");

            const string methodName = nameof(Request);
            Console.WriteLine($"methodName: {methodName}");
            Func<object, object, object, object, object, TResponse> request;
            Console.WriteLine("initialized request.");
            var transportType = transport.GetType();
            Console.WriteLine($"transportType: {transportType}");
            var genericArgument = typeof(TResponse);
            Console.WriteLine($"genericArgument: {genericArgument}");

            Console.WriteLine($"transport: {transport}");
            // Console.WriteLine($"httpMethod: {httpMethod}");
            Console.WriteLine($"path: {path}");
            Console.WriteLine($"postData: {postData}");
            Console.WriteLine($"requestParameters: {requestParameters}");

            try
            {
                var builder = MethodBuilder<Func<object, object, object, object, object, TResponse>>.Start(moduleVersionPtr, mdToken, opCode, methodName);
                Console.WriteLine($"builder: {builder}");
                var withConcreteType = builder.WithConcreteType(transportType);
                Console.WriteLine($"withConcreteType: {withConcreteType}");
                var withDeclaringTypeGenerics = withConcreteType.WithDeclaringTypeGenerics(genericArgument);
                Console.WriteLine($"withDeclaringTypeGenerics: {withDeclaringTypeGenerics}");
                var withParameters = withDeclaringTypeGenerics.WithParameters(null, path, postData, requestParameters);
                // var withMethodGenerics = withConcreteType.WithMethodGenerics(genericArgument);
                // Console.WriteLine($"withMethodGenerics: {withMethodGenerics}");
                // var withParameters = withMethodGenerics.WithParameters(null, path, postData, requestParameters);
                Console.WriteLine($"withParameters: {withParameters}");
                var withNamespaceAndFilters = withParameters.WithNamespaceAndNameFilters(
                            ClrNames.Ignore,
                            HttpMethodTypeName,
                            ClrNames.String,
                            PostDataTypeName,
                            RequestParametersInterfaceTypeName);
                Console.WriteLine($"withNamespaceAndFilters: {withNamespaceAndFilters}");
                request = withNamespaceAndFilters.Build();
                Console.WriteLine($"request: {request}");
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: TransportInterfaceTypeName,
                    methodName: methodName,
                    instanceType: transport.GetType().AssemblyQualifiedName);
                throw;
            }

            Console.WriteLine("Calling CreateRequestScope");
            using (var scope = ElasticsearchNetCommon.CreateRequestScope(Tracer.Instance, IntegrationName, httpMethod, path, requestParameters))
            {
                try
                {
                    Console.WriteLine("Calling request");
                    // var returned = request(transport, httpMethod, path, postData, requestParameters);
                    var returned = request(transport, null, path, postData, requestParameters);
                    Console.WriteLine($"returned {returned}");
                    scope?.Span.SetDbStatementFromPostData(postData, transport);
                    Console.WriteLine("Returning");
                    return returned;
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="transport">The transport for the original method</param>
        /// <param name="httpMethod">The request http method</param>
        /// <param name="path">The request path</param>
        /// <param name="cancellationTokenSource">A cancellation token</param>
        /// <param name="postData">The post data</param>
        /// <param name="requestParameters">The request parameters</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = ElasticsearchAssemblyName,
            TargetAssembly = ElasticsearchAssemblyName,
            TargetType = TransportInterfaceTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", HttpMethodTypeName, ClrNames.String, ClrNames.CancellationToken, PostDataTypeName, RequestParametersInterfaceTypeName },
            TargetMinimumVersion = Version7,
            TargetMaximumVersion = Version7)]
        public static object RequestAsync<TResponse>(
            object transport,
            object httpMethod,
            object path,
            object cancellationTokenSource,
            object postData,
            object requestParameters,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Console.WriteLine("REQUESTASYNC!!");
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            return RequestAsyncInternal<TResponse>(transport, httpMethod, path, cancellationToken, postData, requestParameters, opCode, mdToken, moduleVersionPtr);
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="transport">The transport for the original method</param>
        /// <param name="httpMethod">The request http method</param>
        /// <param name="path">The request path</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <param name="postData">The post data</param>
        /// <param name="requestParameters">The request parameters</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original result</returns>
        private static async Task<TResponse> RequestAsyncInternal<TResponse>(
            object transport,
            object httpMethod,
            object path,
            CancellationToken cancellationToken,
            object postData,
            object requestParameters,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = "RequestAsync";
            Func<object, object, object, CancellationToken, object, object, Task<TResponse>> requestAsync;
            var transportType = transport.GetType();
            var genericArgument = typeof(TResponse);

            try
            {
                requestAsync =
                    MethodBuilder<Func<object, object, object, CancellationToken, object, object, Task<TResponse>>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(transportType)
                       .WithMethodGenerics(genericArgument)
                       .WithParameters(httpMethod, path, cancellationToken, postData, requestParameters)
                       .WithNamespaceAndNameFilters(
                            ClrNames.GenericTask,
                            HttpMethodTypeName,
                            ClrNames.String,
                            ClrNames.CancellationToken,
                            PostDataTypeName,
                            RequestParametersInterfaceTypeName)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: TransportInterfaceTypeName,
                    methodName: methodName,
                    instanceType: transportType.AssemblyQualifiedName);
                throw;
            }

            using (var scope = ElasticsearchNetCommon.CreateRequestScope(Tracer.Instance, IntegrationName, httpMethod, path, requestParameters))
            {
                try
                {
                    var returned = await requestAsync(transport, httpMethod, path, cancellationToken, postData, requestParameters).ConfigureAwait(false);
                    if (scope != null)
                    {
                        await scope.Span.SetDbStatementFromPostDataAsync(postData, transport);
                    }

                    return returned;
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }
    }
}
