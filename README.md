# efCore.Boost (VS Code layout)

This repository lines up your existing **DbRepo** library as **efCore.Boost**, adds tests, and includes a NuGet packaging setup.

## Structure
- `src/efCore.Boost` — class library (package-ready)
- `tests/efCore.Boost.Tests` — xUnit tests (InMemory + SQLite in-memory)
- `samples/ConsoleSample` — minimal console host
- `.github/workflows/publish-nuget.yml` — GitHub Actions to pack & publish

## Local dev
```bash
dotnet restore
dotnet build
dotnet test
dotnet pack -c Release
```

## Publish
- Locally: `dotnet pack -c Release` → push `nupkg` to NuGet
- GitHub Actions: run **publish-nuget** workflow with a `version` and secret `NUGET_API_KEY`
