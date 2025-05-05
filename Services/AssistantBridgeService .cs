using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using AlexAssistant.Models;
using Control;
using CsvHelper;
using Grpc.Core;
using Grpc.Core.Logging;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AlexAssistant.Services
{
    public class AssistantBridgeService : AssistantBridge.AssistantBridgeBase
    {
        static AnimatedAsisstant? animatedasis = null;
        private readonly string baseWeatherUrl;
        private readonly HttpClient _httpClient;
        private readonly string _apiWeatherKey;
        private readonly string _apiGeminiKey;
        string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=";
        string cities = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "worldcities.csv");
        public AssistantBridgeService()
        {
            _apiGeminiKey = App.Configuration["ApiKeys:Gemini"];
            _apiWeatherKey = App.Configuration["ApiKeys:Weather"];
            baseWeatherUrl = "https://api.openweathermap.org/data/2.5/weather";
            geminiUrl += _apiGeminiKey;
            _httpClient = new HttpClient();
        }
        public override async Task<GeminiResponse> GeminiQues(GeminiRequest request, ServerCallContext context)
        {
            try
            {
                string systemPrompt = @"
        You are a voice assistant. Answer very briefly, 1-2 sentences maximum, in a conversational tone.
Don't provide additional information that wasn't asked for. Don't use bullet points, lists.
Answer as if you were talking to a friend. Be clear and specific.
If you don't know the answer, just say ""Sorry, I don't know"" without explaining.
        ";

                string fullPrompt = $"{systemPrompt}\n\nQUESTION: {request.Message}\n\nANSWER:";

                var requestBody = new
                {
                    contents = new[]
                    {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = fullPrompt }
                    }
                }
            },
                    generationConfig = new
                    {
                        temperature = 0.4,
                        maxOutputTokens = 100,
                        topP = 0.95
                    }
                };

                string jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(geminiUrl, content);
                response.EnsureSuccessStatusCode();
                string jsonResponse = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                string resultText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (resultText.Length > 150)
                {
                    var sentences = resultText.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                    if (sentences.Length > 0)
                    {
                        if (sentences.Length > 1)
                            resultText = sentences[0].Trim() + ". " + sentences[1].Trim() + ".";
                        else
                            resultText = sentences[0].Trim() + ".";
                    }
                }

                resultText = resultText.Replace("*", "").Replace("#", "").Replace("-", "").Trim();


                var responseMessage = new GeminiResponse();
                responseMessage.Responses.Add(resultText);

                return responseMessage;
            }
            catch (Exception ex)
            {

                var errorResponse = new GeminiResponse();
                errorResponse.Responses.Add($"Error: {ex.Message}");
                return errorResponse;
            }
        }


        public override Task<CityResponse> GetCity(CityRequest request, ServerCallContext context)
        {
            string cityName = request.City;
            string result = FindCityInCsv(cityName, cities);

            return Task.FromResult(new CityResponse
            {
                FullCityName = result
            });

        }
        private string FindCityInCsv(string cityName, string csvFilePath)
        {
            try
            {
                using (var reader = new StreamReader(csvFilePath, Encoding.UTF8))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<CityOnly>().ToList();

                    var city = records.FirstOrDefault(c => c.city.Equals(cityName, StringComparison.OrdinalIgnoreCase));

                    if (city != null)
                    {
                        return $"City: {city.city}";
                    }
                    else
                    {
                        return $"City {cityName} not found.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error reading the CSV file: {ex.Message}";
            }
        }


        public override async Task<WeatherResponse> GetWeather(CityRequest request, ServerCallContext context)
        {
            string url = $"{baseWeatherUrl}?q={request.City}&appid={_apiWeatherKey}&units=metric";

            try
            {
                Console.WriteLine($"Full URL: {url}");

                HttpResponseMessage response = await _httpClient.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new WeatherResponse
                    {
                        City = request.City,
                        Description = $"Error from weather API: {response.ReasonPhrase}",
                        Temperature = 0
                    };
                }

                var weatherData = JsonDocument.Parse(responseBody);

                var description = weatherData.RootElement.GetProperty("weather")[0].GetProperty("description").GetString();
                var temperature = weatherData.RootElement.GetProperty("main").GetProperty("temp").GetDouble();

                return new WeatherResponse
                {
                    City = request.City,
                    Description = description,
                    Temperature = temperature
                };
            }
            catch (Exception ex)
            {
                return new WeatherResponse
                {
                    City = request.City,
                    Description = $"Error: {ex.Message}",
                    Temperature = 0
                };
            }
        }
        public override Task<Status> ActivateForm(ActivateFormRequest request, ServerCallContext context)
        {
            bool shouldActivate = request.Activate;
            Console.WriteLine($"[gRPC Service] Received ActivateForm request. Activate = {shouldActivate}");
            string message = "";
            bool success = true;

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (shouldActivate)
                    {
                        if (animatedasis == null)
                        {
                            Console.WriteLine("[UI Dispatcher] Creating new AnimatedAsisstant instance.");
                            animatedasis = new AnimatedAsisstant();
                                                                                                               }
                        else
                        {
                            Console.WriteLine("[UI Dispatcher] Using existing AnimatedAsisstant instance.");
                        }

                                                 Console.WriteLine("[UI Dispatcher] Calling Show() and Activate().");
                        animatedasis.Show();                          animatedasis.Activate();                          message = "Form activated and shown.";
                    }
                    else
                    {
                                                 if (animatedasis != null)
                        {
                                                         Console.WriteLine("[UI Dispatcher] Hiding existing AnimatedAsisstant instance.");
                            animatedasis.Hide();                              message = "Form deactivated and hidden.";
                        }
                        else
                        {
                                                         Console.WriteLine("[UI Dispatcher] Hide requested, but no form instance exists or it's already hidden.");
                            message = "Form was already hidden or not created.";
                                                     }
                    }
                });
            }
            catch (Exception ex)
            {
                                 Console.WriteLine($"[ERROR] Exception in ActivateForm Dispatcher: {ex}");
                success = false;
                message = $"Error processing activation request: {ex.Message}";
            }

                         return Task.FromResult(new Status { Success = success, Message = message });
        }


    }

}