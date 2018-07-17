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
                        else if (url == "/speech")
                        {
                            Config config = Config.FromQuery(req.Query);
                            bool useTuling = config.Locale == "zh-cn" && !String.IsNullOrEmpty(config.TulingKey);
                            await WSWork(context, webSocket, new SpeechWebSocketBehavior(config, async (send, query) =>
                            {
                                string resp;
                                if (useTuling)
                                {
                                    resp = await GetTulingResult(query, config.TulingKey);
                                }
                                else
                                {
                                    resp = "Echo: " + query;
                                }
                                await send("BOT:" + resp);
                                return resp;
                            }));
                        }
                        else if (url == "/sr")
                        {
                            Config config = Config.FromQuery(req.Query);
                            await WSWork(context, webSocket, new SpeechWebSocketBehavior(config));
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
                                bool useTuling = config.Locale == "zh-cn" && !String.IsNullOrEmpty(config.TulingKey);
                                using (var sr = new StreamReader(req.Body, Encoding.UTF8))
                                {
                                    string query = await sr.ReadToEndAsync();
                                    string resp;
                                    if (useTuling)
                                    {
                                        resp = await GetTulingResult(query, config.TulingKey);
                                    }
                                    else
                                    {
                                        resp = "Echo: " + query;
                                    }
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

        private static async Task<string> GetTulingResult(string query, string tulingKey)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://www.tuling123.com/openapi/api");
            httpWebRequest.ContentType = "application/json; charset=utf-8";
            httpWebRequest.Method = "POST";

            string json = $@"{{
""key"": ""{tulingKey}"",
  ""info"": ""{query}"",
  ""loc"": ""北京市中关村"",
  ""userid"": ""Your Tuling ID""
}}";
            var botstream = httpWebRequest.GetRequestStream();
            var writeBuf = Encoding.UTF8.GetBytes(json);
            botstream.Write(writeBuf, 0, writeBuf.Length);

            HttpWebResponse httpResponse = (HttpWebResponse)await httpWebRequest.GetResponseAsync();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var tulingResp = await streamReader.ReadToEndAsync();
                var tulingRespObj = JsonConvert.DeserializeObject<TulingResponse>(tulingResp);
                return tulingRespObj.text;
            }
        }
    }

    internal class TulingResponse
    {
        public string code;
        public string text;
    }

    public class Config
    {

        // Note: new unified SpeechService API key and issue token uri is per region

        // The way to get api key:
        // Free: https://azure.microsoft.com/en-us/try/cognitive-services/?api=speech-services
        // Paid: https://go.microsoft.com/fwlink/?LinkId=872236&clcid=0x409 


        public string UnifiedKey = "Your Unified Key";
        public string UnifiedRegion = "westus";
        public string Locale = "zh-cn";
        public string TulingKey = "Your Tuling Keys";

        internal static Config FromQuery(IQueryCollection query)
        {
            var value = new Config();
            if (query.TryGetValue("unified-key", out var unifiedKey))
            {
                value.UnifiedKey = unifiedKey.Count > 0 ? unifiedKey[0] : "";
            }
            if (query.TryGetValue("unified-region", out var unifiedRegion))
            {
                value.UnifiedRegion = unifiedRegion.Count > 0 ? unifiedRegion[0] : "";
            }
            if (query.TryGetValue("locale", out var locale))
            {
                value.Locale = locale.Count > 0 ? locale[0] : "";
            }
            if (query.TryGetValue("tuling-key", out var tulingKeys))
            {
                value.TulingKey = tulingKeys.Count > 0 ? tulingKeys[0].ToLower() : "";
            }
            Console.WriteLine(value.UnifiedKey);
            return value;
        }
    }
}