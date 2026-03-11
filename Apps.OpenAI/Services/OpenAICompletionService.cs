using Apps.OpenAI.Api;
using Apps.OpenAI.Api.Requests;
using Apps.OpenAI.Dtos;
using Apps.OpenAI.Models.PostEdit;
using Apps.OpenAI.Models.Requests.Chat;
using Apps.OpenAI.Services.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TiktokenSharp;

namespace Apps.OpenAI.Services;

public class OpenAICompletionService(OpenAiUniversalClient openAIClient) : IOpenAICompletionService
{
    private const string DefaultEncoding = "cl100k_base";

    public async Task<ChatCompletitionResult> ExecuteChatCompletionAsync(
        IEnumerable<ChatMessageDto> messages,
        string modelId,
        BaseChatRequest request,
        object? responseFormat = null)
    {
        var jsonDictionary = new Dictionary<string, object>
        {
            { "model", modelId },
            { "messages", messages },
            { "top_p", request?.TopP ?? 1 },
            { "presence_penalty", request?.PresencePenalty ?? 0 },
            { "frequency_penalty", request?.FrequencyPenalty ?? 0 },
        };

        bool usesLegacyParams = UsesLegacyChatParams(modelId);
        if (!usesLegacyParams)
            jsonDictionary.Add("response_format", responseFormat);

        if (request?.Temperature != null && !IsGpt5Family(modelId))
        {
            jsonDictionary.Add("temperature", request.Temperature);
        }
        
        if (request?.MaximumTokens != null)
        {
            jsonDictionary.Add("max_completion_tokens", request.MaximumTokens);
        }
        
        if (request?.ReasoningEffort != null && IsGpt5Family(modelId))
        {
            jsonDictionary.Add("reasoning_effort", request.ReasoningEffort);
        }

        var jsonBodySerialized = JsonConvert.SerializeObject(jsonDictionary, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        });

        var apiRequest = new OpenAIRequest("/chat/completions", Method.Post)
            .AddJsonBody(jsonBodySerialized);

        var response = await openAIClient.ExecuteWithErrorHandling<ChatCompletionDto>(apiRequest);
        return new(response, true, null);
    }

    private static bool UsesLegacyChatParams(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        var normalized = modelId.Trim().ToLowerInvariant();

        // Legacy families that rely on older chat parameter behavior.
        if (normalized.StartsWith("gpt-3.5"))
        {
            return true;
        }

        if (normalized == "gpt-4" || normalized.StartsWith("gpt-4-") || normalized.StartsWith("gpt-4-32k"))
        {
            return true;
        }

        return false;
    }

    private static bool IsGpt5Family(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        return modelId.Trim().StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    public int CalculateTokenCount(string text, string modelId)
    {
        try
        {
            var encoding = GetEncodingForModel(modelId);
            var tikToken = TikToken.EncodingForModel(encoding);
            return tikToken.Encode(text).Count;
        }
        catch (Exception)
        {
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }

    private static string GetEncodingForModel(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            return DefaultEncoding;
        }

        modelId = modelId.ToLower();
        if (modelId.StartsWith("gpt-4") || modelId.StartsWith("gpt-3.5") || modelId.StartsWith("text-embedding"))
        {
            return "cl100k_base";
        }

        if (modelId.Contains("davinci") || modelId.Contains("curie") ||
            modelId.Contains("babbage") || modelId.Contains("ada"))
        {
            return "p50k_base";
        }

        return DefaultEncoding;
    }
}
