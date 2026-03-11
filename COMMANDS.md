# Build app
```
dotnet build Apps.OpenAI.sln -c Debug
```

# Test app
```
dotnet test Apps.OpenAI.sln -c Debug
```

# Publish app
```
dotnet publish Apps.OpenAI/Apps.OpenAI.csproj -c Release -o .\artifacts\blackbird-openai
```
