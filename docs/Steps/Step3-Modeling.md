# Step 3: Modeling the Domain

In this step, we will define our database tables. We'll replace the default template entities with our own: `AnimalType` and `Pet`.

## 3.1 Define the Entities

Open the `MyPets.Model` project and create these two classes. We use EfCore.Boost attributes to define the database schema consistently.

### AnimalType.cs
This table stores categories like "Cat", "Dog", or "Parrot".

```csharp
using EfCore.Boost.Model.Attributes;

public class AnimalType
{
    [DbAutoUid]
    public int Id { get; set; }
    [Name]
    public string Name { get; set; } = null!;
}
```

### Pet.cs
This table stores individual pets and links them to an `AnimalType`.

```csharp
using EfCore.Boost.Model.Attributes;
using System.ComponentModel.DataAnnotations.Schema;

[Index(nameof(Name), IsUnique = false)]
[Index(nameof(CreatedUtc), AllDescending = true)]
public class Pet
{
    [DbAutoUid]
    public long Id { get; set; }
    [Name]
    public string Name { get; set; } = null!;
    [StrMed]
    public string? Breed { get; set; }
    public int AnimalTypeId { get; set; }
    public int BirthYear { get; set; }
    [CreatedUtc]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AnimalTypeId))]
    public AnimalType AnimalType { get; set; } = null!;
}
```

## 3.2 Key Attributes Used
EfCore.Boost uses **string intent attributes** to ensure consistent column sizes and indexing across all database providers. These attributes map to standardized "size buckets":

- `[DbAutoUid]`: Marks the property as an auto-incrementing primary key.
- `[Name]`: A semantic string attribute that maps to the **StrMed** bucket (256 characters). It is ideal for names, titles, and searchable labels.
- `[StrMed]`: Medium-length string bucket (256 characters).
- `[CreatedUtc]`: Marks the property as the record creation timestamp in UTC.
- `[Index]`: Defines a database index. `AllDescending = true` is used for the `CreatedUtc` index to optimize for "newest first" queries.
- `[ForeignKey]`: Standard EF Core attribute to explicitly define the foreign key relationship.

### String Size Buckets
For reference, EfCore.Boost defines these standard buckets:
- **StrCode**: 30 characters (for identifiers/codes).
- **StrShort**: 50 characters (for short labels).
- **StrMed**: 256 characters (for names/titles).
- **StrLong**: 512 characters (for descriptions/URLs).
- **Text**: Unbounded (for large content).

## 3.3 Attributes vs. Fluent API

The attribute-based approach shown above is generally preferred in EfCore.Boost because it keeps the model definition and database schema configuration in one place. It is clear, easy to reason about, and provides an immediate understanding of how each property maps to the database.

However, if you prefer not to use attributes on your POCO classes—or if you need to refer to external classes or complex configurations often found in Domain Driven Design (DDD)—you can achieve the same result using the Fluent API in your `DbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Pet>(b =>
    {
        b.Property(x => x.Id).HasDbAutoUid();
        b.Property(x => x.Name).HasPurposeName();
        b.Property(x => x.Breed).HasPurposeStrMed();
        b.Property(x => x.CreatedUtc).HasPurposeCreatedUtc();
    });
}
```

For more details on available attributes and their fluent counterparts, see the [Model Building Guide](../ModelBuilding.md).

---

[Next: Creating Views >](Step4-Views.md)
