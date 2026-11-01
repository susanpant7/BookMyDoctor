using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace MyHospitalAI.Features.Common;

// Logs every HTTP request with its status and how long the response took, every chat hub call (the chat runs over
// one long-lived SignalR connection, so its messages are not separate HTTP requests), and at startup the list of
// endpoints. All under the "RequestTiming" log category, so it can be turned down in appsettings.json.
public static class RequestTiming
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(RequestTiming));
            var timer = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                var path = context.Request.Path + context.Request.QueryString;
                // A WebSocket (the chat hub's connection) "responds" only when it closes.
                if (context.WebSockets.IsWebSocketRequest)
                {
                    logger.LogInformation("WS {Path} closed after {Seconds:F1}s", context.Request.Path, timer.Elapsed.TotalSeconds);
                }
                else
                {
                    logger.LogInformation("{Method} {Path} → {Status} in {Milliseconds:F0} ms",
                        context.Request.Method, path, context.Response.StatusCode, timer.Elapsed.TotalMilliseconds);
                }
            }
        });

    // Writes every mapped endpoint to the log once the app has started, e.g. "GET api/doctors/{id}".
    public static void LogEndpoints(this WebApplication app) =>
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var lines = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint =>
                {
                    var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
                    return $"{(methods is { Count: > 0 } ? string.Join(",", methods) : "ANY"),-7} /{endpoint.RoutePattern.RawText?.TrimStart('/')}";
                })
                .Distinct()
                .OrderBy(line => line[8..], StringComparer.Ordinal)
                .ToList();
            var hubMethods = typeof(Chats.ChatHub).GetMethods()
                .Where(method => method.DeclaringType == typeof(Chats.ChatHub) && method.IsPublic && !method.IsSpecialName)
                .Select(method => method.Name);

            app.Logger.LogInformation("{Count} HTTP endpoints:\n{Endpoints}\nChat hub methods (over /hubs/chat): {HubMethods}",
                lines.Count, string.Join("\n", lines), string.Join(", ", hubMethods));
        });
}

// Times each call to a hub method, e.g. SendMessage, which includes the whole assistant reply.
public sealed class HubTimingFilter : IHubFilter
{
    private readonly ILogger _logger;

    public HubTimingFilter(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(RequestTiming));
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var timer = Stopwatch.StartNew();
        var outcome = "ok";
        try
        {
            return await next(context);
        }
        catch (Exception ex)
        {
            outcome = $"failed ({ex.GetType().Name})";
            throw;
        }
        finally
        {
            _logger.LogInformation("HUB {Hub}.{Method} → {Outcome} in {Milliseconds:F0} ms",
                context.Hub.GetType().Name, context.HubMethodName, outcome, timer.Elapsed.TotalMilliseconds);
        }
    }
}
