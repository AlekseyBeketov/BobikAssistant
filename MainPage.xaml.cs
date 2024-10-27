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
using System.Timers;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.Maui.Dispatching;

// using UIKit;

namespace BobikAssistant
{
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        private VoskKeywordDetector keywordDetector;
        private WaveInEvent waveIn;
        private WaveFileWriter writer;
        private bool _isRecording;
        private bool _isSpeaking;
        private string _filePath;

        private List<short> _audioBuffer;

        private Model voskModel;

        private System.Timers.Timer silenceTimer;
        private DateTime lastSoundTime;
        private const int SilenceTimeout = 1500;

        public string text = "";
        private static readonly string storyFilePath = "D:/Sources/BobikAssistant/BobikAssistant/story.txt";
        private string apiKeyMistral = "";

        // Добавляем ObservableCollection для хранения истории сообщений
        public ObservableCollection<Message> MessageHistory { get; set; } = new ObservableCollection<Message>();

        public MainPage()
        {
            string envFilePath = Path.Combine(AppContext.BaseDirectory, ".env");
            Env.Load(envFilePath);
            apiKeyMistral = Environment.GetEnvironmentVariable("API_KEY_MISTRAL");
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
            silenceTimer = new System.Timers.Timer(100);
            silenceTimer.Elapsed += OnSilenceTimerElapsed;
            LoadMessageHistory();
            BindingContext = this;
        }

        private void DownScroll(object sender, EventArgs e)
        {
            // Прокручиваем содержимое ScrollView вниз при открытии страницы
            Dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(200); // Задержка, чтобы контент успел загрузиться
                await scroller.ScrollToAsync(0, scroller.ContentSize.Height, true);
            });
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

        public class Message : INotifyPropertyChanged
        {
            private string _role;
            private string _content;

            [JsonPropertyName("role")]
            public string Role
            {
                get => _role;
                set
                {
                    _role = value;
                    OnPropertyChanged(nameof(Role));
                }
            }

            [JsonPropertyName("content")]
            public string Content
            {
                get => _content;
                set
                {
                    _content = value;
                    OnPropertyChanged(nameof(Content));
                }
            }

            [JsonPropertyName("tool_calls")]
            public object ToolCalls { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
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

            // Проверка уровня звука
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i);
                if (Math.Abs(sample) > 500) // Порог для определения звука
                {
                    lastSoundTime = DateTime.Now;
                }
            }
        }

        private void OnSilenceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isRecording && (DateTime.Now - lastSoundTime).TotalMilliseconds > SilenceTimeout)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnRecordButtonClicked(null, null);
                });
            }
        }

        private void SpeakText(string text)
        {
            _isSpeaking = true;
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

        private void StopSpeaking()
        {
            if (_isSpeaking)
            {
                using (var synthesizer = new SpeechSynthesizer())
                {
                    synthesizer.SpeakAsyncCancelAll();
                }
                _isSpeaking = false;
            }
        }

        private void OnRecordButtonClicked(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                StopSpeaking();
                waveIn.StopRecording();
                writer?.Dispose();
                DisposeWaveIn();
                silenceTimer.Stop();
                // StatusLabel.Text = $"Запись завершена. Файл сохранен: {_filePath}";
                RecordButton.Text = "Начать запись";
                _isRecording = false;

                // Обработка файла с помощью VOSK
                Task.Run(() => ProcessWithVosk(_filePath));
            }
            else
            {
                StopSpeaking();
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
                lastSoundTime = DateTime.Now;
                silenceTimer.Start();
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
        private static readonly string apiUrlMistral = "https://api.mistral.ai/v1/chat/completions";

        List<object> messageHistory = ReadMessageHistory();
        private async Task SendToMistralAsync(string text)
        {
            using (HttpClient client = new HttpClient())
            {
                // Устанавливаем заголовки
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyMistral);
                // Читаем историю сообщений из файла
                messageHistory = ReadMessageHistory();
                messageHistory.Add(new { role = "user", content = "Отвечай как голосовой ассистент, дружелюбно и по делу, но не более 850 символов." + text });
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessageHistory.Add(new Message { Role = "user", Content = text });
                });
                // Создаем тело запроса
                var requestBody = new
                {
                    model = "mistral-large-latest",
                    messages = messageHistory.ToArray()
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
                        string newMessage = chatResponse.Choices[0].Message.Content.ToString();
                        newMessage = newMessage.Replace("**", "");
                        /*
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            StatusLabel.Text = $"{chatResponse.Choices[0].Message.Content}";
                        });
                        */
                        //messageHistory.Add(new { role = "user", content = text });
                        messageHistory.Add(new { role = "assistant", content = newMessage });
                        WriteLastMessage(messageHistory);
                        // Обновляем историю сообщений в UI
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            MessageHistory.Add(new Message { Role = "assistant", Content = newMessage });
                        });
                        Dispatcher.DispatchAsync(async () =>
                        {
                            await scroller.ScrollToAsync(0, scroller.ContentSize.Height, true);
                        });

                        SpeakText(newMessage); // Озвучиваем ответ от ИИ


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

        private static List<object> ReadMessageHistory()
        {
            if (File.Exists(storyFilePath))
            {
                string json = File.ReadAllText(storyFilePath);
                return JsonSerializer.Deserialize<List<object>>(json);
            }
            else
            {
                return new List<object>();
            }
        }

        private static void WriteLastMessage(List<object> messages)
        {
            // Если сообщений больше 8, оставляем только последние 8
            if (messages.Count > 8)
            {
                messages = messages.Skip(messages.Count - 8).ToList();
            }

            string json = JsonSerializer.Serialize(messages);
            File.WriteAllText(storyFilePath, json);
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
                                                text = textElement.GetString();
                                                firstTextFound = true; // Текст найден, больше искать не нужно
                                                /*
                                                MainThread.BeginInvokeOnMainThread(() =>
                                                {
                                                    StatusLabel.Text = $"{text}";
                                                });
                                                */

                                                foreach (var command in OtherCommands.Commands)
                                                {
                                                    string keyValue = command.Key;
                                                    if (text.Contains(keyValue))
                                                    {
                                                        command.Value(StatusLabel);
                                                        break; // WARNING 
                                                    }
                                                }

                                                Task.Run(() => SendToMistralAsync(text));
                                            }
                                        }
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

        // Метод для загрузки истории сообщений из файла
        private void LoadMessageHistory()
        {
            var messages = ReadMessageHistory();
            foreach (var message in messages)
            {
                if (message is JsonElement jsonElement)
                {
                    string role = jsonElement.GetProperty("role").GetString();
                    string content = jsonElement.GetProperty("content").GetString();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        MessageHistory.Add(new Message { Role = role, Content = content });
                    });
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
