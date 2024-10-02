using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NAudio.Wave;
using Pv;
using Vosk;
using System.Globalization;
using System.Speech.Synthesis;
using DotNetEnv;
// using UIKit;

namespace BobikAssistant
{
    public partial class MainPage : ContentPage
    {
        private VoskKeywordDetector keywordDetector;
        private WaveInEvent waveIn;
        private WaveFileWriter writer;
        private bool _isRecording;
        private string _filePath;

        private List<short> _audioBuffer;

        private Model voskModel;

        public MainPage()
        {
            string envFilePath = Path.Combine(AppContext.BaseDirectory, ".env");
            Env.Load(envFilePath);
            InitializeComponent();
            _isRecording = false;
            _audioBuffer = new List<short>();

            string folderPath = Path.Combine(AppContext.BaseDirectory, "voiceRecords");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            _filePath = $"{folderPath}/записьГолоса.wav";
            keywordDetector = new VoskKeywordDetector("бобик", OnKeywordDetected);
            keywordDetector.StartListening();
            InitVosk();
        }

        private void OnKeywordDetected()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnRecordButtonClicked(null, null);
            });
            keywordDetector.StopListening();
        }


        public class ChatResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("object")]
            public string Object { get; set; }

            [JsonPropertyName("created")]
            public long Created { get; set; }

            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("choices")]
            public Choice[] Choices { get; set; }

            [JsonPropertyName("usage")]
            public Usage Usage { get; set; }
        }

        public class Choice
        {
            [JsonPropertyName("index")]
            public int Index { get; set; }

            [JsonPropertyName("message")]
            public Message Message { get; set; }

            [JsonPropertyName("finish_reason")]
            public string FinishReason { get; set; }

            [JsonPropertyName("logprobs")]
            public object Logprobs { get; set; }
        }

        public class Message
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }

            [JsonPropertyName("tool_calls")]
            public object ToolCalls { get; set; }
        }

        public class Usage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }
        }

        private void InitVosk()
        {
            /////////////////////////////////////////////
            // ЛЕША У НАС ДВЕ МОДЕЛИ
            /////////////////////////////////////////////


            try
            {
                voskModel = new Model("D:/Sources/BobikAssistant/BobikAssistant/bin/voiceModels/vosk-model-small-ru-0.22");
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = $"Ошибка загрузки модели VOSK: {ex.Message}";
                });
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_isRecording && writer != null)
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
            }
        }

        private void SpeakText(string text)
        {
            using (var synthesizer = new SpeechSynthesizer())
            {
                // synthesizer.Rate = 5; // мужской голос
                synthesizer.Rate = 3; // женский голос
                var builder = new PromptBuilder();
                builder.StartVoice(new CultureInfo("ru-RU")); // Устанавливаем русский язык
                builder.AppendText(text);
                builder.EndVoice();
                synthesizer.Speak(builder); // женский голос

                /* МУЖСКОЙ ГОЛОС
                // Создаем MemoryStream для захвата аудиоданных
                using (var memoryStream = new MemoryStream())
                {
                    synthesizer.SetOutputToWaveStream(memoryStream);
                    synthesizer.Speak(builder);

                    // Перенаправляем аудиоданные в микрофон
                    memoryStream.Position = 0;
                    using (var waveOut = new WaveOutEvent())
                    using (var waveProvider = new RawSourceWaveStream(memoryStream, new WaveFormat(16000, 16, 1)))
                    {
                        waveOut.Init(waveProvider);
                        waveOut.Play();

                        while (waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                }
                */
            }
        }

        private void OnRecordButtonClicked(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                waveIn.StopRecording();
                writer?.Dispose();
                DisposeWaveIn();

                StatusLabel.Text = $"Запись завершена. Файл сохранен: {_filePath}";
                (sender as Button).Text = "Начать запись";
                _isRecording = false;

                // Обработка файла с помощью VOSK
                Task.Run(() => ProcessWithVosk(_filePath));
            }
            else
            {
                DisposeWaveIn();

                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1)
                };

                writer = new WaveFileWriter(_filePath, waveIn.WaveFormat);
                waveIn.DataAvailable += OnDataAvailable;

                waveIn.StartRecording();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RecordButton.Text = "Остановить запись";
                });
                _isRecording = true;
            }
        }

        private void DisposeWaveIn()
        {
            if (waveIn != null)
            {
                waveIn.DataAvailable -= OnDataAvailable;
                waveIn.StopRecording();
                waveIn.Dispose();
                waveIn = null;
            }
        }

        // Метод для отправки распознанного текста в Mistral AI
        private static readonly string apiKeyMistral = Environment.GetEnvironmentVariable("API_KEY_MISTRAL");
        private static readonly string apiUrlMistral = "https://api.mistral.ai/v1/chat/completions";

        private async Task SendToMistralAsync(string text)
        {
            using (HttpClient client = new HttpClient())
            {
                // Устанавливаем заголовки
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyMistral);

                // Создаем тело запроса
                var requestBody = new
                {
                    model = "mistral-large-latest",
                    messages = new[]
                    {
                    new { role = "user", content = "Отвечай как голосовой ассистент, дружелюбно и по делу, но не более 850 символов." + text } // Передаем распознанный текст
                }
                };

                // Сериализуем тело запроса в JSON
                string jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Отправляем POST-запрос
                HttpResponseMessage response = await client.PostAsync(apiUrlMistral, content);

                // Проверяем статус ответа
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    var chatResponse = JsonSerializer.Deserialize<ChatResponse>(jsonResponse);

                    // Проверяем, что десериализация прошла успешно
                    if (chatResponse != null && chatResponse.Choices != null && chatResponse.Choices.Length > 0)
                    {
                        // Выводим ответ
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            StatusLabel.Text = $"{chatResponse.Choices[0].Message.Content}";
                        });
                        SpeakText(chatResponse.Choices[0].Message.Content); // Озвучиваем ответ от ИИ
                    }
                    else
                    {
                        Console.WriteLine("Ошибка десериализации или нет выбора.");
                    }
                }
                else
                {
                    Console.WriteLine("Ошибка: " + response.StatusCode);
                }
                InitVosk();
            }
        }

        private async Task ProcessWithVosk(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (voskModel == null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            StatusLabel.Text = "Ошибка: Модель Vosk не была инициализирована.";
                        });
                        return;
                    }

                    // Создаем VoskRecognizer с моделью
                    using (VoskRecognizer rec = new VoskRecognizer(voskModel, 16000.0f))
                    {
                        bool firstTextFound = false;
                        rec.SetMaxAlternatives(0);
                        rec.SetWords(true);

                        using (Stream source = File.OpenRead(filePath))
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead;

                            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                if (rec.AcceptWaveform(buffer, bytesRead))
                                {
                                    var result = rec.Result();

                                    // Проверяем результат, только если первый текст еще не найден
                                    if (!firstTextFound && !string.IsNullOrWhiteSpace(result))
                                    {
                                        using (JsonDocument doc = JsonDocument.Parse(result))
                                        {
                                            JsonElement root = doc.RootElement;
                                            if (root.TryGetProperty("text", out JsonElement textElement) && !string.IsNullOrWhiteSpace(textElement.GetString()))
                                            {
                                                string text = textElement.GetString();
                                                firstTextFound = true; // Текст найден, больше искать не нужно
                                                MainThread.BeginInvokeOnMainThread(() =>
                                                {
                                                    StatusLabel.Text = $"{text}";
                                                });
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var partialResult = rec.PartialResult();
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        StatusLabel.Text = $"{partialResult}";
                                    });
                                }
                            }
                        }

                        if (!firstTextFound)
                        {
                            var finalResult = rec.FinalResult();
                            if (!string.IsNullOrWhiteSpace(finalResult))
                            {
                                using (JsonDocument doc = JsonDocument.Parse(finalResult))
                                {
                                    JsonElement root = doc.RootElement;
                                    if (root.TryGetProperty("text", out JsonElement textElement) && !string.IsNullOrWhiteSpace(textElement.GetString()))
                                    {
                                        string text = textElement.GetString();
                                        MainThread.BeginInvokeOnMainThread(() =>
                                        {
                                            StatusLabel.Text = $"{text}";
                                        });

                                        // ОТПРАВЛЯЕМ ДАННЫЕ MISTRAL AI
                                        Task.Run(() => SendToMistralAsync(text));
                                    }
                                }
                            }
                        }
                        keywordDetector.StartListening();

                    }
                }
                catch (Exception ex)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StatusLabel.Text = $"Ошибка: {ex.Message}";
                    });
                }
            });
        }


    }
}
