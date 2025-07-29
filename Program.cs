using ConsoleApp1;
using ConsoleApp1.Tool;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// Represents the overall API response from the LLM
public record VllmResponse(string? content, List<Choice> choices);
public record Choice(Message message);
public record Message(string? content, List<ToolCall>? tool_calls);
public record ToolCall(string id, string type, FunctionCall function);
public record FunctionCall(string name, string arguments);

// Represents the result of our local tool execution
public record ToolResultMessage(string role, string content, string tool_call_id);

// Represents the streaming API response chunk
public record VllmStreamResponse(List<StreamChoice> choices);
public record StreamChoice(DeltaMessage delta, string? finish_reason);
public record DeltaMessage(string? content, List<ToolCall>? tool_calls);

public class Program
{
    private static readonly string VllmApiUrl = "http://10.10.0.203:8000/v1/chat/completions";
    private static readonly HttpClient httpClient = new HttpClient();
    private static readonly Dictionary<string, iTool> toolLibrary = new Dictionary<string, iTool>();
    private static readonly List<object> toolSchemas = new List<object>();

    public static JsonSerializerOptions options = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // 注册工具
        RegisterAllTools();

        // 初始化AI服务
        var aiService = new AIService(VllmApiUrl, httpClient, options, toolSchemas);

        // 初始化对话管理器
        var conversationManager = new ConversationManager(options);
        conversationManager.AddMessage(new SystemMessage(GetSystemPrompt()));

        // 初始化对话处理器
        var conversationProcessor = new ConversationProcessor(aiService, toolLibrary, conversationManager);

        Console.WriteLine("您好！我是您的报告生成AI助手（Function Calling版）。");
        Console.WriteLine("您可以随时提出需求，例如：'有哪些报告模板？' 或 '帮我看看立项报告的大纲'");
        Console.WriteLine("--------------------------------------------------------------------");

        while (true)
        {
            Console.Write("您 > ");
            string userInput = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit") break;

            // 处理用户输入，默认使用流式输出
            await conversationProcessor.ProcessUserInputAsync(userInput, useStreaming: false);
        }

        Console.WriteLine("感谢您的使用！");
    }

    private static string GetSystemPrompt()
    {
        return @"你是一个专业的AI助手，可以引导用户完成撰写文档的任务。
1. 当需要时，你会使用工具来完成任务。
2. 当目前的信息不满足工具的要求时，可询问、引导用户获取所需的工具参数，禁止自己生成参数（重要）
3. 与用户交谈时，永远不要提及工具名称。 例如，不要说我需要使用工具来查询大纲模板，而只需说我将查询大纲模板。
4. 在调用每个工具之前，首先向用户解释你为什么要调用它。

你拥有完整的记忆管理能力，可以：
- 使用记忆工具存储重要信息（全局变量、大纲、进度、内容等）
- 使用记忆工具查询之前存储的信息
- 使用记忆工具查看所有可用的记忆
- 使用记忆工具清理不需要的记忆

记忆管理的关键点：
1. 每当用户提到项目时，应该设置 currentProjectID 全局变量
2. 生成大纲后，应该保存到 documentOutline
3. 每开始一个新章节时，应该更新 currentChapterName 全局变量
4. 每完成一个章节时，应该保存章节内容到 documentContent，并更新进度
5. 变量替换时，应该从记忆中查询变量的值

撰写文档的步骤如下：
1、生成大纲
    1.1 设置当前项目ID（使用记忆工具）
    1.2 生成大纲可在现有的大纲模板根据用户要求进行修订，也可以从头根据用户信息生成
    1.3 保存大纲结构到记忆（使用记忆工具）
2、循环生成大纲下所有章节内容
    2.1 依此生成大纲下的所有章节，每次只生成一个章节
    2.2 更新当前章节名称到记忆（使用记忆工具）
    2.3 每个章节生成前，应提示用户将要生成章节XXX的内容
    2.4 应该查询是否有语义相近的内容模板，如果有则询问用户是否采用（重要），如果用户选择不采用或没有查到内容模板，则询问用户如何生成（用户填写、AI生成、使用现有知识库内容）
    2.5 内容模板可能返回以下内容：
        2.5.1 Call_XXXTool(参数)   其中：Call_XXXTool应该调用XXX工具；参数为工具XXX的参数。被大括号包裹的内容应该被识别成变量，如CurrentProjectID应替换为从记忆中查询到的值
        2.5.2 嵌套调用 内容模板返回的内容可以嵌套，此时应该先调用内层工具计算出结果作为参数，再调用外层工具
        2.5.3 如果返回没有任何Call_XXXTool或被大括号包裹的内容，则直接使用返回的字符串作为章节内容
    2.6 保存章节内容到记忆（使用记忆工具）
    2.7 更新进度信息到记忆（使用记忆工具）
    2.8 每个章节生成后，应和用户确认目前生成的章节内容。用户确认后，才能生成下一个章节的内容；";
    }

    private static void RegisterAllTools()
    {
        RegisterTool(new ListOutlineTemplatesTool());
        RegisterTool(new GetOutlineDetailsTool());
        RegisterTool(new ListNodeTemplatesTool());
        RegisterTool(new GetKnowledgeTool());
        RegisterTool(new GetAIGenerateTool());

        // 注册记忆管理工具
        RegisterTool(new SetMemoryTool());
        RegisterTool(new GetMemoryTool());
        RegisterTool(new ListMemoryTool());
        RegisterTool(new DeleteMemoryTool());
    }

    private static void RegisterTool(iTool tool)
    {
        toolLibrary.Add(tool.Name, tool);
        toolSchemas.Add(tool.GetSchema());
    }
}