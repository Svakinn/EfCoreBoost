# Step 9: Business Logic and Unit of Work

> [!IMPORTANT]
> The API project provided in the template is a simplistic example designed to demonstrate how to use the Unit of Work. It is not intended to be a definitive template for application infrastructure or complex architecture.

In this final step, we'll look at how to use our database model and the Unit of Work in the API project. This is where your application's business logic (BLL) resides.

## 9.1 Registering Services in Program.cs

To make our Unit of Work factory and business logic available throughout the API, we need to register them in the Dependency Injection (DI) container. Open `Program.cs` in the `MyPets.Api` project.

Locate the service registration section and update it as follows:

```csharp
// Inject our UoW-Factory and business logic (PetLogic) to the DI container
builder.Services.AddSingleton<IUowMyPetsFactory, UowMyPetsFactory>();
builder.Services.AddScoped<PetLogic>();
```

**Key Configuration Details:**
- **Singleton Factory**: We register `IUowMyPetsFactory` as a **Singleton**. Since the factory itself doesn't hold any request-specific state and is used only to create UoW instances, one instance for the entire application lifetime is most efficient.
- **Scoped Logic**: We register `PetLogic` as **Scoped**. This ensures that a new instance of our business logic is created for each HTTP request, aligning with the lifecycle of the Unit of Work it will eventually use.
- **Background Workers**: While the template includes an `IpBackgroundWorker`, we can remove it if our application doesn't require background processing.

## 9.2 The UoW Factory

When working with the Business Logic Layer, we don't inject the Unit of Work (`MyPetsUow`) directly. Instead, we inject the `IUowMyPetsFactory`. 

**Why the Factory?**
The factory is extremely lightweight. It allows for lazy initialization of the Unit of Work, ensuring that a database connection is only opened when it's actually needed. This is a best practice for maintaining a responsive and scalable API.

## 9.3 Implementing PetLogic

Open the `BLL` folder in the `MyPets.Api` project. You'll see some example files from the template. You can remove these and create your own. For our project, let's rename `IpLogic.cs` to `PetLogic.cs` and adapt it for our pets.

Here is how you might implement `PetLogic` to handle adding pets and querying our `PetDetails` view using OData:

```csharp
using MyPets.Api.DTO;
using MyPets.Model;
using EfCore.Boost.DbRepo;
using Microsoft.AspNetCore.OData.Query;

namespace MyPets.Api.BLL;

public sealed class PetLogic(IUowMyPetsFactory uowMyPetsFactory) 
{
    private readonly IUowMyPetsFactory _uowMyPetsFactory = uowMyPetsFactory;
    private MyPetsUow? _uowMyPets;

    // Lazy initialization of the Unit of Work
    private MyPetsUow UoW => _uowMyPets ??= _uowMyPetsFactory.Create(); 

    /// <summary>
    /// Adds a new pet to the database and returns the created entity.
    /// </summary>
    public async Task<Pet> AddPet(string name, int animalTypeId, int birthYear)
    {
        var pet = new Pet
        {
            Name = name,
            AnimalTypeId = animalTypeId,
            BirthYear = birthYear,
            CreatedUtc = DateTime.UtcNow
        };
        UoW.Pets.Add(pet);
        await UoW.SaveChangesAsync();
        return pet;
    }

    /// <summary>
    /// Demonstrates OData filtered queries for the PetDetails view
    /// </summary>
    public async Task<QueryResult<MyPetsCTX.PetDetails>> ListPets(ODataQueryOptions<MyPetsCTX.PetDetails> options, CancellationToken ct)
    {
        // Base query - we can add default filters here if needed
        var baseQuery = UoW.PetDetails.QueryUnTracked();
        // The client determines filters, paging, and sorting via OData parameters
        return await UoW.PetDetails.FilterODataAsync(baseQuery, options, null, false, ct);
    }
}
```

### Key Takeaways:
- **Injection**: We inject `IUowMyPetsFactory` via the constructor.
- **Lazy Loading**: The `UoW` property ensures we only create the `MyPetsUow` instance when first accessed.
- **View Access**: Querying the `PetDetails` view is as simple as querying any other table.
- **OData Support**: The `FilterODataAsync` method makes it incredibly easy to provide powerful querying capabilities to your API clients.

## 9.4 Updating the Controller

Finally, don't forget to update your API controllers. Just like we renamed `IpLogic` to `PetLogic`, you should rename `IpController.cs` to `PetController.cs` (or create a new one).

In the controller, you will inject `PetLogic` and create endpoints that call the methods we implemented. This keeps your controllers thin and your business logic centralized in the BLL.

## Summary

Congratulations! You have successfully completed the EfCore.Boost "Getting Started" journey. You have:
- Created a new solution from a template.
- Defined a domain model with `AnimalType` and `Pet`.
- Created a custom database view `PetDetails`.
- Generated and applied provider-specific migrations.
- Populated the database with seed data.
- Verified everything with cross-platform tests.
- Implemented business logic using the Unit of Work and OData.

You now have a solid, professional-grade data foundation ready for your next application!

---

[Back to Overview](../GettingStarted.md)
