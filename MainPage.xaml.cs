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
// using Pv;
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
		public ObservableCollection<Message> MessageHistory { get; set; } = new ObservableCollection<Message>();

		private VoskKeywordDetector keywordDetector;
        private WaveInEvent ?waveIn;
        private WaveFileWriter writer;

		private Model voskModel;
		private DateTime lastSoundTime;
		private System.Timers.Timer silenceTimer;

		private SpeechSynthesizer synthesizer = new SpeechSynthesizer();
		private List<short> _audioBuffer;
		private const int SilenceTimeout = 1000;

		private bool _isRecording;
        private bool _isMuted;

        private const string BasePrompt = "Отвечай как голосовой ассистент, дружелюбно и по делу, но строго не более 650 символов!";
        private const string Ollama_Url = "http://127.0.0.1:11434/api/chat";

		private static readonly string VoskModelPath = "D:/Sources/BobikAssistant/BobikAssistant/voiceModels/vosk-model-small-ru-0.22";
		private static readonly string StoryFilePath = "D:/Sources/BobikAssistant/BobikAssistant/story.txt";
		private static readonly string ApiUrlMistral = "https://api.mistral.ai/v1/chat/completions";

		private string _fileVoicePath { get; set; }
		public string userInputText { get; set; }
        private string apiKeyMistral { get; set; }

        public MainPage()
        {
            string envFilePath = Path.Combine(AppContext.BaseDirectory, ".env");
            Env.Load(envFilePath);
            apiKeyMistral = Environment.GetEnvironmentVariable("API_KEY_MISTRAL") ?? string.Empty;
            InitializeComponent();
            _isRecording = false;

            _audioBuffer = new List<short>();

            string folderPath = Path.Combine(AppContext.BaseDirectory, "voiceRecords");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

			_fileVoicePath = $"{folderPath}/записьГолоса.wav";
            keywordDetector = new VoskKeywordDetector("бобик", OnKeywordDetected);
            keywordDetector.StartListening();
			InitVosk();
            silenceTimer = new System.Timers.Timer(100);
			MessageEntry.Completed += OnSendButtonClicked;
			silenceTimer.Elapsed += OnSilenceTimerElapsed;
			_ = LoadMessageHistoryAsync();
            BindingContext = this;
            MuteButton.Source = (ImageSource)new MuteIconConverter().Convert(_isMuted, typeof(ImageSource), null, null);
        }

        private void DownScroll(object? sender, EventArgs? e)
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

        private void InitVosk()
        {
            /////////////////////////////////////////////
            // ЛЕША У НАС ДВЕ МОДЕЛИ
            /////////////////////////////////////////////

            voskModel = new Model(VoskModelPath);
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

        private void OnSilenceTimerElapsed(object? sender, ElapsedEventArgs? e)
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
			if (_isMuted || synthesizer == null)
				return;

			synthesizer.Rate = 3; // женский голос

			var builder = new PromptBuilder();
			builder.StartVoice(new CultureInfo("ru-RU")); // Устанавливаем русский язык
			builder.AppendText(text);
			builder.EndVoice();

			synthesizer.SpeakAsync(builder); // Асинхронное воспроизведение речи
		}

		private void StopSpeaking()
		{
			if (synthesizer != null)
			{
				synthesizer.SpeakAsyncCancelAll();
			}
		}

		private void OnMuteButtonClicked(object sender, EventArgs e)
		{
			_isMuted = !_isMuted;
			MuteButton.Source = (ImageSource)new MuteIconConverter().Convert(_isMuted, typeof(ImageSource), null, null);

			if (_isMuted)
			{
				StopSpeaking();
			}
		}

		private void OnSendButtonClicked(object? sender, EventArgs? e)
        {
            StopSpeaking();
			string text = MessageEntry.Text;
            if (text != string.Empty)
            {
				Task.Run(() => SendToOllamaAsync(text));
				MessageEntry.Text = string.Empty;
			}
        }

        private void OnRecordButtonClicked(object? sender, EventArgs? e)
        {
            if (_isRecording)
            {
                waveIn?.StopRecording();
                writer?.Dispose();
                DisposeWaveIn();
                silenceTimer.Stop();
                RecordButton.Text = "Начать запись";
                _isRecording = false;

                // Обработка файла с помощью VOSK
                Task.Run(() => ProcessWithVosk(_fileVoicePath));
            }
            else
            {
                DisposeWaveIn();

                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1)
                };

                writer = new WaveFileWriter(_fileVoicePath, waveIn.WaveFormat);
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

        private async Task SendToOllamaAsync(string text)
        {
			MainThread.BeginInvokeOnMainThread(() =>
			{
				MessageHistory.Add(new Message { Role = "user", Content = text });
				DownScroll(null, null);
			});

			var messageHistory = await ReadMessageHistoryAsync();
			messageHistory.Add(new { role = "user", content = $"{BasePrompt}" + text });

			using (HttpClient client = new HttpClient())
			{
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				var requestBody = new
				{
					model = "hf.co/Vikhrmodels/Vikhr-Gemma-2B-instruct-GGUF:Q4_K",
					messages = messageHistory,
					stream = false  // Выключаем потоковый режим
				};

				string jsonRequest = JsonSerializer.Serialize(requestBody);
				var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

				HttpResponseMessage response = await client.PostAsync(Ollama_Url, content);

				if (response.IsSuccessStatusCode)
				{
					string responseText = await response.Content.ReadAsStringAsync();

					var options = new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true // Игнорируем регистр
					};

					var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseText, options);

					if (ollamaResponse != null && ollamaResponse.Message?.Content != null)
					{
						string resultText = ollamaResponse.Message.Content.Replace("\n---\n","");

						MainThread.BeginInvokeOnMainThread(() =>
						{
							MessageHistory.Add(new Message { Role = "assistant", Content = resultText });
							DownScroll(null, null);
						});

						messageHistory.Add(new { role = "assistant", content = resultText });
						await WriteLastMessageAsync(messageHistory);
						SpeakText(resultText);
					}
					else
					{
                        string errorText = "Произошла ошибка, повторите попытку позже";
						messageHistory.Add(new { role = "assistant", content = errorText });
						MainThread.BeginInvokeOnMainThread(() =>
						{
							MessageHistory.Add(new Message { Role = "assistant", Content = errorText });
							DownScroll(null, null);
						});
					}
				}
				else
				{
					string errorText = "Возникла ошибка: " + response.StatusCode;
					messageHistory.Add(new { role = "assistant", content = errorText });

					MainThread.BeginInvokeOnMainThread(() =>
					{
						MessageHistory.Add(new Message { Role = "assistant", Content = errorText });
						DownScroll(null, null);
					});
				}

				InitVosk();
			}
		}

		private async Task SendToMistralAsync(string text)
        {
            List<object> messageHistory = await ReadMessageHistoryAsync();
            using (HttpClient client = new HttpClient())
            {
                // Устанавливаем заголовки
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyMistral);
                // Читаем историю сообщений из файла
                messageHistory = await ReadMessageHistoryAsync();
                messageHistory.Add(new { role = "user", content = $"{BasePrompt}" + text });
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessageHistory.Add(new Message { Role = "user", Content = text });
                    DownScroll(null, null);
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
                HttpResponseMessage response = await client.PostAsync(ApiUrlMistral, content);

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
                        newMessage = newMessage.Replace("**", string.Empty);

                        messageHistory.Add(new { role = "assistant", content = newMessage });
						await WriteLastMessageAsync(messageHistory);
                        // Обновляем историю сообщений в UI
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            MessageHistory.Add(new Message { Role = "assistant", Content = newMessage });
                            DownScroll(null, null); // Вызываем функцию DownScroll
                        });
                        await Dispatcher.DispatchAsync(async () =>
                        {
                            await scroller.ScrollToAsync(0, scroller.ContentSize.Height, true);
                        });

                        SpeakText(newMessage); // Озвучиваем ответ от ИИ
                    }
                    else
                    {
						string errorText = "Ошибка десериализации или нет выбора.";
						messageHistory.Add(new { role = "assistant", content = errorText });

						MainThread.BeginInvokeOnMainThread(() =>
						{
							MessageHistory.Add(new Message { Role = "assistant", Content = errorText });
							DownScroll(null, null);
						});
                    }
                }
                else
                {
					string errorText = "Возникла ошибка: " + response.StatusCode;
					messageHistory.Add(new { role = "assistant", content = errorText });

					MainThread.BeginInvokeOnMainThread(() =>
					{
						MessageHistory.Add(new Message { Role = "assistant", Content = errorText });
						DownScroll(null, null);
					});
                }
                InitVosk();
            }
        }

		private static async Task<List<object>> ReadMessageHistoryAsync()
		{
			if (File.Exists(StoryFilePath))
			{
				string json = await File.ReadAllTextAsync(StoryFilePath);
				return JsonSerializer.Deserialize<List<object>>(json) ?? new List<object>();
			}
			else
			{
				return new List<object>();
			}
		}

		private static async Task WriteLastMessageAsync(List<object> messages)
        {
            // Если сообщений больше 8, оставляем только последние 8
            if (messages.Count > 8)
            {
                messages = messages.Skip(messages.Count - 8).ToList();
            }

            string json = JsonSerializer.Serialize(messages);
            await File.WriteAllTextAsync(StoryFilePath, json);
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
												userInputText = textElement.GetString() ?? string.Empty;
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
                                                    if (userInputText.Contains(keyValue))
                                                    {
                                                        command.Value(StatusLabel);
                                                        break; // WARNING 
                                                    }
                                                }

                                                Task.Run(() => SendToOllamaAsync(userInputText));
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
		private async Task LoadMessageHistoryAsync()
		{
			var messages = await ReadMessageHistoryAsync();

			foreach (var message in messages)
			{
				if (message is JsonElement jsonElement)
				{
					string role = jsonElement.GetProperty("role").GetString() ?? string.Empty;
					string content = jsonElement.GetProperty("content").GetString() ?? string.Empty;

					MainThread.BeginInvokeOnMainThread(() =>
					{
						MessageHistory.Add(new Message { Role = role, Content = content.Replace(BasePrompt, string.Empty) });
					});
				}
			}
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
			private string _displayContent;

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
					DisplayContent = value.Replace(FilterPhrase, string.Empty).Trim() ?? string.Empty;
				}
			}

			public string DisplayContent
			{
				get => _displayContent;
				set
				{
					_displayContent = value;
					OnPropertyChanged(nameof(DisplayContent));
				}
			}

			private const string FilterPhrase = "Отвечай как голосовой ассистент, дружелюбно и по делу, но строго не более 850 символов!";

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

		public class OllamaResponse
		{
			[JsonPropertyName("model")]
			public string Model { get; set; }

			[JsonPropertyName("created_at")]
			public string CreatedAt { get; set; }

			[JsonPropertyName("message")]
			public OllamaMessage Message { get; set; }

			[JsonPropertyName("done_reason")]
			public string DoneReason { get; set; }

			[JsonPropertyName("done")]
			public bool Done { get; set; }

			[JsonPropertyName("total_duration")]
			public long TotalDuration { get; set; }

			[JsonPropertyName("load_duration")]
			public long LoadDuration { get; set; }

			[JsonPropertyName("prompt_eval_count")]
			public int PromptEvalCount { get; set; }

			[JsonPropertyName("prompt_eval_duration")]
			public long PromptEvalDuration { get; set; }

			[JsonPropertyName("eval_count")]
			public int EvalCount { get; set; }

			[JsonPropertyName("eval_duration")]
			public long EvalDuration { get; set; }
		}

		public class OllamaMessage
		{
			[JsonPropertyName("role")]
			public string Role { get; set; }

			[JsonPropertyName("content")]
			public string Content { get; set; }
		}
	}
}
