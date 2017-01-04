using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WebSocketsDemo.Middlewares
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private ConcurrentBag<WebSocket> _webSocketCollection;

        public WebSocketMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<WebSocketMiddleware>();
            _webSocketCollection = new ConcurrentBag<WebSocket>();
        }

        public async Task Invoke(HttpContext httpContext)
        {
            _logger.LogInformation("Handling request: " + httpContext.Request.Path);

            if (httpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("Added socket " + webSocket + " to collection.");
                _webSocketCollection.Add(webSocket);
                while (webSocket.State == WebSocketState.Open)
                {
                    var token = CancellationToken.None;
                    var buffer = new ArraySegment<byte>(new byte[4096]);
                    var received = await webSocket.ReceiveAsync(buffer, token);

                    switch (received.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            var request = Encoding.UTF8.GetString(buffer.Array,
                                                    buffer.Offset,
                                                    buffer.Count);
                            var type = WebSocketMessageType.Text;
                            var data = Encoding.UTF8.GetBytes(request);
                            buffer = new ArraySegment<byte>(data);

                            foreach (var socket in _webSocketCollection)
                            {
                                if (socket != null && socket.State == WebSocketState.Open)
                                {
                                    await socket.SendAsync(buffer, type, true, token);
                                }
                            }
                            
                            break;
                    }
                }
            }
            else
            {
                await _next.Invoke(httpContext);
            }

            _logger.LogInformation("Finished handling request.");
        }
    }

    public static class RequestLoggerExtensions
    {
        public static IApplicationBuilder UseWebSocketHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<WebSocketMiddleware>();
        }
    }
}