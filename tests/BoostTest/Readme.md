# EfCore.Boost Tests (and Usage Demo)

This test project serves two roles:

1. **Validation**: it verifies that EfCore.Boost behaves consistently across database providers.
2. **Demonstration**: it shows practical, copy-pasteable examples of how to use Boost in real code.

The guiding principle is simple: **same model, same Unit of Work code, different database ´reality´**.

## What´s in here

### 1) A database project, migrated to multiple providers

The solution includes a single EF Core model ("the database project") that is migrated and exercised on:

- PostgreSQL
- SQL Server (incl. Azure SQL)
- MySQL

Boost conventions and provider rules apply at runtime based on which connection/provider is active.

### 2) The same test suite runs against each provider

For PostgreSQL, SQL Server, and MySQL we use **Testcontainers** to spin up ephemeral databases, apply migrations, and run the **same** smoke tests against each provider.

This demonstrates the intended Boost workflow:

- choose a provider (connection + driver)
- migrate the same model
- run the same UOW + Repo operations
- verify results and behavior consistently

### 3) Smoke tests that mirror "real usage"

The smoke tests are intentionally written as a compact, end-to-end walkthrough of typical Boost usage, including:

- **Unit of Work patterns** (repo access, query helpers, transaction flow)
- **Routines** (calling stored procedures/functions in a provider-aware way)
- **OData handling** (filter/sort/page, and expand-as-include patterns where supported)
- **Bulk inserts** (provider-optimized paths, batching behavior, identity handling)

In other words: if you want to understand how Boost is meant to be used, the smoke tests are the shortest path.

### 4) Ef-migrations helpers

In the folder [TestDb/Ps/](./TestDb/Ps/) you´ll find powershell scripts that handle the model migrations for all supported providers.
Take a look a them if you want to see examples on how to automate migrations for multiple providers.

## Usage of Testcontainers

This test project uses **Testcontainers** to validate EfCore.Boost against _real database engines_, not mocks, fakes, or in-memory substitutes.

A test container is, quite literally, a database server in a box:

- started on demand
- configured programmatically
- used for a test run
- and thrown away afterward

Each test container runs the same database software you would use in production (PostgreSQL, SQL Server, MySQL), but with a lifecycle fully controlled by the test code.

### How Testcontainers are used here

For each supported database provider, the test flow is the same:

1. A database server is started in a container
2. A fresh test database is created
3. EF Core migrations are applied
4. The smoke tests are executed
5. The container is stopped and disposed

Because the database lifecycle itself is part of what is being validated, **each provider is exercised end-to-end within a single test method**, with synchronized variants included where relevant.

This makes the test intent explicit: we are testing how Boost, EF Core, and the database provider behave together, from startup to shutdown.  
See [TestContainers.md](./TestContainers.md) for more details on the Testcontainers setup.

### One test per database provider

Each database flavor (PostgreSQL, SQL Server, MySQL) has:

- its own container configuration
- its own provider and driver
- the same EF Core model
- the same Unit of Work and repository logic
- the same smoke test code

This structure demonstrates that Boost´s conventions allow the _same code_ to run against _different database realities_ without branching or provider-specific logic in the test body.

Synchronized test variants exist only to validate those execution paths. They are not intended as guidance for modern application code, where asynchronous database access should be preferred.

### What is being tested

The container-backed tests validate:

- database startup and connectivity
- cross-provider migrations from the same model
- Unit of Work usage patterns
- routine execution across providers
- OData query handling
- bulk insert behavior
- provider-specific edge cases handled by Boost conventions

In short: this is an **integration test suite by design**, and the tests double as executable documentation.

See [UnitTestContainers.cs](./UnitTestContainers.cs#L351-L474) for the actual test code.
And [SmokeTest.md](./SmokeTest.md) for more details about the smoke test.

### Requirements to run container-based tests

To run these tests locally or in CI, the following are required:

- Docker installed
- Docker running and accessible to the current user
- permission to start and stop containers

Testcontainers communicates directly with Docker. If Docker is unavailable or not running, the container-based tests will fail before any test logic is executed.

See [TestConteiners.md](./TestConteiners.md) for more details.

For environments where containers cannot be used (for example, Azure SQL), a different testing strategy is required. That setup is covered in a dedicated section of this documentation.

## Azure SQL note

SQL Server can be tested with containers, but **Azure SQL cannot**.  
You need a real Azure SQL database for that. Create an emtpy database named `TestDb` in your Azure SQL server, and configure access using a managed identity or service principal.  
See [TestAzure.md](./TestAzure.md) for more details.
