using Apps.OpenAI.Actions.Base;
using Apps.OpenAI.Models.Content;
using Apps.OpenAI.Models.Identifiers;
using Apps.OpenAI.Models.Requests.Chat;
using Apps.OpenAI.Models.Requests.Content;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Apps.OpenAI.Dtos;
using Apps.OpenAI.Models.Entities;
using Apps.OpenAI.Models.PostEdit;
using Apps.OpenAI.Services;
using Blackbird.Filters.Transformations;
using Blackbird.Filters.Extensions;
using Blackbird.Filters.Enums;
using Blackbird.Filters.Constants;
using Blackbird.Applications.SDK.Blueprints;
using Apps.OpenAI.Constants;
using Apps.OpenAI.Models.Requests.Background;
using Apps.OpenAI.Models.Responses.Background;
using Apps.OpenAI.Models.Responses.Chat;
using Apps.OpenAI.Utils;
using Blackbird.Applications.Sdk.Glossaries.Utils.Dtos;
using Blackbird.Filters.Xliff.Xliff1;
using TiktokenSharp;

namespace Apps.OpenAI.Actions;

[ActionList("Translation")]
public class TranslationActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) 
    : BaseActions(invocationContext, fileManagementClient)
{
    private const string TokenEncoding = "cl100k_base";
    private const int DefaultTokenBudgetPerBucket = 4000;

    [BlueprintActionDefinition(BlueprintAction.TranslateFile)]
    [Action("Translate", Description = "Translates file content from a CMS or file storage and outputs localized content for compatible actions.")]
    public async Task<ContentProcessingResult> TranslateContent([ActionParameter] TextChatModelIdentifier modelIdentifier,
        [ActionParameter] TranslateContentRequest input,
        [ActionParameter, Display("Additional instructions", Description = "Specify additional instructions to be applied to the translation. For example, 'Cater to an older audience.'")] string? prompt,
        [ActionParameter] GlossaryRequest glossary,
        [ActionParameter] ReasoningEffortRequest reasoningEffortRequest,
        [ActionParameter, Display("Bucket size", Description = "Specify the approximate max number of source tokens per translation bucket. Default value: 4000. (See our documentation for an explanation)")] int? bucketSize = null,
        [ActionParameter, Display("Parallel requests", Description = "Maximum number of translation buckets processed in parallel. Default value: 3.")] int? parallelRequests = null)
    {
        // Step 1: Resolve runtime options and validate parallelism constraints.
        var neverFail = false;
        var tokenBudgetPerBucket = bucketSize ?? DefaultTokenBudgetPerBucket;
        var maxParallelBatches = parallelRequests ?? 3;
        if (maxParallelBatches < 1 || maxParallelBatches > 10)
        {
            throw new PluginMisconfigurationException("Parallel requests must be between 1 and 10.");
        }
        if (tokenBudgetPerBucket < 1)
        {
            throw new PluginMisconfigurationException("Bucket size must be greater than 0.");
        }

        // Step 2: Download and parse the input file into transformation content.
        var result = new ContentProcessingResult();
        var stream = await fileManagementClient.DownloadAsync(input.File);
        var content = await ErrorHandler.ExecuteWithErrorHandlingAsync(() =>
            Transformation.Parse(stream, input.File.Name)
        );

        // Step 3: Merge language metadata from UI input only when file metadata is missing.
        content.SourceLanguage ??= input.SourceLanguage;
        content.TargetLanguage ??= input.TargetLanguage;        

        // Step 4: Ensure target language exists; source language can still be auto-detected.
        if (content.TargetLanguage == null) throw new PluginMisconfigurationException("The target language is not defined yet. Please assign the target language in this action.");

        // Step 5: Auto-detect source language when it is absent in both file and UI input.
        if (content.SourceLanguage == null)
        {
            content.SourceLanguage = await IdentifySourceLanguage(modelIdentifier, content.Source().GetPlaintext());
        }

        // Step 6: Prepare translation services and shared state for aggregated results.
        var batchProcessingService = new BatchProcessingService(UniversalClient, FileManagementClient);

        var systemprompt = string.Empty;
        var errors = new List<string>();
        var usages = new List<UsageDto>();
        var aggregationLock = new object();

        // Step 7: Configure batch translation options passed to the processing service.
        var batchOptions = new BatchProcessingOptions(
            UniversalClient.GetModel(modelIdentifier.ModelId),
            content.SourceLanguage,
            content.TargetLanguage,
            prompt,
            systemprompt,
            false,
            glossary.Glossary,
            true,
            3,
            null,
            reasoningEffortRequest.ReasoningEffort,
            content.Notes);

        // Step 8: Define per-batch execution logic, including deduplication and fallback mapping.
        async Task<List<TranslationEntity>> BatchTranslate(List<(Unit Unit, Segment Segment)> batch, int batchNumber)
        {
            var batchList = batch;
            var idSegments = batchList.Select((x, i) => new { Id = i + 1, Value = x }).ToDictionary(x => x.Id.ToString(), x => x.Value.Segment);
            
            var translationLookup = new Dictionary<string, TranslationEntity>();
            try
            {
                var batchResult = await batchProcessingService.ProcessBatchAsync(idSegments, batchOptions, false);

                var duplicates = batchResult.UpdatedTranslations.GroupBy(x => x.TranslationId)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { TranslationId = g.Key, Count = g.Count() })
                    .ToList();

                lock (aggregationLock)
                {
                    systemprompt = batchResult.SystemPrompt;
                    errors.AddRange(duplicates.Select(duplicate => $"Duplicate translation ID found: {duplicate.TranslationId} appears {duplicate.Count} times"));
                    errors.AddRange(batchResult.ErrorMessages);
                    usages.Add(batchResult.Usage);
                }

                if (batchResult.IsSuccess)
                {
                    foreach (var translation in batchResult.UpdatedTranslations)
                    {
                        translationLookup.TryAdd(translation.TranslationId, translation);
                    }
                }

                if (!batchResult.IsSuccess && !neverFail)
                {
                    throw new PluginApplicationException(
                        $"Failed to process batch {batchNumber} (token budget: {tokenBudgetPerBucket}). Errors: {string.Join(", ", batchResult.ErrorMessages)}");
                }
            }
            catch (Exception ex) when (neverFail)
            {
                lock (aggregationLock)
                {
                    errors.Add($"Error in batch {batchNumber} (token budget: {tokenBudgetPerBucket}): {ex.Message}");
                }
            }
            
            // Ensure exactly one result per (Unit, Segment) in the batch
            var allResults = new List<TranslationEntity>();
            for (int i = 0; i < batchList.Count; i++)
            {
                var id = (i + 1).ToString();
                if (translationLookup.TryGetValue(id, out var translation))
                {
                    allResults.Add(translation);
                }
                else
                {
                    // Fallback: return original target text so the segment is unchanged
                    allResults.Add(new TranslationEntity
                    {
                        TranslationId = id,
                        TranslatedText = batchList[i].Segment.GetTarget()
                    });
                }
            }

            return allResults;
        }

        // Step 9: Extract initial segments eligible for translation and compute counters.
        var units = content.GetUnits();        
        result.TotalSegmentsCount = units.SelectMany(x => x.Segments).Count();
        units = units.Where(x => x.IsInitial);
        var segments = units.Where(x => x.IsInitial).SelectMany(x => x.Segments);
        result.TotalTranslatable = segments.Count();

        // Step 10: Flatten unit-segment pairs so updates can be re-applied to original units.
        var segmentsToTranslate = units
            .Where(x => x.IsInitial)
            .SelectMany(unit => unit.Segments.Select(segment => (Unit: unit, Segment: segment)))
            .ToList();

        // Step 11: Build translation buckets using token budget instead of segment count.
        var translationBatches = await BuildTokenBucketsAsync(
            segmentsToTranslate,
            x => x.Segment.GetSource(),
            tokenBudgetPerBucket);

        // Step 12: Limit concurrent batch execution with a semaphore.
        using var semaphore = new SemaphoreSlim(maxParallelBatches, maxParallelBatches);

        var batchTasks = translationBatches
            .Select((batch, index) => ProcessBatch(batch, index + 1))
            .ToList();

        // Step 13: Execute all batches, then regroup translated segments by their parent unit.
        var processedSegments = (await Task.WhenAll(batchTasks)).SelectMany(x => x).ToList();
        var processedBatches = processedSegments
            .GroupBy(x => x.Unit)
            .Select(group => (group.Key, group.Select(x => (x.Segment, x.Translation)).ToList()))
            .ToList();

        result.ProcessedBatchesCount = translationBatches.Count;
        result.Usage = UsageDto.Sum(usages);
        result.SystemPrompt = systemprompt;

        // Step 14: Wrap per-batch execution with semaphore acquire/release.
        async Task<List<(Unit Unit, Segment Segment, TranslationEntity Translation)>> ProcessBatch(
            List<(Unit Unit, Segment Segment)> batch,
            int batchNumber)
        {
            await semaphore.WaitAsync();
            try
            {
                var translations = await BatchTranslate(batch, batchNumber);
                return batch.Select((item, i) => (item.Unit, item.Segment, translations[i])).ToList();
            }
            finally
            {
                semaphore.Release();
            }
        }

        // Step 15: Apply translated text back to segments and collect usage provenance.
        var updatedCount = 0;
        foreach (var (unit, results) in processedBatches)
        {
            foreach(var (segment, translation) in results) 
            {
                var shouldTranslateFromState = segment.State == null || segment.State == SegmentState.Initial;
                if (!shouldTranslateFromState || string.IsNullOrEmpty(translation.TranslatedText))
                {
                    continue;
                }

                if (segment.GetTarget() != translation.TranslatedText)
                {
                    updatedCount++;
                    segment.SetTarget(translation.TranslatedText);
                    segment.State = SegmentState.Translated;
                }
            }

            var model = UniversalClient.GetModel(modelIdentifier.ModelId);
            unit.Provenance.Translation.Tool = model;
            double tokens = result.Usage.TotalTokens / processedBatches.Count();
            unit.AddUsage(model, Math.Round(tokens, 0), UsageUnit.Tokens);
        }

        result.TargetsUpdatedCount = updatedCount;

        // Step 16: Serialize and upload output in the requested format.
        if (input.OutputFileHandling == "original")
        {
            var targetContent = content.Target();
            result.File = await fileManagementClient.UploadAsync(targetContent.Serialize().ToStream(), targetContent.OriginalMediaType, targetContent.OriginalName);
        } 
        else if (input.OutputFileHandling == "xliff1")
        {
            var xliff1String = Xliff1Serializer.Serialize(content);
            result.File = await fileManagementClient.UploadAsync(xliff1String.ToStream(), MediaTypes.Xliff, content.XliffFileName);
        }
        else
        {
            result.File = await fileManagementClient.UploadAsync(content.Serialize().ToStream(), MediaTypes.Xliff, content.XliffFileName);
        }

        // Step 17: Return aggregated processing metadata and output file reference.
        return result;
    }    

    [Action("Translate in background", Description = "Starts background translation for a file and outputs a batch ID to download results later.")]
    public async Task<BackgroundProcessingResponse> TranslateInBackground([ActionParameter] StartBackgroundProcessRequest startBackgroundProcessRequest)
    {
        var stream = await fileManagementClient.DownloadAsync(startBackgroundProcessRequest.File);
        var content = await ErrorHandler.ExecuteWithErrorHandlingAsync(() => 
            Transformation.Parse(stream, startBackgroundProcessRequest.File.Name)
        );
        
        content.SourceLanguage ??= startBackgroundProcessRequest.SourceLanguage;
        content.TargetLanguage ??= startBackgroundProcessRequest.TargetLanguage;
        
        if (content.TargetLanguage == null) 
            throw new PluginMisconfigurationException("The target language is not defined yet. Please assign the target language in this action.");

        if (content.SourceLanguage == null)
        {
            content.SourceLanguage = await IdentifySourceLanguage(startBackgroundProcessRequest, content.Source().GetPlaintext());
        }

        var units = content.GetUnits();
        var segments = units.SelectMany(x => x.Segments);
        segments = segments.GetSegmentsForTranslation().ToList();

        var batchRequests = new List<object>();
        
        Glossary? blackbirdGlossary = await ProcessGlossaryFromFile(startBackgroundProcessRequest.Glossary);
        Dictionary<string, List<GlossaryEntry>>? glossaryLookup = null;
        if (blackbirdGlossary != null)
        {
            glossaryLookup = CreateGlossaryLookup(blackbirdGlossary);
        }
        
        var systemPromptBase = $"Translate the following texts from {content.SourceLanguage} to {content.TargetLanguage}. " +
                            "Preserve the original format, tags, and structure. Return the translations in the specified JSON format.";
                            
        if (startBackgroundProcessRequest.AdditionalInstructions != null)
        {
            systemPromptBase += $" Additional instructions: {startBackgroundProcessRequest.AdditionalInstructions}.";
        }
        
        if(glossaryLookup != null)
        {
            systemPromptBase += " Use the provided glossary to ensure accurate translations of specific terms.";
        }
        
        var tokenBudgetPerBucket = startBackgroundProcessRequest.BucketSize ?? DefaultTokenBudgetPerBucket;
        if (tokenBudgetPerBucket < 1)
            throw new PluginMisconfigurationException("Bucket size must be greater than 0.");

        var indexedSegments = segments
            .Select((segment, index) => (Segment: segment, Index: index))
            .ToList();

        var segmentBuckets = await BuildTokenBucketsAsync(
            indexedSegments,
            x => x.Segment.GetSource(),
            tokenBudgetPerBucket);
        
        foreach (var (bucket, bucketIndex) in segmentBuckets.Select((bucket, index) => (bucket, index)))
        {
            var segmentTexts = new List<string>();
            var segmentIds = new List<string>();
            
            foreach (var item in bucket)
            {
                var sourceText = item.Segment.GetSource();
                segmentTexts.Add(sourceText);
                segmentIds.Add(item.Index.ToString());
            }
            
            var userPrompt = "Translate the following texts:\n\n";
            for (int i = 0; i < segmentTexts.Count; i++)
            {
                userPrompt += $"ID: {segmentIds[i]}\nText: {segmentTexts[i]}\n\n";
            }
            
            if (glossaryLookup != null)
            {
                var combinedText = string.Join(" ", segmentTexts);
                var glossaryPromptPart = GetOptimizedGlossaryPromptPart(glossaryLookup, combinedText);
                if (!string.IsNullOrEmpty(glossaryPromptPart))
                {
                    userPrompt += $"\nGlossary terms:\n{glossaryPromptPart}";
                }
            }
            
            var batchRequest = new
            {
                custom_id = bucketIndex.ToString(),
                method = "POST",
                url = "/v1/chat/completions",
                body = new
                {
                    model = UniversalClient.GetModel(startBackgroundProcessRequest.ModelId),
                    messages = new object[]
                    {
                        new
                        {
                            role = "system",
                            content = systemPromptBase
                        },
                        new
                        {
                            role = "user",
                            content = userPrompt
                        }
                    },
                    response_format = ResponseFormats.GetXliffResponseFormat()
                }
            };

            batchRequests.Add(batchRequest);
        }

        var batchResponse = await CreateBatchAsync(batchRequests);
        content.MetaData.Add(new Metadata("background-type", "translate") { Category = [Meta.Categories.Blackbird]});
        return new BackgroundProcessingResponse
        {
            BatchId = batchResponse.Id,
            Status = batchResponse.Status,
            CreatedAt = batchResponse.CreatedAt,
            ExpectedCompletionTime = batchResponse.ExpectedCompletionTime,
            TransformationFile = await fileManagementClient.UploadAsync(content.Serialize().ToStream(), MediaTypes.Xliff, content.XliffFileName)
        };
    }

    private static async Task<List<List<T>>> BuildTokenBucketsAsync<T>(
        IReadOnlyList<T> items,
        Func<T, string> getText,
        int tokenBudget)
    {
        var encoding = await TikToken.GetEncodingAsync(TokenEncoding);
        var buckets = new List<List<T>>();
        var currentBucket = new List<T>();
        var currentTokenCount = 0;

        foreach (var item in items)
        {
            var text = getText(item) ?? string.Empty;
            var tokenCount = Math.Max(1, encoding.Encode(text).Count);

            if (currentBucket.Count > 0 && currentTokenCount + tokenCount > tokenBudget)
            {
                buckets.Add(currentBucket);
                currentBucket = new List<T>();
                currentTokenCount = 0;
            }

            currentBucket.Add(item);
            currentTokenCount += tokenCount;
        }

        if (currentBucket.Count > 0)
        {
            buckets.Add(currentBucket);
        }

        return buckets;
    }

    [BlueprintActionDefinition(BlueprintAction.TranslateText)]
    [Action("Translate text", Description = "Outputs localized text for the provided input text.")]
    public async Task<TranslateTextResponse> LocalizeText([ActionParameter] TextChatModelIdentifier modelIdentifier, 
        [ActionParameter] LocalizeTextRequest input, 
        [ActionParameter] GlossaryRequest glossary)
    {
        var systemPrompt = "You are a text localizer. Localize the provided text for the specified locale while " +
                           "preserving the original text structure. Respond with localized text and only localized text, nothing else.";

        var userPrompt = @$"
                    Original text: {input.Text}
                    Locale: {input.TargetLanguage} 
                
                    ";

        if (glossary.Glossary != null)
        {
            var glossaryPromptPart = await GetGlossaryPromptPart(glossary.Glossary, input.Text, true);
            if (glossaryPromptPart != null)
            {
                userPrompt +=
                    "\nEnhance the localized text by incorporating relevant terms from our glossary where applicable. " +
                    "If you encounter terms from the glossary in the text, ensure that the localized text aligns " +
                    "with the glossary entries for the respective languages. If a term has variations or synonyms, " +
                    "consider them and choose the most appropriate translation from the glossary to maintain " +
                    $"consistency and precision. {glossaryPromptPart}";
            }
        }

        userPrompt += "Localized text: ";

        var messages = new List<ChatMessageDto> { new(MessageRoles.System, systemPrompt), new(MessageRoles.User, userPrompt) };
        var response = await ExecuteChatCompletion(messages, UniversalClient.GetModel(modelIdentifier.ModelId), input);

        return new()
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            TranslatedText = response.Choices.First().Message.Content,
            Usage = response.Usage,
        };
    }

}
