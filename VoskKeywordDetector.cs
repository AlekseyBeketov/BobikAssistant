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
        private WaveInEvent waveIn;
        private Model voskModel;
        private VoskRecognizer recognizer;
        private string keyword;
        private Action onKeywordDetected;
        private bool isListening;

        public VoskKeywordDetector(string keyword, Action onKeywordDetected)
        {
            this.keyword = keyword;
            this.onKeywordDetected = onKeywordDetected;
            Initialize();
        }

        private void Initialize()
        {
            string modelPath = "D:/Sources/BobikAssistant/BobikAssistant/bin/voiceModels/vosk-model-small-ru-0.22";
            voskModel = new Model(modelPath);
            recognizer = new VoskRecognizer(voskModel, 16000.0f);
            recognizer.SetMaxAlternatives(0);
            recognizer.SetWords(true);

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
                waveIn.StartRecording();
                isListening = true;
            }
        }

        public void StopListening()
        {
            if (isListening)
            {
                waveIn.StopRecording();
                isListening = false;
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
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

        public void Dispose()
        {
            waveIn.Dispose();
            recognizer.Dispose();
            voskModel.Dispose();
        }
    }
}