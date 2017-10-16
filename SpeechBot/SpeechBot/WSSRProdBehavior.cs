using CognitiveServicesSTT;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechBot.WebPortal
{
    public class WSSRProdBehavior : WebSocketBehavior
    {
        private int totalBytes;

        private System.Timers.Timer timer = new System.Timers.Timer();
        private System.Timers.Timer connTimeoutTimer = new System.Timers.Timer(60 * 1000);

        public string lastestGuess = "";

        private bool SREnded = false;

        public WSSRProdBehavior(Config config) : this(config, null, null)
        {
            this.config = config;
        }

        public WSSRProdBehavior(Config config, Func<Func<string, Task>, string, Task<string>> DoQuery = null, Action<Func<string, Task>, string> OnMessage = null)
        {
            this.config = config;
            Console.WriteLine("New Behavior Obj");
            timer.Stop();
            timer.Elapsed += (sender, e) => Stop().Wait();
            timer.AutoReset = false;
            timer.Interval = 3000;
            timer.Start();
            connTimeoutTimer.Elapsed += (sender, e) =>
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "idle", CancellationToken.None).Wait();
                    }
                    catch (Exception)
                    {
                        // See https://github.com/aspnet/AspNetCoreModule/issues/77
                    }
                }
            };
            connTimeoutTimer.AutoReset = false;
            connTimeoutTimer.Start();
            this.DoQuery = DoQuery;
            _OnMessage = OnMessage;
        }

        public override void OnClose()
        {
            Console.WriteLine("WS Closed");
            if (!SREnded)
            {
                SREnded = true;
                //client?.EndAudio();
            }
        }

        private async Task Stop()
        {
            timer.Stop();

            if (!SREnded)
            {
                SREnded = true;
                //client?.EndAudio();
                Console.WriteLine("Sending SR:End");
                if (webSocket.State == WebSocketState.Open)
                {
                    await Send("SR:End");
                    if (DoQuery != null && !String.IsNullOrWhiteSpace(lastestGuess))
                    {
                        var ttsText = await DoQuery(Send, lastestGuess);
                        lastestGuess = "";

                        var tts = new TTSClient(OnTTSData, () => { Console.WriteLine("TTS End"); });

                        var sentences = ttsText.Split(new char[] { ';', '。', '？', '?' });
                        SendTTSData(tts, sentences);
                    }
                }
            }
        }

        private async void SendTTSData(TTSClient tts, string[] sentences)
        {
            foreach (var sentence in sentences)
            {
                await tts.DoTTS(config, sentence);
            }
        }

        private void OnTTSData(object sender, CognitiveServicesTTS.GenericEventArgs<System.IO.Stream> e)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                e.EventData.CopyTo(memoryStream);
                var bytes = memoryStream.ToArray();
                if (bytes.Length > 0)
                {
                    Console.WriteLine("TTS WS Sending " + bytes.Length + " bytes");
                    Send(bytes).Wait();
                }
            }
            //Sessions.CloseSession(ID);
        }

        public override void OnError(Exception e)
        {
            Console.WriteLine("WS Error: " + e.Message);
            Console.WriteLine(e);
        }

        private bool _stop = false;

        public override void OnMessage(WebSocketReceiveResult result, ArraySegment<byte> arraySegment)
        {
            connTimeoutTimer.Stop();
            connTimeoutTimer.Start();
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
            {
                var text = Encoding.UTF8.GetString(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
                Console.WriteLine("WS Recieved: " + text);
                if (text == "end")
                {
                    Stop().Wait();
                }
                else if (text == "begin")
                {
                    lastestGuess = "";
                    SREnded = false;
                    timer.Stop();
                    Console.WriteLine("Creating SRClient");
                    client = new SRClient();
                    client.DoSR(config.srKey, Conversation_ResponseReceived, OnSRClose, config.srLocale, config.srEndpoint);

                    _stop = false;
                }
                else
                {
                    _OnMessage?.Invoke(Send, text);
                }
            }
            else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
            {
                Console.WriteLine("WS Recieved (bytes): " + arraySegment.Count);
                if (!SREnded)
                {
                    client.SendAudio(arraySegment.ToArray(), arraySegment.Count);
                    totalBytes += arraySegment.Count;
                    Console.WriteLine("Sent {0}k bytes", totalBytes / 1024.0);
                }
            }
        }

        private SRClient client;
        private Func<Func<string, Task>, string, Task<string>> DoQuery;
        private Action<Func<string, Task>, string> _OnMessage;

        public Config config { get; }

        public override void OnOpen()
        {
            Console.WriteLine("WS Opened");
        }

        private void Conversation_ResponseReceived(CognitiveServicesSTT.WebSocketMessageType type, string responseText)
        {
            try
            {
                if (!timer.Enabled)
                {
                    timer.Start();
                }
                if (type == CognitiveServicesSTT.WebSocketMessageType.FinalResult && !SREnded)
                {
                    Console.WriteLine("Phrase: " + responseText);
                    lastestGuess = responseText;
                    timer.Stop();
                    Send("[F]" + responseText).Wait();
                    Stop().Wait();
                }
                else if (type == CognitiveServicesSTT.WebSocketMessageType.PartialResult)
                {
                    // You can get the partial results without waiting for the request to end.
                    Console.WriteLine("Intermediate: " + responseText);
                    timer.Stop();
                    timer.Start();
                    lastestGuess = responseText;
                    Send("[P]" + responseText).Wait();
                }
                else
                {
                    Console.WriteLine("Unknown: " + responseText);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void OnSRClose()
        {
            Console.WriteLine("SR closed");
        }
    }
}