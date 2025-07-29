
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpToken;

// Base class for all message types for polymorphism
[JsonConverter(typeof(ChatMessageConverter))]
public abstract record ChatMessage(string role, string? content);

// Concrete message types
public record SystemMessage(string? content) : ChatMessage("system", content);
public record UserMessage(string? content) : ChatMessage("user", content);
public record ToolMessage(string? content, string tool_call_id) : ChatMessage("tool", content);

// Assistant message can contain text content and tool calls
public record AssistantMessage(string? content, List<ToolCall>? tool_calls = null) : ChatMessage("assistant", content);


public class ConversationManager
{
    private static readonly GptEncoding _tokenizer = GptEncoding.GetEncoding("cl100k_base");
    private readonly List<ChatMessage> _history = new List<ChatMessage>();
    private readonly double _compressionThreshold;
    private readonly JsonSerializerOptions _jsonOptions;

    // Using SharpToken for accurate token counting of the entire serialized message.
    private int CurrentHistoryLength => _history.Sum(m => _tokenizer.CountTokens(JsonSerializer.Serialize(m, _jsonOptions)));

    public ConversationManager(JsonSerializerOptions jsonOptions, double compressionThreshold = 1024 * 1000 * 0.75)
    {
        _jsonOptions = jsonOptions;
        _compressionThreshold = compressionThreshold;
    }

    public void AddMessage(ChatMessage message)
    {
        _history.Add(message);
        CompressIfNeeded();
    }

    public List<ChatMessage> GetHistory()
    {
        return new List<ChatMessage>(_history);
    }

    private void CompressIfNeeded()
    {
        if (CurrentHistoryLength <= _compressionThreshold)
        {
            return;
        }

        System.Console.WriteLine($"[上下文管理] 当前 token 数 {CurrentHistoryLength} 已超出阈值 {_compressionThreshold}，开始第一阶段压缩（软压缩）...");

        // Stage 1: Soft Compression - Replace thinking process with a placeholder
        bool softCompressionPerformed = false;
        // We start from the second message, skipping the initial system prompt.
        // We also don't want to compress the very last messages.
        for (int i = 1; i < _history.Count - 2; i++)
        {
            // Find an assistant message that made a tool call
            if (_history[i] is AssistantMessage assistantMessage && assistantMessage.tool_calls != null && assistantMessage.tool_calls.Any())
            {
                // Check if it has not been compressed already
                if (assistantMessage.content != "[思考过程已压缩]")
                {
                    System.Console.WriteLine($"[上下文管理] 软压缩: 压缩了第 {i} 条消息 (助手思考过程).");
                    // Replace the verbose thinking process with a placeholder
                    _history[i] = assistantMessage with { content = "[思考过程已压缩]" };
                    softCompressionPerformed = true;
                }
            }
        }

        if (softCompressionPerformed)
        {
            System.Console.WriteLine($"[上下文管理] 第一阶段压缩完成，当前 token 数: {CurrentHistoryLength}");
        }
        else
        {
            System.Console.WriteLine($"[上下文管理] 无可用的软压缩目标。");
        }


        // If still over threshold, proceed to Stage 2
        if (CurrentHistoryLength > _compressionThreshold)
        {
            System.Console.WriteLine($"[上下文管理] Token 数仍然超标，开始第二阶段压缩（硬压缩）...");

            // Stage 2: Hard Compression - Remove the entire first user turn per user's request.
            // TODO: This is a simple but aggressive strategy. A more advanced approach might involve
            // summarizing the removed turn or using a more nuanced selection process to avoid
            // losing critical context set in the first turn (e.g., a project ID).

            int firstUserMessageIndex = -1;
            int secondUserMessageIndex = -1;

            for (int i = 0; i < _history.Count; i++)
            {
                if (_history[i] is UserMessage)
                {
                    if (firstUserMessageIndex == -1)
                    {
                        firstUserMessageIndex = i;
                    }
                    else
                    {
                        secondUserMessageIndex = i;
                        break;
                    }
                }
            }

            if (firstUserMessageIndex != -1 && secondUserMessageIndex != -1)
            {
                int countToRemove = secondUserMessageIndex - firstUserMessageIndex;
                _history.RemoveRange(firstUserMessageIndex, countToRemove);
                System.Console.WriteLine($"[上下文管理] 硬压缩: 删除了从索引 {firstUserMessageIndex} 开始的 {countToRemove} 条消息 (第一轮对话)。");
            }
            else
            {
                System.Console.WriteLine("[上下文管理] 硬压缩: 未找到足够的用户消息来执行删除操作。");
            }
        }
        System.Console.WriteLine($"[上下文管理] 所有压缩完成，最终 token 数: {CurrentHistoryLength}");
    }
}


// This custom converter is necessary because System.Text.Json cannot deserialize
// into a polymorphic collection (like List<ChatMessage>) out of the box.
// It inspects the 'role' and 'tool_calls' properties to decide which concrete class to create.
public class ChatMessageConverter : JsonConverter<ChatMessage>
{
    public override ChatMessage Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        throw new System.NotImplementedException("Deserialization is not implemented, this converter is for serialization only.");
    }

    public override void Write(Utf8JsonWriter writer, ChatMessage value, JsonSerializerOptions options)
    {
        // We use a custom serialization to handle the different message types
        // This ensures the JSON output is exactly what the vLLM API expects.
        writer.WriteStartObject();

        writer.WriteString("role", value.role);

        if (value.content != null)
        {
            writer.WriteString("content", value.content);
        }
        else
        {
            writer.WriteNull("content");
        }


        if (value is AssistantMessage assistantMessage && assistantMessage.tool_calls != null && assistantMessage.tool_calls.Any())
        {
            writer.WritePropertyName("tool_calls");
            JsonSerializer.Serialize(writer, assistantMessage.tool_calls, options);
        }

        if (value is ToolMessage toolMessage)
        {
            writer.WriteString("tool_call_id", toolMessage.tool_call_id);
        }

        writer.WriteEndObject();
    }
}
