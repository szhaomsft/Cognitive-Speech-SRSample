using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace UnifiedSpeechServicesSTT
{
    public enum WebSocketMessageType
    {
        Error,
        PartialResult,
        FinalResult
    }

    public class SRClient
    {
        public enum SRClientStatus
        {
            Idle,
            WorkingOnce,
            WorkingAlways
        };
        private const int CutoffSeconds = 60;
        private readonly string _subscriptionKey;
        private readonly string _region;
        private readonly string _locale;
        private SpeechFactory _factory;
        private SpeechRecognizer _recognizer;
        public SRClientStatus Status { get; private set; }
        private SendMessageAction _sendMessage;
        private Action _onClose;
        private readonly int _id;
        private readonly ProducerConsumerStream _interMemoryStream;
        private readonly AudioInputStreamFormat _audioInputStreamFormat;
        private readonly BinaryAudioStreamReader _binaryAudioStreamReader;

        public SRClient()
        {
            this._id = new Random().Next(0, 10000);
        }

        public SRClient(string subscriptionKey, string region = "westus", string locale = "zh-cn") : this()
        {
            // audio input stream format
            _audioInputStreamFormat.AvgBytesPerSec = 16000 * 2;
            _audioInputStreamFormat.Channels = 1;
            _audioInputStreamFormat.SamplesPerSec = 16000;
            _audioInputStreamFormat.FormatTag = 1;
            _audioInputStreamFormat.BitsPerSample = 16;
            _audioInputStreamFormat.BlockAlign = 2;
            _interMemoryStream = new ProducerConsumerStream(8 * 1000);
            _binaryAudioStreamReader = new BinaryAudioStreamReader(_audioInputStreamFormat, _interMemoryStream);
            this._subscriptionKey = subscriptionKey;
            this._region = region;
            this._locale = locale;
            this.CreateFactory();
        }

        private void CreateFactory()
        {
            if (string.IsNullOrEmpty(this._subscriptionKey) || string.IsNullOrEmpty(this._region))
            {
                Console.WriteLine("Creating speech factory error: invalid key or region.");
                return;
            }
            else
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Creating speech factory with key of {0} and region of {1}.", this._subscriptionKey, this._region));
                this._factory = SpeechFactory.FromSubscription(_subscriptionKey, _region);
            }
        }

        private void CreateRecognizer()
        {
            // direct input from mic, for test only
            //            this.recognizer = this.factory.CreateSpeechRecognizer(this.locale);
            // input from stream
            this._recognizer = this._factory.CreateSpeechRecognizerWithStream(_binaryAudioStreamReader, _locale);
        }

        private void Restart()
        {
            var status = this.Status;
            this.Close();
            this.CreateFactory();
            this.StartRecognition(status == SRClientStatus.WorkingAlways);
        }

        public void WriteLine(string s)
        {
            Console.WriteLine("SRClient" + _id + ": " + s);
        }

        public void LinkAction(SendMessageAction onResult, Action onClose)
        {
            this._sendMessage = onResult;
            this._onClose = onClose;
        }

        public bool IsClosed { get; private set; }

        private static void OnTimerCallback(object target)
        { }

        public void SendAudio(byte[] buffer, int bytesRead)
        {
            if (Status == SRClientStatus.WorkingOnce || Status == SRClientStatus.WorkingAlways)
            {
                this._interMemoryStream.WriteAsync(buffer, 0, bytesRead);
            }
        }

        public void EndAudio()
        {
        }

        public void DoSR(string subscriptionKey, SendMessageAction onResult, Action onClose, string locale, string endpoint = null, string region = "westus")
        {
            this._sendMessage = onResult;
            this._onClose = onClose;

            WriteLine("client created");
        }

        public delegate void SendMessageAction(WebSocketMessageType type, string s = "");

        public void Close()
        {
            this.StopRecognition().ConfigureAwait(false);
            // dispose recognizer
            try
            {
                this._recognizer.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// Start Recognition
        /// </summary>
        /// <param name="continuous">boolean, indicate if it is a continous task</param>
        public void StartRecognition(bool continuous = false)
        {
            if (this.Status != SRClientStatus.Idle)
            {
                this.StopRecognition().ConfigureAwait(false);
                Console.WriteLine("Warning: new recognition starts before last one ends.");
            }

            _interMemoryStream.Reset();

            this.CreateRecognizer();

            // connect handlers
            this._recognizer.IntermediateResultReceived += this.OnIntermediatedResultReceived;
            this._recognizer.FinalResultReceived += this.OnFinalResultReceivedHandler;
            this._recognizer.OnSessionEvent += OnSessionEventHandler;
            this._recognizer.RecognitionErrorRaised += this.OnErrorHandler;


            // starts recognition
            if (continuous)
            {
                Console.WriteLine("Creating continuous recognizer");
                this._recognizer.StartContinuousRecognitionAsync();
                this.Status = SRClientStatus.WorkingAlways;
            }
            else
            {
                Console.WriteLine("Creating single recognizer");
                this._recognizer.RecognizeAsync();
                this.Status = SRClientStatus.WorkingOnce;
            }
        }

        public async Task StopRecognition()
        {
            this._interMemoryStream.CompleteAdding();
            // stop continuous recognition
            if (this.Status == SRClientStatus.WorkingAlways)
            {
                await this.StopContinuousRecognition();
            }

            // delete handlers
            try
            {
                this._recognizer.IntermediateResultReceived -= this.OnIntermediatedResultReceived;
                this._recognizer.FinalResultReceived -= this.OnFinalResultReceivedHandler;
                this._recognizer.OnSessionEvent -= OnSessionEventHandler;
                this._recognizer.RecognitionErrorRaised -= this.OnErrorHandler;
            }
            catch (Exception)
            {
                // ignored
            }
            // change state
            this.Status = SRClientStatus.Idle;

            this._recognizer?.Dispose();
        }

        public async Task StopContinuousRecognition()
        {
            this.Status = SRClientStatus.Idle;
            await this._recognizer.StopContinuousRecognitionAsync();
        }

        private void OnIntermediatedResultReceived(object sender, SpeechRecognitionResultEventArgs e)
        {
            Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "Speech recognition: Intermediate result: {0} ", e.ToString()));
            this._sendMessage(WebSocketMessageType.PartialResult, e.Result.Text);
        }

        private void OnFinalResultReceivedHandler(object sender, SpeechRecognitionResultEventArgs e)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Speech recognition: Final result: {0} ", e.ToString()));
            if (e.Result.RecognitionStatus == RecognitionStatus.Recognized && !string.IsNullOrEmpty(e.Result.Text))
            {
                if (this.Status == SRClientStatus.WorkingOnce)
                {
                    this.StopRecognition();
                    this.Status = SRClientStatus.Idle;
                }
                this._sendMessage(WebSocketMessageType.FinalResult, e.Result.Text);
            }

        }

        private void OnErrorHandler(object sender, RecognitionErrorEventArgs e)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Speech recognition: Error information: {0} ", e.ToString()));
            //            this._factory = null;
            //            Restart();
            this._sendMessage(WebSocketMessageType.Error, e.FailureReason);
        }

        private void OnSessionEventHandler(object sender, SessionEventArgs e)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Speech recognition: Session event: {0} ", e.ToString()));
            if (e.EventType == SessionEventType.SessionStoppedEvent && this.Status != SRClientStatus.Idle)
            {
                this.StartRecognition(this.Status == SRClientStatus.WorkingAlways);
            }
        }
    }
}