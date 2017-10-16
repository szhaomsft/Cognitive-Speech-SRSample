using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechBot.WebPortal
{
    internal class WSTTSBehavior : WebSocketBehavior
    {
        private Config config;

        public WSTTSBehavior(Config config)
        {
            this.config = config;
        }

        public override void OnClose()
        {
            Console.WriteLine("WS Closed");
        }

        public override void OnError(Exception e)
        {
            Console.WriteLine("WS Error: " + e.Message);
            Console.WriteLine(e);
        }

        public override void OnMessage(WebSocketReceiveResult result, ArraySegment<byte> arraySegment)
        {
            if (result.MessageType == WebSocketMessageType.Text)
            {
                Console.WriteLine("WS Recieved: " + Encoding.UTF8.GetString(arraySegment.Array, arraySegment.Offset, arraySegment.Count));
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                Console.WriteLine("WS Recieved (bytes): " + arraySegment.Count);
            }
        }

        public override void OnOpen()
        {
            Console.WriteLine("WS Opened");
            var ttsText = context.Request.Query["text"][0];

            var tts = new TTSClient(OnTTSData, () => { Console.WriteLine("TTS End"); });

            var sentences = ttsText.Split(new char[] { ';', '。', '？', '?' });
            foreach (var sentence in sentences)
            {
                tts.DoTTS(config, sentence).Wait();
            }
            webSocket.CloseAsync(WebSocketCloseStatus.Empty, "finished", CancellationToken.None).Wait();
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
                    webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, CancellationToken.None).Wait();
                }
            }
        } 
    }
}