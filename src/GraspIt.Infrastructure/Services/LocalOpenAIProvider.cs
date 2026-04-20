using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GraspIt.Core.Entities;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.Retry;

namespace GraspIt.Infrastructure.Services;

public class QuestionRow
{
    public string QNo { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Marks { get; set; } = string.Empty;
    public string BT { get; set; } = string.Empty;
    public string CO { get; set; } = string.Empty;
}

public interface ILLMProvider
{
    Task<List<QuestionRow>> GenerateExamQuestionsAsync(string ocrMarkdown, string ragContext, string instructions);

    /// <summary>
    /// Generates structured question paper data in the format expected by
    /// LLM_qn_paper_generator.js (matching qn_format.json schema).
    /// </summary>
    Task<QuestionPaperData?> GenerateStructuredExamAsync(
        string ocrMarkdown, string ragContext, string instructions, QuestionPaperData? templateMetadata = null);
}

public class LocalOpenAIProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly AsyncRetryPolicy _retryPolicy;

    // JSON schema template for the LLM to follow
    private static readonly string StructuredJsonSchema = @"
{
  ""code"": ""24PXYZ401 - "",
  ""title"": ""Subject Title"",
  ""semester"": ""Fourth Semester BCA"",
  ""examNo"": ""-I"",
  ""duration"": ""60 Minutes"",
  ""date"": ""DD Month YYYY"",
  ""time"": ""HH:MM AM - HH:MM AM"",
  ""maxMarks"": ""25 Marks"",
  ""note"": ""1) Answer Two full questions choosing One full question from each of the units."",
  ""footer"": ""BT* : Bloom's Taxonomy   L*: Level   TLO*: Topic Level Outcome"",
  ""units"": [
    {
      ""name"": ""Unit - I"",
      ""questions"": [
        {
          ""no"": ""1"",
          ""parts"": [
            { ""sub"": ""a"", ""text"": ""Question text here."", ""marks"": ""6"", ""bt"": ""L2"", ""co"": ""CO1"" },
            { ""sub"": ""b"", ""text"": ""Question text here."", ""marks"": ""6.5"", ""bt"": ""L2"", ""co"": ""CO1"" }
          ]
        },
        ""OR"",
        {
          ""no"": ""2"",
          ""parts"": [
            { ""sub"": ""a"", ""text"": ""Question text here."", ""marks"": ""6"", ""bt"": ""L2"", ""co"": ""CO1"" }
          ]
        }
      ]
    }
  ]
}";

    public LocalOpenAIProvider(ChatClient? chatClient = null)
    {
        if (chatClient != null)
        {
            _chatClient = chatClient;
        }
        else
        {
            // OpenAI Official SDK (v2.0+) configured to point to local LM Studio via standard proxy endpoint.
            var httpClient = new HttpClient();
            
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri("http://localhost:1234/v1") // LM Studio / vLLM local endpoint
            };

            var client = new OpenAIClient(new ApiKeyCredential("lm-studio-local-key"), options);
            _chatClient = client.GetChatClient("local-model");
        }

        // Use Polly to wrap the API call in an AsyncRetryPolicy (3 retries, exponential backoff)
        _retryPolicy = Policy
            .Handle<Exception>() // Catches Network/Timeout/Rate limits from the local LLM
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public async Task<List<QuestionRow>> GenerateExamQuestionsAsync(string ocrMarkdown, string ragContext, string instructions)
    {
        var systemMessage = new SystemChatMessage(
            "You are an expert academic assistant designed to generate university-level exam questions.\n" +
            "CRITICAL: You MUST return ONLY a raw JSON array matching a List<QuestionRow> object.\n" +
            "Do NOT wrap it in ```json ticks. Do NOT output conversational text.\n" +
            "The JSON array should contain objects strictly with Keys: 'QNo', 'Question', 'Marks', 'BT', 'CO'.\n" +
            "Base the questions on the RAG context provided, matching the structural style of the OCR Markdown reference paper."
        );

        var prompt = $"【User Instructions】: {instructions}\n\n" +
                     $"【Reference Formatting (OCR)】:\n{ocrMarkdown}\n\n" +
                     $"【Syllabus Context (RAG)】:\n{ragContext}";

        var userMessage = new UserChatMessage(prompt);

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                ClientResult<ChatCompletion> completionResult = await _chatClient.CompleteChatAsync(
                    new OpenAI.Chat.ChatMessage[] { systemMessage, userMessage });

                var responseText = completionResult.Value.Content[0].Text;
                // Robust extraction: isolate exactly the JSON array to bypass <think> tags or chatty text
                int startIndex = responseText.IndexOf('[');
                int endIndex = responseText.LastIndexOf(']');
                if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                {
                    responseText = responseText.Substring(startIndex, endIndex - startIndex + 1);
                }

                return JsonSerializer.Deserialize<List<QuestionRow>>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<QuestionRow>();
            });
        }
        catch (Exception)
        {
            // Throw fallback gracefully if LLM repeatedly fails after Polly retries 
            return new List<QuestionRow>(); 
        }
    }

    /// <summary>
    /// Generates a complete, structured question paper in qn_format.json schema.
    /// The LLM fills in questions while preserving all the structural metadata
    /// (units, OR separators, sub-parts with Bloom's and CO levels).
    /// </summary>
    public async Task<QuestionPaperData?> GenerateStructuredExamAsync(
        string ocrMarkdown, string ragContext, string instructions, QuestionPaperData? templateMetadata = null)
    {
        var systemMessage = new SystemChatMessage(
            "You are an expert academic assistant that generates university-level exam question papers.\n\n" +
            "CRITICAL RULES:\n" +
            "1. You MUST return ONLY a single raw JSON object (NOT an array, NOT wrapped in ```json).\n" +
            "2. The JSON must exactly match this schema:\n" +
            StructuredJsonSchema + "\n\n" +
            "3. Each unit must contain questions with sub-parts (a, b, c, etc.).\n" +
            "4. Use \"OR\" as a literal string element in the questions array to separate alternative questions.\n" +
            "5. Bloom's Taxonomy levels: L1(Remember), L2(Understand), L3(Apply), L4(Analyze), L5(Evaluate), L6(Create).\n" +
            "6. Ensure marks per unit total correctly and question difficulty varies appropriately.\n" +
            "7. Generate meaningful, subject-specific questions based on the provided syllabus context.\n" +
            "8. Do NOT output any conversational text, explanations, or markdown. ONLY the JSON object.\n" +
            "9. The footer value should always be: \"BT* : Bloom's Taxonomy   L*: Level   TLO*: Topic Level Outcome\"\n" +
            "10. Populate the 'code', 'title', 'semester', etc. fields based on the instructions and reference.\n"
        );

        var templateHint = "";
        if (templateMetadata != null)
        {
            templateHint = $"\n【Template Metadata (use these values for header fields)】:\n" +
                          $"Code: {templateMetadata.Code}\n" +
                          $"Title: {templateMetadata.Title}\n" +
                          $"Semester: {templateMetadata.Semester}\n" +
                          $"ExamNo: {templateMetadata.ExamNo}\n" +
                          $"Duration: {templateMetadata.Duration}\n" +
                          $"MaxMarks: {templateMetadata.MaxMarks}\n";
        }

        var prompt = $"【User Instructions】: {instructions}\n\n" +
                     $"【Reference Paper Format (OCR)】:\n{ocrMarkdown}\n\n" +
                     $"【Syllabus Context (RAG)】:\n{ragContext}" +
                     templateHint;

        var userMessage = new UserChatMessage(prompt);

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                ClientResult<ChatCompletion> completionResult = await _chatClient.CompleteChatAsync(
                    new OpenAI.Chat.ChatMessage[] { systemMessage, userMessage });

                var responseText = completionResult.Value.Content[0].Text;

                // Extract JSON object: find first { and last }
                int startIndex = responseText.IndexOf('{');
                int endIndex = responseText.LastIndexOf('}');
                if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
                    return null;

                responseText = responseText.Substring(startIndex, endIndex - startIndex + 1);

                // Parse the structured JSON, handling "OR" in units and questions arrays
                return ParseStructuredJson(responseText);
            });
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Custom parser for the qn_format.json structure that handles mixed arrays
    /// containing both objects and "OR" string literals.
    /// </summary>
    private static QuestionPaperData? ParseStructuredJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var paper = new QuestionPaperData
            {
                Code = GetString(root, "code"),
                Title = GetString(root, "title"),
                Semester = GetString(root, "semester"),
                ExamNo = GetString(root, "examNo"),
                Duration = GetString(root, "duration"),
                Date = GetString(root, "date"),
                Time = GetString(root, "time"),
                MaxMarks = GetString(root, "maxMarks"),
                Note = GetString(root, "note"),
                Footer = GetString(root, "footer")
            };

            if (root.TryGetProperty("units", out var unitsElem) && unitsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var unitElem in unitsElem.EnumerateArray())
                {
                    if (unitElem.ValueKind == JsonValueKind.String && unitElem.GetString() == "OR")
                    {
                        paper.Units.Add(new QuestionPaperUnit { Name = "OR" });
                        continue;
                    }

                    if (unitElem.ValueKind != JsonValueKind.Object) continue;

                    var unit = new QuestionPaperUnit
                    {
                        Name = GetString(unitElem, "name")
                    };

                    if (unitElem.TryGetProperty("questions", out var questionsElem) &&
                        questionsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var qElem in questionsElem.EnumerateArray())
                        {
                            if (qElem.ValueKind == JsonValueKind.String && qElem.GetString() == "OR")
                            {
                                unit.Questions.Add(new QuestionPaperQuestion { No = "OR" });
                                continue;
                            }

                            if (qElem.ValueKind != JsonValueKind.Object) continue;

                            var question = new QuestionPaperQuestion
                            {
                                No = GetString(qElem, "no")
                            };

                            if (qElem.TryGetProperty("parts", out var partsElem) &&
                                partsElem.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var pElem in partsElem.EnumerateArray())
                                {
                                    if (pElem.ValueKind != JsonValueKind.Object) continue;
                                    question.Parts.Add(new QuestionPaperPart
                                    {
                                        Sub = GetString(pElem, "sub"),
                                        Text = GetString(pElem, "text"),
                                        Marks = GetString(pElem, "marks"),
                                        Bt = GetString(pElem, "bt"),
                                        Co = GetString(pElem, "co")
                                    });
                                }
                            }

                            unit.Questions.Add(question);
                        }
                    }

                    paper.Units.Add(unit);
                }
            }

            return paper;
        }
        catch
        {
            return null;
        }
    }

    private static string GetString(JsonElement elem, string prop)
    {
        if (elem.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString() ?? "";
        return "";
    }
}
