using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Vosk;

namespace BobikAssistant
{
    public class VoskKeywordDetector
    {
        private WaveInEvent ?waveIn;
        private Model voskModel;
        private VoskRecognizer ?recognizer;
        private string keyword;
        private Action onKeywordDetected;
        private bool isListening;
		private static readonly string voskModelPath = "/Users/alexbeketov/Developer/BobikAssistant/BobikAssistant/voiceModels/vosk-model-small-ru-0.22";

		public VoskKeywordDetector(string keyword, Action onKeywordDetected)
        {
            this.keyword = keyword;
            this.onKeywordDetected = onKeywordDetected;
            Initialize();
        }

        private void Initialize()
        {
            if (!Directory.Exists(voskModelPath))
            {
                throw new FileNotFoundException("Model directory does not exist.");
            }
            try
            {
                Console.WriteLine("Loading Vosk model...");
                voskModel = new Model(voskModelPath);
                Console.WriteLine("Vosk model loaded successfully.");

                recognizer = new VoskRecognizer(voskModel, 16000);
                recognizer.SetMaxAlternatives(0);
                recognizer.SetWords(true);
                Console.WriteLine("Vosk recognizer initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during model initialization: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1)
            };

            waveIn.DataAvailable += OnDataAvailable;
        }

        public void StartListening()
        {
            if (!isListening)
            {
                waveIn?.StartRecording();
                isListening = true;
            }
        }

        public void StopListening()
        {
            if (isListening)
            {
                waveIn?.StopRecording();
                isListening = false;
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs? e)
        {
            try
            {
                if (recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                {
                    string result = recognizer.Result();
                    if (result.Contains(keyword))
                    {
                        onKeywordDetected?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing audio data: {ex.Message}");
            }
        }

        public void Dispose()
        {
            waveIn?.Dispose();
            recognizer?.Dispose();
            voskModel.Dispose();
        }
    }
}