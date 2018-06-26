using UnifiedSpeechServicesSTT;
using UnifiedSpeechServicesTTS;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace SpeechBot.WebPortal
{
    public class SpeechWebSocketBehavior : WebSocketBehavior
    {
        private int totalBytes;

        private SRClient _srClient;

        private System.Timers.Timer timer = new System.Timers.Timer();
        /// <summary>
        /// communication timeout timer, refresh on final cognitive results or tts request received, close websocket when time out.
        /// </summary>
        private readonly System.Timers.Timer _connTimeoutTimer = new System.Timers.Timer(60 * 1000);

        public string lastestGuess = "";

        private bool SREnded = false;

        public static readonly bool StreamLog = false;

        public SpeechWebSocketBehavior(Config config) : this(config, null, null)
        {
            this.config = config;
        }

        public SpeechWebSocketBehavior(Config config, Func<Func<string, Task>, string, Task<string>> doQuery = null, Action<Func<string, Task>, string> onMessage = null)
        {
            this.config = config;
            Console.WriteLine("New Behavior Obj.");
            timer.Stop();
//            timer.Elapsed += (sender, e) => Stop().Wait();
            timer.AutoReset = false;
            timer.Interval = 10000;
            timer.Start();
            _connTimeoutTimer.Elapsed += (sender, e) => CloseWebSocket();
            _connTimeoutTimer.AutoReset = false;
            _connTimeoutTimer.Start();
            this.DoQuery = doQuery;
            _OnMessage = onMessage;
            this.StartSRClient(); 
        }

        private void StartSRClient()
        {
            Console.WriteLine("Creating SR Client...");
            this._srClient = new SRClient(config.UnifiedKey, config.UnifiedRegion, config.Locale);
            this._srClient.LinkAction(Conversation_ResponseReceived, OnSRClose);
        }

        public override void OnClose()
        {
            Console.WriteLine("WS Closed");
            _srClient?.Close();
        }

        private async Task Stop()
        {
            timer.Stop();
            this._srClient?.StopRecognition();

            if (!SREnded)
            {
                SREnded = true;
                Console.WriteLine("Sending SR:End");
                if (webSocket.State == WebSocketState.Open)
                {
                    await Send("SR:End");
                }
            }
        }

        public void CloseWebSocket()
        {
            if (webSocket.State == WebSocketState.Open)
            {
                Send("SR:End").Wait();
                try
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "idle", CancellationToken.None).Wait();
                }
                catch (Exception)
                {
                    // See https://github.com/aspnet/AspNetCoreModule/issues/77
                }
            }
        }

        private async Task ReplyAndTTS(string srText)
        {
            if (DoQuery != null && !String.IsNullOrWhiteSpace(srText))
            {
                var ttsText = await DoQuery(Send, srText);

                var tts = new TTSClient(OnTTSData, () => { Console.WriteLine("TTS End"); });

                var sentences = ttsText.Split(new char[] { ';', '。', '？', '?' });
                SendTTSData(tts, sentences);
            }
        }

        private async void SendTTSData(TTSClient tts, string[] sentences)
        {
            foreach (var sentence in sentences)
            {
                await tts.DoTTS(config.UnifiedKey, config.UnifiedRegion, sentence, config.Locale);
            }
        }

        private void OnTTSData(object sender, UnifiedSpeechServicesTTS.GenericEventArgs<System.IO.Stream> e)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                e.EventData.CopyTo(memoryStream);
                var bytes = memoryStream.ToArray();
                if (bytes.Length > 0)
                {
                    if (StreamLog)
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
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
            {
                var text = Encoding.UTF8.GetString(arraySegment.Array ?? throw new InvalidOperationException(), arraySegment.Offset, arraySegment.Count);
                Console.WriteLine("WS Recieved: " + text);
                if (text == "[sr]end")
                {
                    Stop().Wait();
                }
                else if (text == "[sr]begin" || text=="[sr]begin-duplex")
                {
                    lastestGuess = "";
                    SREnded = false;
                    timer.Stop();
                    Console.WriteLine("Creating SRClient");
                    this._srClient.StartRecognition(text == "[sr]begin-duplex");

                    _stop = false;
                }
                else if (text.StartsWith("[tts]"))
                {
                    _connTimeoutTimer.Stop();
                    _connTimeoutTimer.Start();
                    string ttsText = text.Substring(5);
                    var tts = new TTSClient(OnTTSData, () => { Console.WriteLine("TTS End"); });
                    var sentences = ttsText.Split(new char[] { ';', '。', '？', '?' });
                    SendTTSData(tts, sentences);
                }
                else
                {
                    _OnMessage?.Invoke(Send, text);
                }
            }
            else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
            {
                if (StreamLog)
                    Console.WriteLine("WS Recieved (bytes): " + arraySegment.Count);
                if (!SREnded)
                {
                    this._srClient.SendAudio(arraySegment.ToArray(), arraySegment.Count);
                    totalBytes += arraySegment.Count;
                    if (StreamLog)
                        Console.WriteLine("Sent {0}k bytes", totalBytes / 1024.0);
                }
            }
        }

        // private SRClient client;
        private Func<Func<string, Task>, string, Task<string>> DoQuery;
        private Action<Func<string, Task>, string> _OnMessage;

        public Config config { get; }

        public override void OnOpen()
        {
            Console.WriteLine("WS Opened");
        }

        private void Conversation_ResponseReceived(UnifiedSpeechServicesSTT.WebSocketMessageType type, string responseText)
        {
            try
            {
                if (!timer.Enabled)
                {
                    timer.Start();
                }
                if (type == UnifiedSpeechServicesSTT.WebSocketMessageType.FinalResult) // && !SREnded)
                {
                    _connTimeoutTimer.Stop();
                    _connTimeoutTimer.Start();
                    Console.WriteLine("Phrase: " + responseText);
                    lastestGuess = responseText;
                    timer.Stop();
                    Send("[F]" + responseText).Wait();
                    this.ReplyAndTTS(responseText).Wait();
//                    Stop().Wait();
                }
                else if (type == UnifiedSpeechServicesSTT.WebSocketMessageType.PartialResult)
                {
                    // You can get the partial results without waiting for the request to end.
                    Console.WriteLine("Intermediate: " + responseText);
                    timer.Stop();
                    timer.Start();
                    lastestGuess = responseText;
                    Send("[P]" + responseText).Wait();
                }
                else if (type == UnifiedSpeechServicesSTT.WebSocketMessageType.Error)
                {
                    // Send error messages
                    Send("[Error]" + responseText).Wait();
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