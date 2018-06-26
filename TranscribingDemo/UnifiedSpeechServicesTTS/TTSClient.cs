using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnifiedSpeechServicesTTS;

namespace UnifiedSpeechServicesTTS
{
    public class TTSClient
    {
        private System.Action onClose;

        private Synthesize cortana = new Synthesize();

        private static readonly Dictionary<string, string> DefaultVoices = new Dictionary<string, string>
        {
            {"ar-eg", "Microsoft Server Speech Text to Speech Voice (ar-EG, Hoda)"},
            {"de-de", "Microsoft Server Speech Text to Speech Voice (de-DE, Hedda)"},
            {"en-au", "Microsoft Server Speech Text to Speech Voice (en-AU, Catherine)"},
            {"en-ca", "Microsoft Server Speech Text to Speech Voice (en-CA, Linda)"},
            {"en-gb", "Microsoft Server Speech Text to Speech Voice (en-GB, Susan, Apollo)"},
            {"en-in", "Microsoft Server Speech Text to Speech Voice (en-IN, Ravi, Apollo)"},
            {"en-us", "Microsoft Server Speech Text to Speech Voice (en-US, ZiraRUS)"},
            {"es-es", "Microsoft Server Speech Text to Speech Voice (es-ES, Laura, Apollo)"},
            {"es-mx", "Microsoft Server Speech Text to Speech Voice (es-MX, Raul, Apollo)"},
            {"fr-ca", "Microsoft Server Speech Text to Speech Voice (fr-CA, Caroline)"},
            {"fr-fr", "Microsoft Server Speech Text to Speech Voice (fr-FR, Julie, Apollo)"},
            {"it-it", "Microsoft Server Speech Text to Speech Voice (it-IT, Cosimo, Apollo)"},
            {"ja-jp", "Microsoft Server Speech Text to Speech Voice (ja-JP, Ayumi, Apollo)"},
            {"pt-br", "Microsoft Server Speech Text to Speech Voice (pt-BR, Daniel, Apollo)"},
            {"ru-ru", "Microsoft Server Speech Text to Speech Voice (ru-RU, Irina, Apollo)"},
            {"zh-hk", "Microsoft Server Speech Text to Speech Voice (zh-HK, Tracy, Apollo)"},
            {"zh-cn", "Microsoft Server Speech Text to Speech Voice (zh-CN, HuihuiRUS)"},
            {"zh-tw", "Microsoft Server Speech Text to Speech Voice (zh-TW, Yating, Apollo)"}
        };

        public TTSClient(EventHandler<GenericEventArgs<Stream>> onData, System.Action onClose)
        {
            this.onClose = onClose;
            cortana.OnAudioAvailable += onData;
            cortana.OnError += ErrorHandler;
        }

        /// <summary>
        /// Handler an error when a TTS request failed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="GenericEventArgs{Exception}"/> instance containing the event data.</param>
        private void ErrorHandler(object sender, GenericEventArgs<Exception> e)
        {
            Console.WriteLine("Unable to complete the TTS request: [{0}]", e.EventData.Message);
        }

//        public async Task DoTTS(Config config, string text)
        public async Task DoTTS(string apiKey, string region, string text, string locale="zh_cn")
        {
            string requestUri = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
            string issueTokenUri = $"https://{region}.api.cognitive.microsoft.com/sts/v1.0/issueToken";

            string accessToken = "";
            if (!String.IsNullOrEmpty(apiKey))
            {
                Authentication auth = new Authentication(issueTokenUri, apiKey);
                try
                {
                    accessToken = auth.GetAccessToken();
                    Console.WriteLine("Token: {0}\n", accessToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed authentication.");
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine(ex.Message);
                    return;
                }
            }
            Console.WriteLine("Starting TTSSample request code execution.");

            if (DefaultVoices.ContainsKey(locale))
            {
                await cortana.Speak(CancellationToken.None, new Synthesize.InputOptions()
                {
                    RequestUri = new Uri(requestUri),
                    // Text to speak
                    Text = text,
                    VoiceType = Gender.Female,
                    // Refer to the documentation for complete list of supported locales.
                    Locale = locale,
                    // You can also customize the output voice. Refer to the documentation to view the different
                    // voices that the TTS service can output.
                    VoiceName = DefaultVoices[locale],
                    AuthorizationToken = "Bearer " + accessToken,
                    OutputFormat = AudioOutputFormat.Audio16Khz32KBitRateMonoMp3
                });
            }
            onClose();
        }
    }
}