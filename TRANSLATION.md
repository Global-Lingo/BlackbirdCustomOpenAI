# Translation Flow (Non-background)

This document summarizes the `Translate` action flow implemented in `TranslationActions.TranslateContent`.

## Steps

1. Validate runtime options
- Resolve bucket size (token budget per bucket) and parallel requests.
- Default token budget is 4000 when bucket size is not provided.
- Ensure parallel requests are between 1 and 10.

2. Download and parse input file
- Download the file from storage.
- Parse it into transformation content.

3. Resolve language values
- Prefer languages found in parsed file metadata.
- If missing, use UI input values (`SourceLanguage`, `TargetLanguage`).

4. Validate/detect languages
- `TargetLanguage` is required; if still missing, fail with configuration error.
- If `SourceLanguage` is still missing, auto-detect it from source plaintext.

5. Build batch processing options
- Configure model, source/target language, glossary, prompt, reasoning effort, and notes.

6. Extract translatable segments
- Read units from parsed content.
- Keep initial units/segments and collect total counters.

7. Create buckets
- Flatten to `(Unit, Segment)` pairs.
- Split into buckets using a token budget based on source text token count.
- Token counting uses `cl100k_base` encoding by default.

8. Process buckets in parallel
- Use a semaphore to limit concurrent batch requests.
- Translate each bucket and collect usage/system prompt/errors.
- If a translation is missing for an ID, keep original target text as fallback.

9. Apply results to content
- Update segment targets only for initial segments with non-empty translated text.
- Mark updated segments as translated.
- Attach model provenance and token usage per unit.

10. Serialize and upload output
- Output as original format, XLIFF 1, or default XLIFF depending on `OutputFileHandling`.
- Return processing stats, usage, system prompt, and output file reference.

## Source of Truth for Languages

- First source: parsed file metadata.
- Fallback: UI action parameters.
- Final fallback (source only): auto-detection.
