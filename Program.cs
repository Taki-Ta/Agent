using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;




// --- Data Structures for Function Calling ---

// Represents the overall API response from the LLM
public record VllmResponse(string? content, List<Choice> choices);
public record Choice(Message message);
public record Message(string? content, List<ToolCall>? tool_calls);
public record ToolCall(string id, string type, FunctionCall function);
public record FunctionCall(string name, string arguments);

// Represents the result of our local tool execution
public record ToolResultMessage(string role, string content, string tool_call_id);


// --- Tool Definitions ---

public abstract class Tool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract object Parameters { get; }
    public abstract string Execute(string arguments);

    public object GetSchema()
    {
        return new
        {
            type = "function",
            function = new
            {
                name = this.Name,
                description = this.Description,
                parameters = this.Parameters
            }
        };
    }
}

public class ListOutlineTemplatesTool : Tool
{
    public override string Name => "list_outline_templates";
    public override string Description => "当用户想要查找可用的大纲模板时使用，此工具会列出所有可用的大纲模板。";
    public override object Parameters => new { type = "object", properties = new { } }; // No parameters

    public override string Execute(string arguments)
    {
        var templates = new[] { "立项申请报告模板", "市场分析报告模板", "年度总结报告模板" };
        return JsonSerializer.Serialize(templates, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}

public class GetOutlineDetailsTool : Tool
{
    public override string Name => "get_outline_details";
    public override string Description => "根据用户提供的大纲模板名称，获取该模板的具体章节结构。";
    public override object Parameters => new
    {
        type = "object",
        properties = new
        {
            template_name = new { type = "string", description = "用户指定的大纲模板的全名, e.g. '立项申请报告模板'" }
        },
        required = new[] { "template_name" }
    };

    public override string Execute(string arguments)
    {
        try
        {
            var argsDoc = JsonDocument.Parse(arguments);
            if (!argsDoc.RootElement.TryGetProperty("template_name", out var templateNameElement))
            {
                return "错误：缺少参数 'template_name'。";
            }

            string templateName = templateNameElement.GetString() ?? "";
            switch (templateName)
            {
                case "立项申请报告模板":
                    return "1. 项目背景\n2. 项目目标与范围\n3. 市场与用户分析\n4. 技术方案\n5. 风险评估\n6. 预算与排期";
                case "市场分析报告模板":
                    return "1. 市场概述\n2. 目标客户分析\n3. 竞争格局分析\n4. SWOT分析\n5. 市场趋势预测";
                case "年度总结报告模板":
                    return "1. 年度业绩回顾\n2. 关键项目复盘\n3. 团队建设与成长\n4. 明年规划与展望";
                default:
                    return $"错误：未找到名为 '{templateName}' 的模板。";
            }
        }
        catch (JsonException ex)
        {
            return $"错误：解析参数失败 - {ex.Message}";
        }
    }
}


public class Program
{
    private static readonly string VllmApiUrl = "http://10.10.0.203:8000/v1/chat/completions";
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly Dictionary<string, Tool> toolLibrary = new Dictionary<string, Tool>();
    private static readonly List<object> toolSchemas = new List<object>();

    private static JsonSerializerOptions options = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Register tools
        RegisterTool(new ListOutlineTemplatesTool());
        RegisterTool(new GetOutlineDetailsTool());

        Console.WriteLine("您好！我是您的报告生成AI助手（Function Calling版）。");
        Console.WriteLine("您可以随时提出需求，例如：'有哪些报告模板？' 或 '帮我看看立项报告的大纲'");
        Console.WriteLine("--------------------------------------------------------------------");

        var conversationHistory = new List<object>
        {
            new { role = "system", content = "你是一个乐于助人的AI助手。当需要时，你会使用工具来回答用户的问题。" }
        };

        while (true)
        {
            Console.Write("您 > ");
            string userInput = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit") break;

            // Add user's message to history
            conversationHistory.Add(new { role = "user", content = userInput });

            Console.WriteLine("AI 正在思考...");
            var responseMessage = await CallVllmApi(conversationHistory);
            conversationHistory = await handleToolCall(conversationHistory, responseMessage);

        }

        Console.WriteLine("感谢您的使用！");
    }

    private static async Task<List<object>> handleToolCall(List<object> conversation, Message message)
    {
        if (message.tool_calls != null && message.tool_calls.Any())
        {
            // Add AI's decision to call a tool to the history
            conversation.Add(new { role = "assistant", content = message.content, tool_calls = message.tool_calls });
            Console.WriteLine($"[调试信息] AI思考内容: \n {message.content}");
            // Execute the first tool call
            var toolCall = message.tool_calls[0];
            Console.WriteLine($"[调试信息] AI决定调用工具: \n {toolCall.function.name}，参数: {toolCall.function.arguments}");
            Tool toolToExecute = toolLibrary[toolCall.function.name];
            string toolResult = toolToExecute.Execute(toolCall.function.arguments);
            Console.WriteLine($"[调试信息] 工具执行结果: \n {toolResult}");
            // Add tool result to history
            conversation.Add(new { role = "tool", content = toolResult });
            // Call LLM again with updated conversation
            message = await CallVllmApi(conversation);
            conversation.Add(new { role = "assistant", content = message.content });
            Console.WriteLine($"AI > {message.content}");
            conversation = await handleToolCall(conversation, message);
        }
        return conversation;
    }

    private static void RegisterTool(Tool tool)
    {
        toolLibrary.Add(tool.Name, tool);
        toolSchemas.Add(tool.GetSchema());
    }

    private static async Task<Message> CallVllmApi(List<object> messages)
    {
        try
        {
            var requestData = new
            {
                model = "vllm-qwen3-14b",
                messages = messages,
                tools = toolSchemas,
                tool_choice = "auto"
            };

            var jsonPayload = JsonSerializer.Serialize(requestData, options);
            var jsonContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.PostAsync(VllmApiUrl, jsonContent);

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
}