# EfCore.Boost Simple Template - Maintainer Documentation

This document describes how to maintain, test, and extend the `EfCore.Boost.Template.Simple` template.

## Purpose

The `EfCore.Boost.Template.Simple` provides a quick-start project structure with two projects:
1. **Model project**: Contains the `DbContext`, entities, and Unit of Work (UoW).
2. **Migrate project**: A console utility for managing database schema, migrations (MS SQL, PostgreSQL, MySQL), and seed data.

## Folder Structure

- `templates\Boost.Simple\`: The template content source.
  - `.template.config\template.json`: Template definition and parameter logic.
  - `BoostX.Model\`: The model project source.
  - `BoostX.Migrate\`: The migration utility source.
  - `Boost.Simple.Template.csproj`: The SDK-style packaging project for the NuGet package.
  - `Usage.md`: The end-user documentation that will be included in the generated project.
  - `PackageREADME.md`: The README specifically for the NuGet package.
  - `LICENSE.txt`: The MIT license for the package.

## Placeholder Tokens

- `BoostX`: Replaced by the project name provided during `dotnet new`. Used in project names, namespaces, and documentation.
- `BoostCTX`: Replaced by the normalized DbContext class name (e.g., `MyProjectContext`).
- `BoostSchemaX`: Replaced by the database schema name (e.g., `core`).

## Template Symbols & Logic

The `template.json` file uses several symbols to handle `DbContext` naming logic:
1. `Context`: A parameter for an explicit `DbContext` name override.
2. `derivedContextName`: A derived symbol that takes the project name (`name`).
3. `initialContextInput`: A `coalesce` generator that uses `Context` if provided, otherwise `derivedContextName`.
4. `normalizedContextName`: A `regex` generator that removes any trailing `Context` or `CTX` from `initialContextInput`.
5. `BoostCTX`: A `regex` generator that appends `Context` to `normalizedContextName`, ensuring the final name follows the standard naming convention. This symbol is used for both file content replacement and file renaming.

## Installation for Local Development

To install the template from the local folder for testing:

```powershell
dotnet new install .\templates\Boost.Simple
```

To uninstall:
```powershell
dotnet new uninstall .\templates\Boost.Simple
```

## Packaging

To produce the NuGet template package (`.nupkg`):

```powershell
dotnet pack .\templates\Boost.Simple\Boost.Simple.Template.csproj -o .\artifacts
```

The resulting package `EfCore.Boost.Template.Simple.1.0.0.nupkg` will be located in the `artifacts` directory.

## Testing the Template

1.  Install the template locally.
2.  Create a test project in a temporary directory:
    ```powershell
    mkdir test_gen
    cd test_gen
    dotnet new boostsimple -n TestApp --Schema myschema --Context SalesDb
    ```
3.  Verify the generated project structure:
    - `TestApp.Model` and `TestApp.Migrate` folders.
    - `SalesDbContext.cs` inside `TestApp.Model`.
    - `DefaultSchemaName = "myschema"` inside `SalesDbContext.cs`.
4.  Build the projects:
    ```powershell
    cd TestApp.Migrate
    dotnet build
    ```

## Extending or Modifying

- To add new files, place them in the `templates\Boost.Simple\` directory and ensure they are not excluded by the `Content` item group in `Boost.Simple.Template.csproj`.
- To add new parameters, update the `symbols` section in `template.json`.
- When updating the version, update the `<Version>` property in `Boost.Simple.Template.csproj`.
