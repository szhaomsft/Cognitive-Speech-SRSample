using Microsoft.CognitiveServices.SpeechRecognition;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace CognitiveServicesSTT
{
    public enum WebSocketMessageType
    {
        Error,
        PartialResult,
        FinalResult
    }

    public class SRClient
    {
        private const int CutoffSeconds = 60;
        private DataRecognitionClient dataClient;
        private Timer timer;
        private SendMessageAction SendMessage;
        private Action OnClose;
        private int id;

        public SRClient()
        {
            this.id = new Random().Next(0, 10000);
        }

        public void WriteLine(string s)
        {
            Console.WriteLine("SRClient" + id + ": " + s);
        }

        public bool IsClosed { get; private set; }

        private static void OnTimerCallback(object target)
        {
            SRClient thisObj = target as SRClient;

            System.Diagnostics.Contracts.Contract.Assert(thisObj != null);

            try
            {
                thisObj.WriteLine("timer timeout.");
                thisObj.SendMessage(WebSocketMessageType.Error);
            }
            finally
            {
                thisObj.CloseEverything();
            }
        }

        public void SendAudio(byte[] buffer, int bytesRead)
        {
            if (!this.IsClosed)
            {
                this.dataClient.SendAudio(buffer, bytesRead);
            }
        }

        public void EndAudio()
        {
            this.dataClient.EndAudio();
            this.CloseEverything();
        }

        public void DoSR(string subscriptionKey, SendMessageAction OnResult, Action OnClose, string locale, string endpoint = null)
        {
            this.SendMessage = OnResult;
            this.OnClose = OnClose;

            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClient(SpeechRecognitionMode.ShortPhrase, locale, subscriptionKey, "", endpoint);
            
            // Event handlers for speech recognition results
            this.dataClient.OnResponseReceived += this.OnResponseReceivedHandler;
            this.dataClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;
            this.dataClient.OnConversationError += this.OnConversationErrorHandler;
            WriteLine("client created");

            timer = new Timer(OnTimerCallback, this, CutoffSeconds * 1000, Timeout.Infinite);
        }

        public delegate void SendMessageAction(WebSocketMessageType type, string s = "");

        private void CloseEverything()
        {
            try
            {
                WriteLine("start closing everything");
                if (!this.IsClosed)
                {
                    this.IsClosed = true;
                    if (this.timer != null)
                    {
                        this.timer.Dispose();
                    }

                    this.dataClient.Dispose();
                    this.OnClose();
                    WriteLine("client closed.");
                }
            }
            catch (Exception e)
            {
                WriteLine("CloseEverything failed");
                WriteLine(e.Message);
                WriteLine(e.StackTrace);
            }
        }

        private void OnResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            System.Diagnostics.Contracts.Contract.Requires(e != null);
            System.Diagnostics.Contracts.Contract.Requires(e.PhraseResponse != null);
            if (e.PhraseResponse.Results != null)
            {
                foreach (var result in e.PhraseResponse.Results)
                {
                    this.SendMessage(WebSocketMessageType.FinalResult, result.DisplayText);
                    break;
                }
            }

            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation
                || e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                WriteLine("RecognitionStatus is EndOfDictation.");
                this.SendMessage(WebSocketMessageType.FinalResult);
                this.CloseEverything();
            }
        }

        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {
            WriteLine("RecognitionStatus is PartialResult.");
            this.SendMessage(WebSocketMessageType.PartialResult, e.PartialResult);
        }

        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            WriteLine(string.Format("Error from speech server. {0}:{1}", e.SpeechErrorCode, e.SpeechErrorText));
            this.SendMessage(WebSocketMessageType.Error);
        }
    }
}