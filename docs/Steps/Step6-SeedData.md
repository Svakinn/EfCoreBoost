# Step 6: Seed Data

To make our application useful from day one, we need to populate the database with some initial data. EfCore.Boost provides two main ways to seed data: **Model-based seeding** (`HasData`) and **Bulk Import** (CSV files).

## 6.1 Model-based Seeding (HasData)

The `MyPets.Model` project shows an example of using EF Core's `HasData` in the `OnModelData` method. This is suitable for small, static lookup tables. However, this data is baked into your migrations and can bloat your model if used for large datasets.

## 6.2 Bulk Import via CSV

For larger datasets or one-time imports, we recommend using the Bulk Import feature. This keeps your model clean of one-time use data.

### Create the Data Files

Create a `Data` folder in the `MyPets.Migrate` project, and inside it, use the `CSV` folder. Create your CSV files there. The filenames must match your entity names.
Remember that the AI can be helpful here!

### AnimalType.csv
```csv
Id,Name
1,Cat
2,Dog
3,Parrot
```

### Pet.csv
```csv
Id,Name,Breed,AnimalTypeId,BirthYear,CreatedUtc
1,Whiskers,Siamese,1,2020,2024-01-01 10:00:00
2,Fido,Golden Retriever,2,2018,2024-01-01 10:05:00
3,Polly,African Grey,3,2022,2024-01-01 10:10:00
```

## 6.3 Configure the Import

In your `MyPets.Migrate` project, ensure that the files in the `CSV` folder are copied to the output directory. You can set this in the `.csproj` file or via the IDE properties (Copy if newer).

## 6.4 Why CSV for Seeding?
- **Readability**: Easy to edit in Excel or any text editor.
- **Performance**: EfCore.Boost's import tool is optimized for fast data loading.
- **Clean Model**: Unlike EF Core's `HasData`, this doesn't clutter your `OnModelCreating` or your migration files with thousands of lines of data.

## 6.5 Implementing the Import Service

The actual import logic is handled in the `ImportService.cs` file within your `MyPets.Migrate` project. This is where the magic happens: by combining the power of EF Core entities with optimized bulk insert operations, EfCore.Boost makes large-scale data imports both incredibly efficient and trivial to set up.

You simply use your existing domain models to define the import, and the `ImportHelper` handles the heavy lifting of mapping and high-speed database insertion.

### Modify ImportService.cs

Update the `ExecuteAsync` method to import `AnimalType` and `Pet` data. We use the `AnimalType` table to check if the data has already been imported (specifically looking for the "Cat" entry).

```csharp
public static async Task ExecuteAsync(MyPetsUow uow)
{
    Console.WriteLine("--- Starting Import ---");
    await uow.RunInTransactionAsync(async (ct) =>
    {
        // 1. Import AnimalTypes
        Console.WriteLine("Importing Animal Types...");
        var animalTypeFile = "AnimalType.csv";
        var atPath = ImportHelper<AnimalType>.GetCsvPath(animalTypeFile);
        if (File.Exists(atPath))
        {
            var helper = new ImportHelper<AnimalType>(uow.AnimalTypes, atPath);
            var firstRow = await helper.ReadFirstRowAsync();
            // Check if "Cat" (ID 1) already exists to avoid duplicate import
            if (firstRow != null && await uow.AnimalTypes.RowByIdUnTrackedAsync(firstRow.Id) != null)
            {
                Console.WriteLine("Seed data already exists. Skipping import.");
                return; 
            }
            await ImportHelper<AnimalType>.ImportAsync(uow.AnimalTypes, animalTypeFile, 100, true);
        }
        // 2. Import Pets
        Console.WriteLine("Importing Pets...");
        var petFile = "Pet.csv";
        if (File.Exists(ImportHelper<Pet>.GetCsvPath(petFile)))
            await ImportHelper<Pet>.ImportAsync(uow.Pets, petFile, 1000, true);
    });
    Console.WriteLine("--- Import Finished ---");
}
```

### Key Points:
- **Efficiency**: The combination of strongly-typed EF Core entities and bulk insert operations ensures that even millions of rows can be imported in seconds, without the overhead of tracking or individual INSERT statements.
- **Ease of Setup**: You don't need separate DTOs or complex mapping logic; the `ImportHelper` works directly with your domain models.
- **Transaction**: The entire import runs within a transaction. If one part fails, everything is rolled back.
- **Identity Check**: We check for the first row's ID (e.g., ID 1 for Cat) to decide if we should skip the import. This is a simple way to make the operation idempotent.
- **Batch Size**: The `ImportAsync` method takes a batch size (e.g., 1000 or 10,000) for performance.

---

[Next: Testing >](Step7-Testing.md)
