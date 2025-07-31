using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ConsoleApp1.Tool
{
    /// <summary>
    /// 获取所有大纲
    /// </summary>
    public class ListOutlineTemplatesTool : iTool
    {
        public string Name => "list_outline_templates";
        public string Description => "当用户想要查找可用的大纲模板时使用，此工具会列出所有可用的大纲模板。";
        public object Parameters => new { type = "object", properties = new { } }; // No parameters

        public string Execute(string arguments)
        {
            var templates = new[] { "立项申请报告模板", "市场分析报告模板", "年度总结报告模板" };
            return JsonSerializer.Serialize(templates, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }
    }

    /// <summary>
    /// 获取指定大纲的具体内容
    /// </summary>
    public class GetOutlineDetailsTool : iTool
    {
        public string Name => "get_outline_details";
        public string Description => "根据用户提供的大纲模板名称，获取该模板的具体章节结构。";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                template_name = new { type = "string", description = "用户指定的大纲模板的全名, e.g. '立项申请报告模板'" }
            },
            required = new[] { "template_name" }
        };

        public string Execute(string arguments)
        {
            try
            {
                var argsDoc = JsonDocument.Parse(arguments);
                while(argsDoc.RootElement.ValueKind == JsonValueKind.String)
                {
                    var content = argsDoc.RootElement.GetString() ?? "";
                    if (content.Contains('{'))
                        argsDoc = JsonDocument.Parse(content ?? "{}");
                    else
                        break;
                }
                if (!argsDoc.RootElement.TryGetProperty("template_name", out var templateNameElement))
                {
                    return "错误：缺少参数 'template_name'。";
                }

                string templateName = templateNameElement.GetString() ?? "";
                switch (templateName)
                {
                    case "立项申请报告模板":
                        return "1. 背景\n2. 项目目标与范围\n3. 市场与用户分析\n4. 技术方案\n5. 风险评估\n6. 预算与排期";
                    case "市场分析报告模板":
                        return "1. 背景\n2. 概述\n3. 目标客户分析\n4. 竞争格局分析\n5. SWOT分析\n6. 市场趋势预测";
                    case "年度总结报告模板":
                        return "1.概述\n2. 年度业绩回顾\n3. 关键项目复盘\n4. 团队建设与成长\n5. 明年规划与展望";
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


    /// <summary>
    /// 获取指定类型文档的所有内容模板
    /// </summary>
    public class ListNodeTemplatesTool : iTool
    {
        public string Name => "list_node_templates";
        public string Description => "当用户想要编写某一类型文档章节的具体内容时使用，此工具会列出指定类型所有可用的内容模板。";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                template_name = new { type = "string", description = "用户指定的模板类型的全名, e.g. '立项申请报告模板'" }
            },
            required = new[] { "template_name" }
        };

        public string Execute(string arguments)
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
                        return JsonSerializer.Serialize(new List<object>() {
                    new
                    {
                        NodeName = "背景",
                        ContentTemplate = new {
                            content="Call_GetKnowledgeTool({CurrentProjectID},'背景')"
                        }
                    },
                    new
                    {
                        NodeName = "风险评估",
                        ContentTemplate = new {
                            content="Call_GetAIGenerateTool({Call_GetKnowledgeTool({CurrentProjectID},'风险项评估')},{Prompt})"
                        }
                    },new
                    {
                        NodeName = "技术方案",
                        ContentTemplate = @"用户填写"
                    },
                    }, Program.options);
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

    /// <summary>
    /// 获取知识库内容工具
    /// </summary>
    public class GetKnowledgeTool : iTool
    {
        public string Name => "get_knowledge";
        public string Description => "当用户想要获取指定项目的指定章节内容时使用，此工具会列出指定项目指定章节的内容。调用此工具前，应该先和用户确认参数。";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                project_id = new { type = "string", description = "用户指定的项目的ID" },
                chapter_name = new { type = "string", description = "用户指定的章节名称" }
            },
            required = new[] { "project_id", "chapter_name" }
        };

        public string Execute(string arguments)
        {
            try
            {
                var argsDoc = JsonDocument.Parse(arguments);
                if (!argsDoc.RootElement.TryGetProperty("project_id", out var projectIDElement))
                {
                    return "错误：缺少参数 'project_id'。";
                }
                if (!argsDoc.RootElement.TryGetProperty("chapter_name", out var chapterNameElement))
                {
                    return "错误：缺少参数 'chapter_name'。";
                }

                string projectID = projectIDElement.GetString() ?? "";
                string chapterName = chapterNameElement.GetString() ?? "";
                return @$"这是项目【{projectID}】 章节 【{chapterName}】的内容";
            }
            catch (JsonException ex)
            {
                return $"错误：解析参数失败 - {ex.Message}";
            }
        }
    }

    /// <summary>
    /// 记忆管理器 - 处理记忆的持久化存储
    /// </summary>
    public static class MemoryManager
    {
        private static readonly string MemoryFilePath = "memory.json";
        private static Dictionary<string, object> memoryData = new Dictionary<string, object>();

        static MemoryManager()
        {
            LoadMemory();
        }

        public static void LoadMemory()
        {
            try
            {
                if (File.Exists(MemoryFilePath))
                {
                    string jsonContent = File.ReadAllText(MemoryFilePath, Encoding.UTF8);
                    var loadedData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                    if (loadedData != null)
                    {
                        memoryData = loadedData;
                    }
                }
                else
                {
                    // 初始化默认记忆结构
                    memoryData = new Dictionary<string, object>
                    {
                        ["globalVariables"] = new Dictionary<string, string>
                        {
                            ["currentProjectID"] = "",
                            ["currentChapterName"] = ""
                        },
                        ["documentOutline"] = new Dictionary<string, object>
                        {
                            ["chapters"] = new List<string>(),
                            ["template"] = ""
                        },
                        ["progress"] = new Dictionary<string, object>
                        {
                            ["completedChapters"] = new List<string>(),
                            ["currentStep"] = ""
                        },
                        ["documentContent"] = new Dictionary<string, string>(),
                        ["customMemories"] = new Dictionary<string, string>()
                    };
                    SaveMemory();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载记忆失败: {ex.Message}");
                memoryData = new Dictionary<string, object>();
            }
        }

        public static void SaveMemory()
        {
            try
            {
                string jsonContent = JsonSerializer.Serialize(memoryData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(MemoryFilePath, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存记忆失败: {ex.Message}");
            }
        }

        public static void SetMemory(string key, object value)
        {
            memoryData[key] = value;
            SaveMemory();
        }

        public static object? GetMemory(string key)
        {
            return memoryData.TryGetValue(key, out var value) ? value : null;
        }

        public static List<string> GetAllKeys()
        {
            return memoryData.Keys.ToList();
        }

        public static bool DeleteMemory(string key)
        {
            bool removed = memoryData.Remove(key);
            if (removed)
            {
                SaveMemory();
            }
            return removed;
        }

        // 设置全局变量
        public static void SetGlobalVariable(string varName, string value)
        {
            if (memoryData.TryGetValue("globalVariables", out var globalVars) && globalVars is JsonElement element)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(element.GetRawText()) ?? new Dictionary<string, string>();
                dict[varName] = value;
                memoryData["globalVariables"] = dict;
                SaveMemory();
            }
        }

        // 获取全局变量
        public static string GetGlobalVariable(string varName)
        {
            if (memoryData.TryGetValue("globalVariables", out var globalVars))
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(globalVars.ToString() ?? "{}");
                    return dict?.TryGetValue(varName, out var value) == true ? value : "";
                }
                catch
                {
                    return "";
                }
            }
            return "";
        }
    }

    /// <summary>
    /// 设置记忆工具
    /// </summary>
    public class SetMemoryTool : iTool
    {
        public string Name => "set_memory";
        public string Description => "设置或更新记忆内容，支持存储全局变量、大纲信息、进度状态等数据";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                key = new { type = "string", description = "记忆的键名，如 'currentProjectID', 'documentOutline.chapters' 等" },
                value = new { type = "string", description = "要存储的值" },
                category = new { type = "string", description = "记忆类别：globalVariables、documentOutline、progress、documentContent、customMemories", @enum = new[] { "globalVariables", "documentOutline", "progress", "documentContent", "customMemories" } }
            },
            required = new[] { "key", "value", "category" }
        };

        public string Execute(string arguments)
        {
            try
            {
                var argsDoc = JsonDocument.Parse(arguments);
                if (!argsDoc.RootElement.TryGetProperty("key", out var keyElement) ||
                    !argsDoc.RootElement.TryGetProperty("value", out var valueElement) ||
                    !argsDoc.RootElement.TryGetProperty("category", out var categoryElement))
                {
                    return "错误：缺少必要参数 'key', 'value' 或 'category'";
                }

                string key = keyElement.GetString() ?? "";
                string value = valueElement.GetString() ?? "";
                string category = categoryElement.GetString() ?? "";

                switch (category)
                {
                    case "globalVariables":
                        MemoryManager.SetGlobalVariable(key, value);
                        return $"成功设置全局变量 {key} = {value}";
                    case "customMemories":
                        MemoryManager.SetMemory($"customMemories.{key}", value);
                        return $"成功设置自定义记忆 {key} = {value}";
                    default:
                        MemoryManager.SetMemory($"{category}.{key}", value);
                        return $"成功设置记忆 {category}.{key} = {value}";
                }
            }
            catch (JsonException ex)
            {
                return $"错误：解析参数失败 - {ex.Message}";
            }
        }
    }

    /// <summary>
    /// 获取记忆工具
    /// </summary>
    public class GetMemoryTool : iTool
    {
        public string Name => "get_memory";
        public string Description => "根据key查询记忆内容，支持查询全局变量、大纲信息、进度状态等";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                key = new { type = "string", description = "要查询的记忆键名" },
                category = new { type = "string", description = "记忆类别，如果不指定则搜索所有类别", @enum = new[] { "globalVariables", "documentOutline", "progress", "documentContent", "customMemories" } }
            },
            required = new[] { "key" }
        };

        public string Execute(string arguments)
        {
            try
            {
                var argsDoc = JsonDocument.Parse(arguments);
                if (!argsDoc.RootElement.TryGetProperty("key", out var keyElement))
                {
                    return "错误：缺少参数 'key'";
                }

                string key = keyElement.GetString() ?? "";
                string? category = null;

                if (argsDoc.RootElement.TryGetProperty("category", out var categoryElement))
                {
                    category = categoryElement.GetString();
                }

                if (category == "globalVariables")
                {
                    string value = MemoryManager.GetGlobalVariable(key);
                    return string.IsNullOrEmpty(value) ? $"未找到全局变量: {key}" : $"{key} = {value}";
                }

                string searchKey = category != null ? $"{category}.{key}" : key;
                var result = MemoryManager.GetMemory(searchKey);

                if (result != null)
                {
                    return JsonSerializer.Serialize(result, Program.options);
                }

                return $"未找到记忆: {searchKey}";
            }
            catch (JsonException ex)
            {
                return $"错误：解析参数失败 - {ex.Message}";
            }
        }
    }

    /// <summary>
    /// 列出记忆工具
    /// </summary>
    public class ListMemoryTool : iTool
    {
        public string Name => "list_memory";
        public string Description => "列出所有记忆的键名，可以按类别筛选";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                category = new { type = "string", description = "要列出的记忆类别，不指定则列出所有", @enum = new[] { "globalVariables", "documentOutline", "progress", "documentContent", "customMemories" } }
            }
        };

        public string Execute(string arguments)
        {
            try
            {
                string? category = null;
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    var argsDoc = JsonDocument.Parse(arguments);
                    if (argsDoc.RootElement.TryGetProperty("category", out var categoryElement))
                    {
                        category = categoryElement.GetString();
                    }
                }

                var allKeys = MemoryManager.GetAllKeys();

                if (category != null)
                {
                    allKeys = allKeys.Where(k => k.StartsWith($"{category}.")).ToList();
                }

                return JsonSerializer.Serialize(new { keys = allKeys, total = allKeys.Count }, Program.options);
            }
            catch (JsonException ex)
            {
                return $"错误：解析参数失败 - {ex.Message}";
            }
        }
    }

    /// <summary>
    /// 删除记忆工具
    /// </summary>
    public class DeleteMemoryTool : iTool
    {
        public string Name => "delete_memory";
        public string Description => "删除指定的记忆内容";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                key = new { type = "string", description = "要删除的记忆键名" },
                category = new { type = "string", description = "记忆类别", @enum = new[] { "globalVariables", "documentOutline", "progress", "documentContent", "customMemories" } }
            },
            required = new[] { "key" }
        };

        public string Execute(string arguments)
        {
            try
            {
                var argsDoc = JsonDocument.Parse(arguments);
                if (!argsDoc.RootElement.TryGetProperty("key", out var keyElement))
                {
                    return "错误：缺少参数 'key'";
                }

                string key = keyElement.GetString() ?? "";
                string? category = null;

                if (argsDoc.RootElement.TryGetProperty("category", out var categoryElement))
                {
                    category = categoryElement.GetString();
                }

                string deleteKey = category != null ? $"{category}.{key}" : key;
                bool success = MemoryManager.DeleteMemory(deleteKey);

                return success ? $"成功删除记忆: {deleteKey}" : $"未找到要删除的记忆: {deleteKey}";
            }
            catch (JsonException ex)
            {
                return $"错误：解析参数失败 - {ex.Message}";
            }
        }
    }

    /// <summary>
    /// 获取AI生成工具
    /// </summary>
    public class GetAIGenerateTool : iTool
    {
        public string Name => "get_ai_generate";
        public string Description => "当用户想要调用AI针对参考信息生成内容时使用，调用此工具前，应该先询问用户的要求，作为调用工具的参数prompt";
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                context = new { type = "string", description = "提供给AI的参考信息" },
                prompt = new { type = "string", description = "用户提示词" }
            },
            required = new[] { "prompt" }
        };

        public string Execute(string arguments)
        {
            try
            {
                var argsDoc = JsonDocument.Parse(arguments);
                if (!argsDoc.RootElement.TryGetProperty("context", out var contextElement))
                {
                    return "错误：缺少参数 'context'。";
                }
                if (!argsDoc.RootElement.TryGetProperty("prompt", out var promptElement))
                {
                    return "错误：缺少参数 'prompt'。";
                }

                return "";
                //string context = contextElement.GetString() ?? "";
                //string prompt = promptElement.GetString() ?? "";
                //var message = Program.CallVllmApi(new List<ChatMessage>{
                //    new SystemMessage($@"参考以下内容回答用户的问题：【{context}】"),
                //    new UserMessage($@"{prompt} /nothink")
                //}).Result.content ?? "";
                ////删除 <think></think> 标记，及其中的内容
                //var index = message.IndexOf("</think>");
                //return index >= 0 ? message.Substring(index + "</think>".Length) : "";
            }
            catch (JsonException ex)
            {
                return $"错误：解析参数失败 - {ex.Message}";
            }
        }
    }
}
