using Apps.OpenAI.Actions.Base;
using Apps.OpenAI.Api;
using Apps.OpenAI.Constants;
using Apps.OpenAI.Dtos;
using Apps.OpenAI.Models.Requests.Chat;
using Apps.OpenAI.Services;
using Blackbird.Applications.Sdk.Common.Authentication;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Reflection;

namespace Tests.OpenAI;

[TestClass]
public class ModelParameterRoutingTests
{
    [TestMethod]
    public async Task ExecuteChatCompletionAsync_Gpt41_UsesModernParams()
    {
        var client = new CapturingOpenAiUniversalClient();
        var service = new OpenAICompletionService(client);

        await service.ExecuteChatCompletionAsync(
            [new ChatMessageDto("user", "hello")],
            "gpt-4.1",
            new BaseChatRequest
            {
                MaximumTokens = 128,
                Temperature = 0.2f,
                TopP = 1,
                PresencePenalty = 0,
                FrequencyPenalty = 0,
            },
            new { type = "json_object" });

        var body = ParseBody(client.LastRequest);

        Assert.IsTrue(body.ContainsKey("response_format"));
        Assert.IsTrue(body.ContainsKey("max_completion_tokens"));
        Assert.IsFalse(body.ContainsKey("reasoning_effort"));
        Assert.IsFalse(body.ContainsKey("max_tokens"));
    }

    [TestMethod]
    public async Task ExecuteChatCompletionAsync_Gpt35_UsesLegacyRouting()
    {
        var client = new CapturingOpenAiUniversalClient();
        var service = new OpenAICompletionService(client);

        await service.ExecuteChatCompletionAsync(
            [new ChatMessageDto("user", "hello")],
            "gpt-3.5-turbo",
            new BaseChatRequest
            {
                MaximumTokens = 128,
                Temperature = 0.2f,
            },
            new { type = "json_object" });

        var body = ParseBody(client.LastRequest);

        Assert.IsFalse(body.ContainsKey("response_format"));
        Assert.IsTrue(body.ContainsKey("max_completion_tokens"));
        Assert.IsFalse(body.ContainsKey("reasoning_effort"));
    }

    [TestMethod]
    public async Task ExecuteChatCompletionAsync_Gpt5_UsesReasoningEffortAndNoTemperature()
    {
        var client = new CapturingOpenAiUniversalClient();
        var service = new OpenAICompletionService(client);

        await service.ExecuteChatCompletionAsync(
            [new ChatMessageDto("user", "hello")],
            "gpt-5",
            new BaseChatRequest
            {
                MaximumTokens = 128,
                Temperature = 0.2f,
                ReasoningEffort = "low",
            },
            new { type = "json_object" });

        var body = ParseBody(client.LastRequest);

        Assert.IsTrue(body.ContainsKey("reasoning_effort"));
        Assert.IsFalse(body.ContainsKey("temperature"));
        Assert.IsTrue(body.ContainsKey("response_format"));
    }

    [TestMethod]
    public void GenerateChatBody_Gpt41_UsesModernTokenFields()
    {
        var body = InvokeGenerateChatBody("gpt-4.1", new BaseChatRequest
        {
            MaximumTokens = 128,
            Temperature = 0.2f,
            ReasoningEffort = "low",
        });

        Assert.IsTrue(body.ContainsKey("max_completion_tokens"));
        Assert.IsTrue(body.ContainsKey("reasoning_effort"));
        Assert.IsFalse(body.ContainsKey("max_tokens"));
    }

    [TestMethod]
    public void GenerateChatBody_Gpt35_UsesLegacyTokenFields()
    {
        var body = InvokeGenerateChatBody("gpt-3.5-turbo", new BaseChatRequest
        {
            MaximumTokens = 128,
            Temperature = 0.2f,
            ReasoningEffort = "low",
        });

        Assert.IsTrue(body.ContainsKey("max_tokens"));
        Assert.IsFalse(body.ContainsKey("max_completion_tokens"));
        Assert.IsFalse(body.ContainsKey("reasoning_effort"));
    }

    private static Dictionary<string, object> InvokeGenerateChatBody(string model, BaseChatRequest request)
    {
        var method = typeof(BaseActions).GetMethod(
            "GenerateChatBody",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);

        var result = method.Invoke(
            null,
            [new List<object> { new ChatMessageDto("user", "hello") }, model, request]);

        Assert.IsNotNull(result);
        return (Dictionary<string, object>)result;
    }

    private static JObject ParseBody(RestRequest request)
    {
        var bodyParam = request.Parameters.FirstOrDefault(x => x.Type == ParameterType.RequestBody);
        Assert.IsNotNull(bodyParam);
        Assert.IsNotNull(bodyParam.Value);

        var raw = bodyParam.Value!.ToString()!;
        var parsed = JToken.Parse(raw);

        if (parsed.Type == JTokenType.String)
        {
            parsed = JToken.Parse(parsed.Value<string>()!);
        }

        return (JObject)parsed;
    }

    private sealed class CapturingOpenAiUniversalClient() : OpenAiUniversalClient(CreateCredentials())
    {
        public RestRequest LastRequest { get; private set; } = null!;

        public override Task<T> ExecuteWithErrorHandling<T>(RestRequest request)
        {
            LastRequest = request;

            if (typeof(T) == typeof(ChatCompletionDto))
            {
                var response = new ChatCompletionDto([], UsageDto.Zero);
                return Task.FromResult((T)(object)response);
            }

            throw new InvalidOperationException($"Unsupported response type: {typeof(T).Name}");
        }

        private static IEnumerable<AuthenticationCredentialsProvider> CreateCredentials()
        {
            return
            [
                new AuthenticationCredentialsProvider(CredNames.ConnectionType, ConnectionTypes.OpenAi),
                new AuthenticationCredentialsProvider(CredNames.ApiKey, "test-key"),
            ];
        }
    }
}
