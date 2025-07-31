using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    /// <summary>
    /// AI服务类 - 统一管理所有AI相关功能
    /// </summary>
    public class AIService
    {
        private readonly string apiUrl;
        private readonly HttpClient httpClient;
        private readonly JsonSerializerOptions jsonOptions;
        private readonly List<object> toolSchemas;

        public AIService(string apiUrl, HttpClient httpClient, JsonSerializerOptions options, List<object> toolSchemas)
        {
            this.apiUrl = apiUrl;
            this.httpClient = httpClient;
            this.jsonOptions = options;
            this.toolSchemas = toolSchemas;
        }

        /// <summary>
        /// 统一的AI调用入口方法
        /// </summary>
        /// <param name="messages">对话历史</param>
        /// <param name="useStreaming">是否使用流式输出</param>
        /// <param name="onDeltaReceived">流式输出回调（仅在useStreaming=true时使用）</param>
        /// <param name="enableThinking">是否启用思考模式</param>
        /// <returns>AI响应消息</returns>
        public async Task<Message> CallAsync(
            List<ChatMessage> messages,
            bool useStreaming = false,
            Action<string>? onDeltaReceived = null,
            bool enableThinking = true)
        {
            if (useStreaming && onDeltaReceived != null)
            {
                return await CallStreamingAsync(messages, onDeltaReceived, enableThinking);
            }
            else
            {
                return await CallNonStreamingAsync(messages, enableThinking);
            }
        }

        /// <summary>
        /// 非流式AI调用
        /// </summary>
        private async Task<Message> CallNonStreamingAsync(List<ChatMessage> messages, bool enableThinking)
        {
            try
            {
                var requestData = new
                {
                    model = "vllm-qwen3-14b",
                    messages = messages,
                    tools = toolSchemas,
                    tool_choice = "auto",
                    chat_template_kwargs = new { enable_thinking = enableThinking }
                };

                var jsonPayload = JsonSerializer.Serialize(requestData, jsonOptions);
                var jsonContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await httpClient.PostAsync(apiUrl, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var vllmResponse = await response.Content.ReadFromJsonAsync<VllmResponse>();
                    return vllmResponse.choices[0].message;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    return new Message($"API调用失败: {response.StatusCode}\n错误信息: {errorContent}", null);
                }
            }
            catch (Exception ex)
            {
                return new Message($"发生异常: {ex.Message}", null);
            }
        }

        /// <summary>
        /// 流式AI调用
        /// </summary>
        private async Task<Message> CallStreamingAsync(List<ChatMessage> messages, Action<string> onDeltaReceived, bool enableThinking)
        {
            try
            {
                var requestData = new
                {
                    model = "vllm-qwen3-14b",
                    messages = messages,
                    tools = toolSchemas,
                    tool_choice = "auto",
                    stream = true,
                    chat_template_kwargs = new { enable_thinking = enableThinking }
                };

                var jsonPayload = JsonSerializer.Serialize(requestData, jsonOptions);
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var fullContent = new StringBuilder();
                        var toolCalls = new List<ToolCall>();
                        var toolCallBuilders = new List<(string Id, string Name, StringBuilder ArgumentsBuilder)>();

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream))
                        {
                            while (!reader.EndOfStream)
                            {
                                string line = await reader.ReadLineAsync() ?? "";
                                if (line.StartsWith("data:"))
                                {
                                    string jsonData = line.Substring(5).Trim();
                                    if (jsonData == "[DONE]")
                                    {
                                        break;
                                    }

                                    try
                                    {
                                        var streamResponse = JsonSerializer.Deserialize<VllmStreamResponse>(jsonData, jsonOptions);

                                        if (streamResponse?.choices[0]?.delta?.tool_calls?.Count > 0)
                                        {
                                            foreach (var toolCallChunk in streamResponse.choices[0].delta.tool_calls)
                                            {
                                                if (!string.IsNullOrEmpty(toolCallChunk.function.name))
                                                {
                                                    toolCallBuilders.Add((toolCallChunk.id, toolCallChunk.function.name, new StringBuilder(toolCallChunk.function.arguments)));
                                                }
                                                else if (toolCallBuilders.Count > 0)
                                                {
                                                    toolCallBuilders[toolCallBuilders.Count - 1].ArgumentsBuilder.Append(toolCallChunk.function.arguments);
                                                }
                                            }
                                        }

                                        string? delta = streamResponse?.choices[0]?.delta?.content;
                                        if (!string.IsNullOrEmpty(delta))
                                        {
                                            onDeltaReceived(delta);
                                            fullContent.Append(delta);
                                        }
                                    }
                                    catch (JsonException e)
                                    {
                                        Console.WriteLine($"[JSON Error] Failed to parse stream chunk: {e.Message} | Data: {jsonData}");
                                    }
                                }
                            }
                        }
                        foreach (var builder in toolCallBuilders)
                        {
                            var argument = builder.ArgumentsBuilder.ToString();
                            if (string.IsNullOrWhiteSpace(argument))
                                argument = "{}";
                            var functionCall = new FunctionCall(builder.Name, argument);
                            toolCalls.Add(new ToolCall(builder.Id, "function", functionCall));
                        }

                        return new Message(fullContent.ToString(), toolCalls.Count > 0 ? toolCalls : null);
                    }
                    else
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"API调用失败: {response.StatusCode}\n错误信息: {errorContent}");
                        return new Message($"API调用失败: {response.StatusCode}", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生异常: {ex.Message}");
                return new Message($"发生异常: {ex.Message}", null);
            }
        }
    }
}