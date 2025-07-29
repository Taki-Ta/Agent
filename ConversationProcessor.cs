using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConsoleApp1.Tool;

namespace ConsoleApp1
{
    /// <summary>
    /// 对话处理器 - 处理对话流程和工具调用
    /// </summary>
    public class ConversationProcessor
    {
        private readonly AIService aiService;
        private readonly Dictionary<string, iTool> toolLibrary;
        private readonly ConversationManager conversationManager;

        public ConversationProcessor(AIService aiService, Dictionary<string, iTool> toolLibrary, ConversationManager conversationManager)
        {
            this.aiService = aiService;
            this.toolLibrary = toolLibrary;
            this.conversationManager = conversationManager;
        }

        /// <summary>
        /// 处理用户输入并返回AI响应
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="useStreaming">是否使用流式输出</param>
        public async Task ProcessUserInputAsync(string userInput, bool useStreaming = true)
        {
            conversationManager.AddMessage(new UserMessage(userInput));
            
            Console.WriteLine("AI 正在思考...");
            Message responseMessage;
            if (useStreaming)
            {
                responseMessage=await aiService.CallAsync(conversationManager.GetHistory(), useStreaming, (delta) => Console.Write(delta));
            }
            else
            {
                responseMessage= await aiService.CallAsync(conversationManager.GetHistory(), useStreaming);
            }
            
            //// 首次调用AI（不使用流式，因为可能有工具调用）
            //responseMessage = await aiService.CallAsync(conversationManager.GetHistory(), useStreaming, (delta) => Console.Write(delta));
            
            if (responseMessage.tool_calls != null && responseMessage.tool_calls.Any())
            {
                await HandleToolCallAsync(responseMessage, useStreaming);
            }
            else
            {
                conversationManager.AddMessage(new AssistantMessage(responseMessage.content));
                Console.WriteLine($"AI > {responseMessage.content}");
            }
        }

        /// <summary>
        /// 处理工具调用
        /// </summary>
        /// <param name="message">包含工具调用的消息</param>
        /// <param name="useStreaming">后续响应是否使用流式输出</param>
        private async Task HandleToolCallAsync(Message message, bool useStreaming)
        {
            if (message.tool_calls != null && message.tool_calls.Any())
            {
                // 添加AI决定调用工具的消息到历史
                conversationManager.AddMessage(new AssistantMessage(message.content, message.tool_calls));
                Console.WriteLine($"[调试信息] AI思考内容: \n {message.content}");
                
                // 执行第一个工具调用
                var toolCall = message.tool_calls[0];
                Console.WriteLine($"[调试信息] AI决定调用工具: \n {toolCall.function.name}，参数: {toolCall.function.arguments}");
                
                iTool toolToExecute = toolLibrary[toolCall.function.name];
                string toolResult = toolToExecute.Execute(toolCall.function.arguments);
                Console.WriteLine($"[调试信息] 工具执行结果: \n {toolResult}");
                
                // 添加工具结果到历史
                conversationManager.AddMessage(new ToolMessage(toolResult, toolCall.id));
                
                // 再次调用LLM获取基于工具结果的响应
                Message vllmMessage;
                if (useStreaming)
                {
                    Console.Write("AI > ");
                    vllmMessage = await aiService.CallAsync(
                        conversationManager.GetHistory(), 
                        useStreaming: true, 
                        onDeltaReceived: (delta) => Console.Write(delta)
                    );
                    Console.WriteLine(); // 流式输出完成后换行
                }
                else
                {
                    vllmMessage = await aiService.CallAsync(conversationManager.GetHistory(), useStreaming: false);
                    Console.WriteLine($"AI > {vllmMessage.content}");
                }
                
                conversationManager.AddMessage(new AssistantMessage(vllmMessage.content));
                
                // 检查是否有新的工具调用
                if (vllmMessage.tool_calls != null && vllmMessage.tool_calls.Any())
                {
                    await HandleToolCallAsync(vllmMessage, useStreaming);
                }
            }
            else
            {
                // 如果没有工具调用，直接添加响应到历史
                conversationManager.AddMessage(new AssistantMessage(message.content));
                Console.WriteLine($"AI > {message.content}");
            }
        }
    }
} 