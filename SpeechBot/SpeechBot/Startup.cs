using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechBot.WebPortal
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();
            var logger = loggerFactory.CreateLogger("");

            try
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseDefaultFiles();
                app.UseStaticFiles();
                app.UseWebSockets();

                app.Run(async (context) =>
                {
                    var req = context.Request;
                    var res = context.Response;
                    var url = req.Path;
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        if (url == "/tts")
                        {
                            Config config = Config.FromQuery(req.Query);
                            await WSWork(context, webSocket, new WSTTSBehavior(config));
                        }
                        else if (url == "/sr")
                        {
                            Config config = Config.FromQuery(req.Query);
                            await WSWork(context, webSocket, new WSSRProdBehavior(config, async (Send, query) =>
                            {
                                string resp = "Echo: " + query;
                                await Send("BOT:" + resp);
                                return resp;
                            }));
                        }
                        else
                        {
                            var buffer = new byte[1024 * 1024];
                            try
                            {
                                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                                while (!result.CloseStatus.HasValue)
                                {
                                    await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, false, CancellationToken.None);
                                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                                }
                                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                                Console.WriteLine(e.StackTrace);
                            }
                        }
                    }
                    else
                    {
                        if (req.Method == "GET")
                        {
                        }
                        else
                        {
                            Console.WriteLine(url);
                            if (url.StartsWithSegments("/bot"))
                            {
                                Config config = Config.FromQuery(req.Query);
                                res.ContentType = "application/json; charset=utf-8";
                                using (var sr = new StreamReader(req.Body, Encoding.UTF8))
                                {
                                    string query = await sr.ReadToEndAsync();
                                    string resp = "Echo: " + query;
                                    await res.WriteAsync(resp ?? "");
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                app.Run(async context =>
                 {
                     logger.LogError($"{ex.Message}");

                     context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                     context.Response.ContentType = "text/plain";
                     await context.Response.WriteAsync(ex.Message).ConfigureAwait(false);
                     await context.Response.WriteAsync(ex.StackTrace).ConfigureAwait(false);
                 });
            }
        }

        private async Task WSWork(HttpContext context, WebSocket webSocket, WebSocketBehavior beh)
        {
            var buffer = new byte[1024 * 1024];
            beh.webSocket = webSocket;
            beh.context = context;
            beh.OnOpen();
            try
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                while (!result.CloseStatus.HasValue)
                {
                    beh.OnMessage(result, new ArraySegment<byte>(buffer, 0, result.Count));

                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                beh.OnClose();
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
            catch (Exception e)
            {
                beh.OnError(e);
            }
        }
        
    }
    
    public class Config
    {
        public string srEndpoint = "";
        public string srKey = "fbc7dd7793f941d08f33c26d398831cf";
        public string srLocale = "zh-cn";
        public string ttsEndpoint = "https://speech.platform.bing.com/synthesize";
        public string ttsKey = "7cb4777d82994c9eadffa9698f25f6a2";
        public string ttsLocale = "zh-cn";

        internal static Config FromQuery(IQueryCollection query)
        {
            var value = new Config();
            if (query.TryGetValue("sr-endpoint", out var srEndpoints))
            {
                value.srEndpoint = srEndpoints.Count > 0 ? srEndpoints[0] : "";
            }
            if (query.TryGetValue("sr-key", out var srKeys))
            {
                value.srKey = srKeys.Count > 0 ? srKeys[0] : "";
            }
            if (query.TryGetValue("sr-locale", out var srLocales))
            {
                value.srLocale = srLocales.Count > 0 ? srLocales[0].ToLower() : "";
            }
            if (query.TryGetValue("tts-endpoint", out var ttsEndpoints))
            {
                value.ttsEndpoint = ttsEndpoints.Count > 0 ? ttsEndpoints[0] : "";
            }
            if (query.TryGetValue("tts-key", out var ttsKeys))
            {
                value.ttsKey = ttsKeys.Count > 0 ? ttsKeys[0] : "";
            }
            if (query.TryGetValue("tts-locale", out var ttsLocales))
            {
                value.ttsLocale = ttsLocales.Count > 0 ? ttsLocales[0].ToLower() : "";
            }
            return value;
        }
    }
}