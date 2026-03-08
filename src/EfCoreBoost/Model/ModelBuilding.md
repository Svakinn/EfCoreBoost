# Model Building in EfCore.Boost
## Consistent Modeling Across SQL Server, PostgreSQL, and MySQL

EfCore.Boost builds directly on Entity Framework Core’s model-building foundations. You still define entities, relationships, views, and read models using normal EF Core techniques.

What EfBoost adds is:

- higher-level intent attributes  
- uniform conventions across database engines  
- predictable naming and schema behavior  
- correct timestamp handling  
- strong string and text semantics  
- safe and explicit foreign-key behavior  
- first-class support for views and read models  

The result is a model that behaves consistently across SQL Server, PostgreSQL, and MySQL — without scattering provider–specific decisions through your code.

---

# Applying EfBoost Conventions

EfBoost conventions are enabled with a single call inside your DbContext:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyEfBoostConventions(this);
}
```

This activates:

- provider-aware schema and naming
- EF view support
- Boost string attributes (StrShort, StrMed, etc.)
- UTC & timestamp normalization
- cascade delete policy
- Postgres-specific fixes (citext, timestamp)
- MySQL timezone handling
- view key support

EF continues to work normally — EfBoost simply aligns environments and removes ambiguity.

---

# Naming & Schema Conventions

EfBoost abstracts the differences between engines that support schemas and engines that do not.

## SQL Server & PostgreSQL
Schemas behave normally:

```
cms.Routes
dbo.Users
```

## MySQL
Since MySQL lacks schema namespaces in the same form, EfBoost maps schema and table as:

```
cms_Routes
core_Users
```

This keeps naming meaningful while remaining portable.

EfBoost ensures:

- consistent logical grouping
- readable migration SQL
- fewer conditional mappings
- identical entity definitions across providers

---

# Views and the ViewKey Attribute
## Views Are First-Class Citizens

A view in EfBoost is a supported, intentional read model.

```csharp
[ViewKey(nameof(Id)]
[DbSchema("cms", Table="CurrentMenuItemsV")]
public class CurrentMenuItemsV
{
    public long Id { get; set; }
}
```

EfBoost will:

- treat it as a view, not a table
- handle schema naming correctly
- declare the key EF requires
- ensure the model is read-oriented

Views:

- can be queried normally
- can act as OData sources
- can share types with routine result sets

EfBoost treats views as proper data access surfaces, not awkward EF hacks.

---

# Date & Timestamp Behavior

## Strategy

EfBoost standardizes timestamp behavior across engines:

- Normalizes EF mappings so semantics match
- Encourages UTC everywhere
- Prevents “server timezone surprise”
- Produces stable auditing and logging

## SQL Server
Uses `datetime2`, works correctly with UTC.

## PostgreSQL
EfBoost standardizes to:

```
timestamp without time zone
```

and expects client/application to store UTC explicitly.

This avoids confusing mixed interpretation of `timestamp with time zone` and ensures predictable comparison behavior.

## MySQL — Session Timezone Enforcement

EfBoost does **not assume** MySQL runs in UTC.

Instead:

> At the start of every MySQL database session, EfBoost explicitly sets the session timezone to UTC.

This guarantees:

- no drift across environments
- stable timestamps in distributed systems
- predictable data movement
- repeatable queries

## Recommendation

EfBoost strongly encourages:

- Store timestamps in UTC
- Convert only at UI boundaries
- Avoid server localization behavior

This produces deterministic results everywhere.

---

# String & Text Handling

Cross–database string rules are not uniform. EfBoost smooths differences while preserving power.

## Provider Baseline

SQL Server:
```
nvarchar(x)
```

MySQL:
```
varchar(x)
```

PostgreSQL:
- defaults to citext when appropriate
- requires citext extension enabled

### Why citext?

- case insensitive matching
- indexable
- stable search semantics
- reduces logic complexity

EfBoost encourages **case-insensitive designs**, especially for:

- indexed columns
- searchable text
- identifiers
- user-facing strings

EfBoost does not force case-insensitive collations on SQL Server or MySQL, but strongly recommends it.
PostgreSQL achieves it indirectly through citext.

---

# String Size Considerations

EfBoost’s StrShort, StrMed, StrLong, etc. define intent. They are chosen to be:

- portable across providers
- safely indexable
- practical for application use
- clear to reason about

A frequent fear is: “Does nvarchar length harm performance?”

Reality:

- SQL Server: size rarely matters unless massively abused
- MySQL: performs correctly within EfBoost ranges
- PostgreSQL: handles text types efficiently

Performance issues appear when giant text columns are indexed incorrectly or misused.
EfBoost’s sizing avoids most traps.

Anything beyond StrLong essentially behaves like free-text and normally should not be indexed.

So sizing provides:

- clarity
- portability
- safe indexing
- predictable migrations

---

# Primary Keys & Identity Behavior

EfBoost provides attributes that clarify identity intent.

```
[DbAutoUid]
```

- declares a property as a key
- aligns generation strategy per provider
- prevents multiple PK definitions
- stabilizes migrations

Guid identities are equally supported and consistently mapped.

EfBoost ensures identity behavior remains predictable during:

- migrations
- bulk insert
- replication
- restore/import scenarios

---

# Cascade Delete Policy
## Pain Avoided on Purpose

EF Core and many databases default to cascade deletes.

Enterprise reality: cascade deletes create more harm than good.

They often cause:

- unintended mass deletion
- unpredictable cascade chains
- circular dependency failures
- migration errors
- operational risk

EfBoost disables cascade deletes globally unless explicitly chosen.

Policy:

> Delete semantics must be intentional, not accidental.

Benefits:

- safer data lifecycle
- clearer reasoning
- fewer schema traps
- improved portability
- lowered operational risk

---

# Shared Read Models: Views & Routines

EfBoost supports a unified view-model concept.

A model class may represent:

- a database view
- a recordset returned from a routine

This allows:

- OData to operate over the view
- routines to return the same view structure
- consistency without duplication

Recommended usage pattern:

- expose views as repositories when appropriate
- treat routine results as read-only sources
- avoid mapping routines as repositories directly

Application stays consistent, predictable, and easier to maintain.

---

# About Migration Execution

Model building defines the database logically.  
Deployment builds it physically.

EfBoost supports disciplined cross‑database migration strategies. These are documented separately in:

📄 [EfMigrationsCMD.md](./EfMigrationsCMD.md)

That document covers:

- generating migrations per provider
- consistent schema deployment
- handling EF migration snapshot limitations
- automation using PowerShell / command scripts

---
# Examples of Applying EfBoost Attributes

This section demonstrates typical attribute usage and how design intent becomes explicit in the model.



## Logging Domain Example

### ErrorLog

```csharp
[Index(nameof(LastChangedUtc), IsUnique = false, AllDescending = true)]
[Index(nameof(SessionId), IsUnique = false)]
[Index(nameof(Context), nameof(LastChangedUtc), nameof(SessionId), IsUnique = false)]
public class ErrorLog
{
    [DbAutoUid]
    public long Id { get; set; }

    public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;

    public long? SessionId { get; set; }

    public int Context { get; set; }

    [StrMed]
    public string? ErrorMsg { get; set; }

    [Text]
    public string? ErrorDetails { get; set; }

    [ForeignKey(nameof(Context))]
    public LogContext? LogContext { get; set; }

    public int Tenant { get; set; } = 1;
}
```

**Why this matters**

- `[DbAutoUid]` ensures consistent identity handling across databases  
- `[StrMed]` defines intent and keeps size/indexing portable  
- `[Text]` communicates “large text, not for indexing”.   
Note: This not strictly neccesary attribute since the end result is the same as if we skip this.   
However it is a good documentation about our intention and that we did not simply forget to mark the string length. 

---

### LogContext

```csharp
[Index(nameof(Ctx), nameof(Tenant), IsUnique = true)]
public class LogContext
{
    [DbAutoUid]
    public int Id { get; set; }

    public int Ctx { get; set; }

    [StrMed]
    public string Name { get; set; } = string.Empty;

    [Text]
    public string? Description { get; set; }

    public int LogTypeId { get; set; } = 1;

    public int Tenant { get; set; } = 1;
}
```

Clear model intent:

- Explicit uniqueness rules  
- Case-insensitive + index-friendly naming   

---

## View Example with Composite ViewKey

EfBoost treats views as first-class read models.  
`ViewKey` declares an EF identity for tracking and correctness.

```csharp
[ViewKey(nameof(LoginId), nameof(CustId), nameof(Code))]
public class LoginPermissionsV
{
    public long LoginId { get; set; }
    public long CustId { get; set; }

    [StrCode]
    public string Code { get; set; } = string.Empty;

    public int ModuleId { get; set; }

    public bool IsExternal { get; set; } = false;
    public bool IsInternal { get; set; } = false;
}
```

Key advantages:

- Stable composite key without artificial surrogate IDs  
- `[StrCode]` expresses “short identifier, case-insensitive, index-safe”  
- Pure read model with clear semantics  
- Works equally well whether backed by a real DB view or routine result  


---

## Custom Attributes

EfBoost custom attributes fall into two clearly separated groups:

1. **Model & relationship attributes**  
   These influence how EF Core reasons about the model: schema placement, keys, identity strategy, and relationship behavior.  
   They do **not** define column size or numeric precision.

2. **Column intent attributes (string & decimal)**  
   These describe the *semantic intent* of a column (code, text, money, quantity, etc.).  
   Boost translates that intent into **consistent, provider-correct column definitions**, ensuring data is stored uniformly across SQL Server, PostgreSQL, and MySQL.

Defaults when no attribute is specified:
- **string**: EF Core default mapping (`text` / provider equivalent), equivalent to `[Text]`
- **decimal**: `decimal(19,4)` (previously provider-specific, now unified)

Boost intentionally does **not** enforce precision conventions for `float` or `double`.  
In application-level data modeling, `decimal` is the natural choice for fixed-precision values such as money, rates, and quantities. 
It provides deterministic precision across providers and avoids rounding artifacts inherent in binary floating-point types.  
`float` and `double` are designed for scientific and computational scenarios where approximate values and wide dynamic range are more important than exact decimal representation.  
For this reason, Boost standardizes precision for `decimal` only.

---

### Views, schema, and identity

| Attribute | Applies to | Purpose |
|---|---|---|
| `ViewKey` | Class (view/read model) | Declares the EF key for a view or read-model so it can be queried correctly. |
| `DbSchema` | Class | Declares the logical schema (and optionally object name) in a provider-aware way. |
| `DbUid` | Property | Marks a numeric identity column (identity / sequence / auto-increment depending on provider). |
| `DbGuid` | Property | Marks a GUID column with provider-correct generation semantics. The value is unique but **not necessarily a primary key or identifier**. |

`DbUid` expresses **identity semantics**.  
`DbGuid` expresses **GUID generation**, independent of key or identity usage.

---

### Relationship override attributes

These override the **default cascade policy** documented earlier and are intended for local exceptions only.

| Attribute | Applies to | Purpose |
|---|---|---|
| `NoCascadeDelete` | Navigation / relationship | Explicitly disables cascade delete for a specific relationship. |
| `CascadeDelete` | Navigation / relationship | Explicitly enables cascade delete for a specific relationship. |

---

### String length & text intent

These attributes express **string intent**, not raw database types.  
Boost applies consistent provider-specific column definitions and safe defaults.

| Attribute | Length applied | Purpose |
|---|---:|---|
| `StrCode` | 30 | Short identifiers or codes (index-friendly). |
| `StrShort` | 50 | Short application strings (names, labels). |
| `StrMed` | 256 | Medium-length application strings. |
| `StrLong` | 512 | Long application strings. |
| `Text` | unbounded | Free or unbounded text intent (not index-oriented). |

Default (no attribute): EF Core default string mapping (`text` / provider equivalent), equivalent to `[Text]`.

### Semantic string attributes

However, if you want **more expressive intent on your columns**, EfBoost
also provides a set of **semantic string attributes**. These can be used
instead of the raw `Str*` attributes. They communicate the **purpose of
the data** rather than just the maximum length of the column.

Internally, Boost maps these semantic attributes to one of the standard
string buckets:

`StrCode`, `StrShort`, `StrMed`, `StrLong`, or `Text`.

This keeps database schemas consistent while still allowing the model to
clearly express *what the data represents*.

| Attribute | Maps to | Typical usage |
|---|---|---|
|`CountryCode`|`StrCode`|ISO country codes (`IS`, `US`, `DE`).|
|`CurrencyCode`|`StrCode`|ISO currency codes (`ISK`, `EUR`, `USD`).|
|`LanguageCode`|`StrCode`|ISO language codes (`en`, `is`).|
|`CultureCode`|`StrCode`|Culture identifiers such as locale codes (`en-GB`, `is-IS`).|
|`MimeType`|`StrShort`|MIME content type identifiers.|
|`AddressPostalCode`|`StrShort`|Postal or ZIP codes.|
|`AddressStreetNumber`|`StrShort`|Street number part of an address.|
|`AddressBuildingUnit`|`StrShort`|Apartment, suite, or building unit identifiers.|
|`Phone`|`StrShort`|Telephone numbers.|
|`UserName`|`StrShort`|Login or account user names.|
|`Name`|`StrMed`|Names of people, entities, or objects.|
|`Title`|`StrMed`|Titles or headings.|
|`ExternalRef`|`StrMed`|Identifiers referencing external systems.|
|`AddressStreetName`|`StrMed`|Street names in addresses.|
|`AddressCity`|`StrMed`|City or town names.|
|`AddressAdminArea`|`StrMed`|Administrative region, state, or province.|
|`AddressRecepientName`|`StrMed`|Recipient name used in address fields.|
|`Email`|`StrLong`|Email addresses.|
|`Url`|`StrLong`|Web addresses or resource URLs.|
|`FileName`|`StrLong`|File names including extensions.|
|`Html`|`Text`|Stored HTML markup content.|
|`Markdown`|`Text`|Markdown formatted content.|
|`RichText`|`Text`|Rich text editor output.|
|`Json`|`Text`|JSON structured content.|
|`Editor`|`Text`|Editor JSON payloads such as TipTap content.|

#### Notes

-   Only **one string intent attribute** should be applied to a
    property.

---

### Decimal precision intent

These attributes express **numeric intent** rather than raw precision.  
Boost enforces uniform precision and scale across providers for `decimal` only.

| Attribute | Precision / Scale | Typical meaning |
|---|---:|---|
| `Percentage` | 18,8 | Percentages and ratios. |
| `Qty` | 18,8 | Quantities. |
| `Rate` | 18,8 | Rates and factors. |
| `Price` | 19,4 | Prices. |
| `Money` | 19,4 | Monetary values. |
| `SortRank` | 38,19 | Ranking or scoring values. |
| `Scientific` | 38,19 | High-precision scientific values. |
| `Longitude` | 9,6 | Geocraphic longitude |
| `Latitude` | 9,6 | Geocraphic latitude. |

Default (no attribute): `decimal(19,4)`.
> Of course, you may specify the standard EF Core precision attribute instead, for example:
> `[Precision(16, 3)]`
>
> Explicit precision configuration always overrides Boost conventions.

---

### Date and time types

Boost standardizes timestamp handling to avoid provider-specific ambiguity.

Recommended usage:

- Use `DateTimeOffset` for persisted timestamps.
- Store all timestamps in UTC.
- Convert to local time only at UI boundaries.

Boost ensures:

- SQL Server uses `datetime2`
- PostgreSQL uses `timestamp without time zone`
- MySQL sessions are forced to UTC

Boost does not rely on database-local timezone interpretation.
Application code is responsible for consistent UTC storage.

This prevents:

- server timezone drift
- daylight saving surprises
- cross-environment inconsistencies
- replication confusion

---

### Semantic marker attributes

These attributes **do not change the database schema**.  
They exist to express **common domain semantics and allow conventions**, tooling, or libraries to recognize standard column meanings.

| Attribute | Applies to | Typical usage |
|---|---|---|
|`Media`|`byte[]`|Binary media content such as images, files, or attachments stored in the database.|
|`Hash`|`byte[]`|Cryptographic hashes such as password hashes or integrity hashes.|
|`Salt`|`byte[]`|Salt values used together with hashed secrets.|
|`Encrypted`|`byte[]`|Encrypted binary payloads or encrypted field storage.|
|`SigningKey`|`byte[]`|Binary signing keys or cryptographic key material used for token signing or verification.|
|`SoftDelete`|`bool` or `DateTime`|Marks a column used for soft-delete state or deletion timestamp (for example `IsDeleted`).|
|`LastChangedUtc`|`DateTime`|Timestamp for the last modification of the row.|
|`CreatedUtc`|`DateTime`|Timestamp when the record was created.|
|`ValidFromUtc`|`DateTime`|Start of a validity period for temporal data.|
|`ValidToUtc`|`DateTime`|End of a validity period for temporal data.|
|`ExpiresUtc`|`DateTime`|Expiration timestamp for temporary or expiring data.|
|`Tenant`|`long`, `int`, or `Guid`|Tenant identifier used for multi-tenant data partitioning.|
|`Status`|`int`, `short`, or `string`|Application-defined status or state indicator.|
---

### Notes on usage

- Only **one string** or **one decimal** intent attribute should be applied per property.
- Explicit fluent configuration always overrides conventions.
- Floating-point types (`float`, `double`) are intentionally left untouched.
- These attributes exist to keep models **portable, predictable, and consistent** across database providers.

---

### Example usage

For a bulk insert demo we displayd this example model:
```csharp
[Index(nameof(LastChangedUtc), IsUnique = false, AllDescending = true)]
[Index(nameof(SessionId), IsUnique = false)]
[Index(nameof(Context), nameof(LastChangedUtc), nameof(SessionId), IsUnique = false)]
public class ErrorLog
{
    [DbAutoUid]
    public long Id { get; set; }
    public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;
    public long? SessionId { get; set; }
    public int Context { get; set; }
    [StrMed]
    public string? ErrorMsg { get; set; }
    [Text]
    public string? ErrorDetails { get; set; }
    [ForeignKey(nameof(Context))]
    public LogContext? LogContext { get; set; }
    public int Tenant { get; set; } = 1;
}

[Index(nameof(Ctx), nameof(Tenant), IsUnique = true)]
public class LogContext
{
    [DbAutoUid]
    public int Id { get; set; }
    public int Ctx { get; set; }
    [StrMed]
    public string Name { get; set; } = string.Empty;
    [Text]
    public string? Description { get; set; }
    public int LogTypeId { get; set; } = 1;
    public int Tenant { get; set; } = 1;
}
```
We could make the model more expressive by using the semantic attributes provided by EfBoost.  
These attributes describe the purpose of the data, enabling clearer domain models while also allowing conventions and tooling to infer appropriate database mappings.  

Using these attributes is entirely optional. Projects may choose to rely only on the basic Str* attributes, or define their own semantic attributes and conventions following the same pattern.

```csharp
[Index(nameof(LastChangedUtc), IsUnique = false, AllDescending = true)]
[Index(nameof(SessionId), IsUnique = false)]
[Index(nameof(Context), nameof(LastChangedUtc), nameof(SessionId), IsUnique = false)]
public class ErrorLog
{
    [DbAutoUid]
    public long Id { get; set; }
    [LastChangedUtc]
    public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;
    [ExternalRef]
    public long? SessionId { get; set; }  //Note: this point s to sessio-info record not current session record
    [ExternalRef]
    public int Context {  get; set; }
    [Title]
    public string? ErrorMsg { get; set; }
    [Text]
    public string? ErrorDetails { get; set; }
    [ForeignKey(nameof(Context))]
    public LogContext? LogContext { get; set; }
    [Tenant]
    public int Tenant { get; set; } = 1;
}

[Index(nameof(Ctx), nameof(Tenant), IsUnique = true)]
public class LogContext
{
    [DbAutoUid]
    public int Id { get; set; }
    [ExternalRef]
    public int Ctx { get; set; }
    [Name]
    public string Name { get; set; } = string.Empty;
    [Text]
    public string? Description { get; set; }
    [ExternalRef]
    public int LogTypeId { get; set; } = 1;
    [Tenant]
    public int Tenant { get; set; } = 1;
}
```

---

# Summary

EfCore.Boost Model Building provides:

✔ Consistent cross‑database modeling  
✔ Stable and safe timestamp handling  
✔ Practical string & collation strategy  
✔ Strong identity conventions  
✔ Explicit cascade delete control  
✔ First‑class views and read models  
✔ MySQL timezone safety  
✔ PostgreSQL citext normalization  

EfBoost doesn’t replace EF Core philosophy — it stabilizes it for multi‑database, enterprise‑scale systems, so you focus on architecture instead of vendor quirks.
