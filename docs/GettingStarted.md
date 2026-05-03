# Getting Started with EfCore.Boost

EfCore.Boost is designed to make Entity Framework Core development faster, more consistent, and multi-provider by default. Instead of a simple library, it provides a comprehensive workflow for building data models, managing migrations, and deploying databases.

## How to use this guide

This guide is structured as a step-by-step journey where we build a simple "MyPets" application from scratch. We recommend following the steps in order:

### 🚀 The 9-Step Journey

1.  **[Step 1: Download & Apply Template](Steps/Step1-Template.md)**  
    Learn how to install the EfCore.Boost templates and create your first solution named `MyPets`.

2.  **[Step 2: Project Structure Overview](Steps/Step2-ProjectStructure.md)**  
    Understand the purpose of the `.Model`, `.Migrate`, and `.Test` projects.

3.  **[Step 3: Modeling the Domain](Steps/Step3-Modeling.md)**  
    Define your entities (`AnimalType` and `Pet`) using Boost attributes like `[DbAutoUid]` and `[Name]`.

4.  **[Step 4: Creating Views](Steps/Step4-Views.md)**  
    Learn how to create read-only views, place them in the correct SQL files, and use AI to convert them for PostgreSQL and MySQL.

5.  **[Step 5: Running Migrations](Steps/Step5-Migrations.md)**  
    Use the PowerShell scripts in the `Ps` folder to generate provider-specific SQL deployment scripts.

6.  **[Step 6: Seed Data](Steps/Step6-SeedData.md)**  
    Populate your database using simple CSV files without cluttering your C# code.

7.  **[Step 7: Smoke Testing](Steps/Step7-Testing.md)**  
    Verify your model, views, and data using the built-in test project.

8.  **[Step 8: Local Deployment](Steps/Step8-Deployment.md)**  
    Deploy your database to a local server using the migration command-line tool.

9.  **[Step 9: Business Logic and Unit of Work](Steps/Step9-Logic.md)**  
    Integrate the database model and Unit of Work into your API and business logic layer.

---

## Core Concepts

If you are already familiar with the basics and want to dive deeper into specific topics, check out these resources:

-   **[Model Building Guide](ModelBuilding.md)**: A comprehensive list of all attributes and fluent API options.
-   **[Why EfCore.Boost?](WhyBoost.md)**: The philosophy and advantages of using this library.
-   **[Unit of Work Patterns](DbUow.md)**: How to use the generated UoW and repositories in your application.
-   **[Migrate Project Details](../TemplateWork/Boost.Simple/BoostX.Migrate/README.md)**: Deep dive into the migration tool and PowerShell scripts.

---

## Ready to start?

Go to **[Step 1: Download & Apply Template](Steps/Step1-Template.md)** to begin!
