# Step 7: Cross-Platform Testing

Before we deploy to our local environment, we verify our implementation using the `MyPets.Test` project. This project is specifically designed to ensure that the data layer works correctly across all supported database providers using **TestContainers**.

## 7.1 Modifying UnitTestContainers.cs

Open the `UnitTestContainers.cs` file in the `MyPets.Test` project. This file contains the logic for spinning up database containers and running smoke tests.

### Update the Import Logic

First, locate the `ImportAsync` method. Just like we did for the `Migrate` project in Step 6, we need to update this method to import our `AnimalType` and `Pet` CSV files.

```csharp
static async Task ImportAsync(MyPetsUow uow)
{
    Console.WriteLine("--- Starting Import ---");
    await uow.RunInTransactionAsync(async (ct) =>
    {
        // 1. Import AnimalTypes
        var animalTypeFile = "AnimalType.csv";
        var atPath = ImportHelper<AnimalType>.GetCsvPath(animalTypeFile);
        if (File.Exists(atPath))
        {
            var helper = new ImportHelper<AnimalType>(uow.AnimalTypes, atPath);
            var firstRow = await helper.ReadFirstRowAsync();
            if (firstRow != null && await uow.AnimalTypes.RowByIdUnTrackedAsync(firstRow.Id) != null)
                Console.WriteLine("Seed data already exists. Skipping.");
            else
                await ImportHelper<AnimalType>.ImportAsync(uow.AnimalTypes, animalTypeFile, 100, true);
        }

        // 2. Import Pets
        var petFile = "Pet.csv";
        if (File.Exists(ImportHelper<Pet>.GetCsvPath(petFile)))
            await ImportHelper<Pet>.ImportAsync(uow.Pets, petFile, 1000, true);
    });
    Console.WriteLine("--- Import Finished ---");
}
```

### Update the Smoke Test

Next, locate the `BasicSmokeAsync` method. This is where we define the actual tests that run against each database provider. We will add logic to verify our new tables and the `PetDetails` view.

```csharp
static async Task BasicSmokeAsync(MyPetsUow uow)
{
    // 1. Add a new row for an animal type
    var newType = new AnimalType { Name = "Hamster" };
    uow.AnimalTypes.Add(newType);
    await uow.SaveChangesAsync();
    Assert.AreNotEqual(0, newType.Id);

    // 2. Query the second pet from the seeded data
    var secondPet = await uow.Pets.QueryUnTracked().FirstOrDefaultAsync(p => p.Id == 2);
    Assert.IsNotNull(secondPet);
    Assert.AreEqual("Fido", secondPet.Name);

    // 3. Query the PetDetails view
    var viewItem = await uow.PetDetails.QueryUnTracked().FirstOrDefaultAsync(p => p.Id == 2);
    Assert.IsNotNull(viewItem);
    Assert.AreEqual("Dog", viewItem.AnimalTypeName);
    Assert.AreEqual("Fido", viewItem.PetName);
}
```

## 7.2 Why use TestContainers?

The test project uses **TestContainers** to spin up real instances of SQL Server, PostgreSQL, and MySQL in Docker. This is crucial because:
1. **Manual SQL**: It verifies that your provider-specific View definitions are syntactically correct for each engine.
2. **Provider Mapping**: It ensures that EF Core mappings and Boost attributes (like `[CreatedUtc]`) behave correctly on each specific database.
3. **Full Lifecycle**: It runs a complete "create, migrate, seed, read" lifecycle to catch configuration issues early.

## 7.3 Running the Tests

Run the tests from your IDE's Test Explorer. You should see tests for `Uow_MsSql_Test`, `Uow_Postgres_Test`, and `Uow_MySql_Test`.

> **Note**: You need **Docker** installed and running on your computer to execute these tests, as they rely on TestContainers to spin up real database instances. If you don't have it, please install [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or a compatible alternative).

If these tests pass, you have high confidence that your domain model, views, and migrations are working perfectly across all three major database platforms!

---

[Next: Local Deployment >](Step8-Deployment.md)
