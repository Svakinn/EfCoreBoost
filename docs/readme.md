# EfCore.Boost Documentation

Welcome to the EfCore.Boost documentation hub.

This index helps you navigate the available guides and deeper documentation topics.  
Start here, then follow links into focused documents.

---

## <img src="../src/EfCoreBoost/icon.png" width="32" height="32" /> Why EfCore.Boost?

EfCore.Boost exists to resolve common challenges when working with EF Core in real-world projects.  
It standardizes how you handle the following 10 key areas:

1. **Structure** – clear boundaries instead of an exposed DbContext
2. **Multi-Provider** – consistent behavior across SQL Server, PostgreSQL, and MySQL
3. **Migrations** – streamlined, script-based deployment workflows
4. **Bulk Operations** – high-performance inserts and updates
5. **OData** – safe and controlled query exposure
6. **Database Features** – first-class support for views and routines
7. **Transactions** – consistent patterns across all providers
8. **Maintainability** – enforced conventions and predictable structure
9. **Model Definition** – simplified attribute-driven configuration
10. **Controlled Access** – purpose-specific Unit of Work boundaries

→ [Why EfCore.Boost? (Detailed Guide)](./WhyBoost.md)

---

## 🚀 Getting Started

Setup from template, model extension, migrations, and deployment workflow.

→ [Getting Started Guide](./GettingStarted.md)  
→ [Manual Installation & Integration](./manual.md)

---

## 🧠 Core Concepts

EfCore.Boost builds on familiar EF Core patterns but organizes them into a clear structure:

1. **DbContext** – the EF Core engine under the hood
2. **Unit of Work (UoW)** – your application-facing interface
3. **Repository pattern** – querying and manipulating data
4. **Factory pattern** – controls lifecycle and creation of UoW instances

→ [Unit of Work](./DbUow.md)  
→ [Repositories](./DbRepo.md)  
→ [UoW Factory Pattern](./uow-factory-pattern.md)

The UoW ties everything together and provides a transactional boundary for database operations.

EfCore.Boost also introduces structured handling of database routines:

- Functions / Stored procedures can be called through the UoW
- These run within the same transaction context as repositories
- Deployment of routines and views follows the same structured patterns

→ [UoW Routines](./DbUowRoutines.md)

---

## 🏗️ Model & Configuration

EfCore.Boost helps you build provider-agnostic models using conventions and attributes.

- Simplifies configuration across SQL Server, PostgreSQL, and MySQL
- Adds metadata-driven behavior through attributes

→ [Model Building](./ModelBuilding.md)

---

## ⚙️ Database & Migrations

EfCore.Boost separates model and migrations into dedicated projects.

Key ideas:

- Model project defines schema and conventions
- Migration project handles deployment and updates
- PowerShell scripts assist with migration generation
- SQL scripts are generated alongside migrations
- The migration project can also import larger seed datasets from CSV files

This approach supports both:

- Application-driven migrations
- Script-based (DBA-friendly) deployments

The migration project is therefore more than a migration runner. It is also the practical command-line tool for:

- creating the database
- applying migrations
- importing seed data
- deploying custom SQL for views and routines

→ [Migration Details](./EfMigrationsCMD.md)

---

## 🧪 Examples & Testing

The test projects serve two purposes:

1. **Validation of EfCore.Boost itself**
2. **Practical examples of how to use UoW and repositories**

They demonstrate:

- Multi-provider setup (SQL Server, PostgreSQL, MySQL)
- Use of test containers to spin up databases
- Reusable smoke tests across providers

→ [Testing & Examples](./tests/Readme.md)

---

## 🧩 Solution Templates

EfCore.Boost provides solution templates to quickly bootstrap projects.

These include:

- Model project (DbContext, UoW, entities)
- Migration project (deployment tooling)
- Optional test / web layers

You can:

- Start new solutions from templates
- Or copy the generated projects into existing solutions

→ [Using the Templates](./templates/UseTheTemplate.md)

---

## 📌 Recommended Path

**If you are new:**

1. Getting Started
2. Core Concepts (UoW + Repository)
3. Model Building
4. Migrations & deployment

**If you are integrating into an existing system:**

- Start from Templates
- Adapt Model + UoW
- Configure migrations

---

## 🏁 Philosophy

EfCore.Boost encourages:

- Explicit control over database access
- Clean separation between model, migration, and application code
- Treating SQL scripts as first-class deployment artifacts
- Patterns that scale from simple apps to enterprise environments

---

This documentation will evolve alongside the project.
