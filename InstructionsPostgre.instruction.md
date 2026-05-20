---
description: "PostgreSQL & EF Core Mapping Guidelines"
applyTo: "src/ClaimManager.Infrastructure/**/*.cs"
owner: Tayssir + Marouen 
---

# PostgreSQL Persistence Rules — ClaimManager

> **Scope:** These rules apply to all infrastructure-layer code under
> `src/ClaimManager.Infrastructure/`. Every Pull Request touching persistence
> logic MUST comply with the rules below before merge.

---

## 1. Naming Conventions

### 1.1 General Casing

| Object | Convention | Example |
|---|---|---|
| Tables | `snake_case`, plural | `claims`, `claim_events` |
| Columns | `snake_case` | `claim_id`, `created_at` |
| Indexes | `ix_<table>_<column(s)>` | `ix_claims_created_at` |
| Unique indexes | `uix_<table>_<column(s)>` | `uix_claims_policy_number` |
| Foreign keys | `fk_<table>_<referenced_table>` | `fk_claims_users` |
| Primary keys | `pk_<table>` | `pk_claims` |
| Check constraints | `ck_<table>_<rule>` | `ck_claims_amount_positive` |
| Sequences | `seq_<table>_<column>` | `seq_claims_claim_number` |

- **Never** use PascalCase, camelCase, or Hungarian notation for any database object.
- Abbreviations are allowed only for well-known domain terms (e.g., `ref` for reference, `amt` for amount). Maintain a project-level glossary at `docs/db-abbreviations.md`.

### 1.2 Table Names

- Use the **plural** form of the aggregate root name: `claims`, `policies`, `users`.
- Junction/link tables follow the pattern `<table_a>_<table_b>` in alphabetical order: `claim_documents`, `claim_tags`.
- Audit/history shadow tables are suffixed `_history`: `claims_history`.

### 1.3 Column Names

- Primary key columns MUST be named `id` (never `claim_id` on the owning table itself).
- Foreign key columns MUST match the pattern `<referenced_table_singular>_id`: `user_id`, `policy_id`.
- Soft-delete flag: `is_deleted` (boolean, NOT NULL, DEFAULT false).

### 1.4 Enum Columns

- Store enums as `VARCHAR` with a `CHECK` constraint, **not** as PostgreSQL `ENUM` types (avoids costly `ALTER TYPE` migrations).

```sql
status VARCHAR(64) NOT NULL
  CONSTRAINT ck_claims_status
  CHECK (status IN ('Draft','Submitted','UnderReview','Approved','Rejected','Closed'))
```

---

## 2. EF Core Mapping

### 2.1 General Principles

- **Always** use the **Fluent API** inside `OnModelCreating`; never place `[Column]`, `[Table]`, `[Key]`, or any other persistence attribute on Domain Entities.
- Place each entity's configuration in a dedicated class implementing `IEntityTypeConfiguration<T>`, stored under `Persistence/Configurations/`.
- Register all configurations automatically:

```csharp
// ClaimManagerDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("public");
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimManagerDbContext).Assembly);
}
```

### 2.2 File & Class Structure

```
src/ClaimManager.Infrastructure/
└── Persistence/
    ├── ClaimManagerDbContext.cs
    ├── Configurations/
    │   ├── ClaimConfiguration.cs
    │   ├── PolicyConfiguration.cs
    │   ├── UserConfiguration.cs
    │   └── ClaimEventConfiguration.cs
    └── Migrations/
```

Each configuration file follows this skeleton:

```csharp
// Persistence/Configurations/ClaimConfiguration.cs
internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("claims");

        builder.HasKey(c => c.Id)
               .HasName("pk_claims");

        builder.Property(c => c.Id)
               .HasColumnName("id")
               .HasColumnType("uuid")
               .HasDefaultValueSql("gen_random_uuid()");

        // ... remaining mappings
    }
}
```

### 2.3 Primary Keys

- Use `UUID` (Guid) for all primary keys. Generate server-side with `gen_random_uuid()`.
- Never use `SERIAL` / `BIGSERIAL` auto-increment integers for new tables.

```csharp
builder.Property(c => c.Id)
       .HasColumnName("id")
       .HasColumnType("uuid")
       .HasDefaultValueSql("gen_random_uuid()");
```

### 2.4 String Columns

- Always specify `HasMaxLength`; avoid unbounded `TEXT` for columns that have a known business constraint.

```csharp
builder.Property(c => c.PolicyNumber)
       .HasColumnName("policy_number")
       .HasColumnType("varchar(64)")
       .HasMaxLength(64)
       .IsRequired();
```

- Free-text fields without a known upper bound (e.g., notes, descriptions) use `text`:

```csharp
builder.Property(c => c.Notes)
       .HasColumnName("notes")
       .HasColumnType("text");
```

### 2.5 Monetary Amounts

- Store money as `NUMERIC(18,4)`, **never** `FLOAT` or `DOUBLE`.

```csharp
builder.Property(c => c.ClaimedAmount)
       .HasColumnName("claimed_amount")
       .HasColumnType("numeric(18,4)")
       .IsRequired();
```

### 2.6 Dates & Timestamps

- All timestamps MUST use `TIMESTAMPTZ` (timezone-aware). Never use `TIMESTAMP WITHOUT TIME ZONE`.

```csharp
builder.Property(c => c.CreatedAt)
       .HasColumnName("created_at")
       .HasColumnType("timestamptz")
       .HasDefaultValueSql("now()")
       .IsRequired();
```

### 2.7 JSONB Columns

- JSONB is permitted only for schema-volatile payloads (audit logs, external API snapshots, metadata bags). Core domain data MUST be in typed columns.
- Map JSONB columns using `.HasColumnType("jsonb")`:

```csharp
// Example: storing the raw insurer API response alongside a claim
builder.Property(c => c.InsurerResponseSnapshot)
       .HasColumnName("insurer_response_snapshot")
       .HasColumnType("jsonb");
```

- Deserialise JSONB through a value converter using `System.Text.Json`:

```csharp
builder.Property(c => c.AuditMetadata)
       .HasColumnName("audit_metadata")
       .HasColumnType("jsonb")
       .HasConversion(
           v => JsonSerializer.Serialize(v, JsonOptions.Default),
           v => JsonSerializer.Deserialize<AuditMetadata>(v, JsonOptions.Default)!);
```

### 2.8 Enumerations

- Map C# enums to `VARCHAR` using a string converter; never use EF Core's default integer storage.

```csharp
builder.Property(c => c.Status)
       .HasColumnName("status")
       .HasColumnType("varchar(64)")
       .HasConversion<string>()
       .IsRequired();
```

### 2.9 Relationships & Foreign Keys

- Always name foreign key constraints explicitly:

```csharp
builder.HasOne(c => c.Policy)
       .WithMany(p => p.Claims)
       .HasForeignKey(c => c.PolicyId)
       .HasConstraintName("fk_claims_policies")
       .OnDelete(DeleteBehavior.Restrict); // Prefer Restrict over Cascade
```

- Use `DeleteBehavior.Restrict` as the default. Cascade deletes require explicit architect approval and a comment in the configuration file.

### 2.10 Owned Entities & Value Objects

- Map DDD Value Objects as Owned Entities so they reside in the same table (no join overhead):

```csharp
// ClaimantAddress is a Value Object inside Claim
builder.OwnsOne(c => c.ClaimantAddress, addr =>
{
    addr.Property(a => a.Street).HasColumnName("claimant_street").HasMaxLength(256);
    addr.Property(a => a.City).HasColumnName("claimant_city").HasMaxLength(128);
    addr.Property(a => a.PostalCode).HasColumnName("claimant_postal_code").HasMaxLength(16);
    addr.Property(a => a.CountryCode).HasColumnName("claimant_country_code").HasColumnType("char(2)");
});
```

### 2.11 Soft Delete & Global Query Filters

- Every entity that supports soft delete MUST declare the filter in its configuration:

```csharp
builder.HasQueryFilter(c => !c.IsDeleted);
```

- To bypass the filter deliberately (e.g., admin restore endpoint), use `.IgnoreQueryFilters()` at the call site and add an explanatory comment.

### 2.12 Concurrency Tokens

- Use PostgreSQL's `xmin` system column for optimistic concurrency to avoid a dedicated `rowversion` column:

```csharp
builder.UseXminAsConcurrencyToken();
```

---

## 3. Performance

### 3.1 Indexing Strategy

- Index every foreign key column (PostgreSQL does **not** create these automatically):

```csharp
builder.HasIndex(c => c.PolicyId)
       .HasDatabaseName("ix_claims_policy_id");
```

- Index all columns used in `WHERE`, `ORDER BY`, or `JOIN` in hot query paths.
- Use **partial indexes** for filtered queries (e.g., only active/open claims):

```csharp
builder.HasIndex(c => c.CreatedAt)
       .HasDatabaseName("ix_claims_created_at_open")
       .HasFilter("is_deleted = false AND status IN ('Submitted','UnderReview')");
```

- Use **composite indexes** when multiple columns always appear together in predicates (order matters — put the most selective column first):

```csharp
builder.HasIndex(c => new { c.AssignedUserId, c.Status })
       .HasDatabaseName("ix_claims_assigned_user_id_status");
```

- Include non-key columns in covering indexes for frequent read-only queries:

```sql
-- In a raw migration if EF Core does not yet support INCLUDE
CREATE INDEX ix_claims_policy_id_covering
    ON claims (policy_id)
    INCLUDE (status, claimed_amount, created_at);
```

### 3.2 Query Design

- **Never** issue `SELECT *`. Use `.Select()` projections or dedicated read models (DTOs):

```csharp
// BAD
var claims = await _context.Claims.ToListAsync();

// GOOD — project only what the caller needs
var summaries = await _context.Claims
    .Where(c => c.AssignedUserId == userId)
    .Select(c => new ClaimSummaryDto(c.Id, c.PolicyNumber, c.Status, c.ClaimedAmount))
    .ToListAsync(cancellationToken);
```

- Use `.AsNoTracking()` for all read-only queries that do not feed into a Unit of Work write:

```csharp
var claim = await _context.Claims
    .AsNoTracking()
    .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
```

- Use `.AsSplitQuery()` for queries with multiple collection `.Include()` calls to avoid cartesian explosion:

```csharp
var claim = await _context.Claims
    .Include(c => c.Events)
    .Include(c => c.Documents)
    .AsSplitQuery()
    .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
```

- Paginate all list endpoints. Never return unbounded result sets:

```csharp
var page = await _context.Claims
    .Where(c => c.AssignedUserId == userId)
    .OrderByDescending(c => c.CreatedAt)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .AsNoTracking()
    .ToListAsync(cancellationToken);
```

### 3.3 Bulk Operations

- Use `ExecuteUpdateAsync` / `ExecuteDeleteAsync` (EF Core 7+) for bulk mutations instead of load-then-save:

```csharp
// Bulk soft-delete stale drafts without loading entities into memory
await _context.Claims
    .Where(c => c.Status == ClaimStatus.Draft && c.CreatedAt < cutoff)
    .ExecuteUpdateAsync(s => s
        .SetProperty(c => c.IsDeleted, true)
        .SetProperty(c => c.DeletedAt, DateTimeOffset.UtcNow)
        .SetProperty(c => c.DeletedBy, "system"),
        cancellationToken);
```

### 3.4 Connection & Transaction Management

- Use `IDbContextFactory<ClaimManagerDbContext>` for background jobs and parallel workers to avoid DbContext thread-safety issues.
- Keep transactions as short-lived as possible; release the connection before performing external I/O (e.g., sending emails, calling third-party APIs).
- Use `SERIALIZABLE` isolation only when explicitly required by a business rule; default to `READ COMMITTED`.

### 3.5 Migration Hygiene

- Every migration MUST be reviewed for lock-contention risk before deploying to production:

| Operation | Lock Risk | Mitigation |
|---|---|---|
| `ADD COLUMN NOT NULL DEFAULT` | High (full table rewrite pre-PG11) | Use PG 11+; add column as nullable, backfill, then add constraint |
| `CREATE INDEX` | High (blocks writes) | Use `CREATE INDEX CONCURRENTLY` in a raw SQL migration |
| `ALTER COLUMN TYPE` | High | Add new column, migrate data, drop old column |
| `ADD FOREIGN KEY` | Medium | Add `NOT VALID`, then `VALIDATE CONSTRAINT` separately |
| `ADD COLUMN NULL` | None | Safe |

- Never apply destructive migrations (DROP TABLE, DROP COLUMN) in the same deployment that removes the application code referencing that column. Use a two-phase deploy.
- All raw SQL in migrations MUST be idempotent (`IF NOT EXISTS`, `IF EXISTS`, `ON CONFLICT DO NOTHING`).

### 3.6 Connection Pooling

- Use **Npgsql** with PgBouncer in transaction-pooling mode for production deployments.
- Set the connection string `Pooling=true; Minimum Pool Size=5; Maximum Pool Size=100;` as a baseline; tune per environment.
- Enable `Enlist=false` when using a distributed transaction coordinator is not required (avoids MSDTC overhead).

---

## 4. Security

- Credentials MUST be stored in environment variables or a secrets manager (Azure Key Vault / AWS Secrets Manager). Never hard-code connection strings in `appsettings.json`.
- The application database user MUST have only `SELECT`, `INSERT`, `UPDATE`, `DELETE` on application tables. `CREATE`, `DROP`, and `ALTER` are granted only to the migrations user.
- Parameterise every query. Raw SQL via `FromSqlRaw` or `ExecuteSqlRaw` MUST use `{0}` positional parameters or `FromSqlInterpolated`; string concatenation is strictly forbidden.

```csharp
// BAD — SQL injection risk
var sql = $"SELECT * FROM claims WHERE policy_number = '{policyNumber}'";

// GOOD
var claims = await _context.Claims
    .FromSqlInterpolated($"SELECT * FROM claims WHERE policy_number = {policyNumber}")
    .ToListAsync(cancellationToken);
```

---

## 5. Testing

- Use **Testcontainers** (`Testcontainers.PostgreSql`) to spin up a real PostgreSQL instance in integration tests. Never mock the DbContext in persistence-layer tests.
- Apply migrations inside the test fixture, not `EnsureCreated()`, so migration scripts are exercised by the test suite:

```csharp
await _dbContext.Database.MigrateAsync();
```

- Reset database state between tests using transaction rollback or by truncating tables within a `BeforeEach` hook; never rely on test execution order.

---

## 6. Checklist — Pre-Merge

- [ ] Table and column names are `snake_case`.
- [ ] All constraints (PK, FK, IX, UQ, CK) are explicitly named following conventions.
- [ ] A dedicated `IEntityTypeConfiguration<T>` class exists for every new entity.
- [ ] Monetary columns use `numeric(18,4)`.
- [ ] All timestamps use `timestamptz`.
- [ ] Enums are stored as `varchar` with a `CHECK` constraint.
- [ ] Foreign key columns are indexed.
- [ ] Soft-delete filter (`HasQueryFilter`) is configured if the entity supports soft delete.
- [ ] Concurrency token (`UseXminAsConcurrencyToken`) is configured.
- [ ] No unbounded queries (pagination applied).
- [ ] Read-only queries use `AsNoTracking()`.
- [ ] Migration reviewed for lock-contention risk.
- [ ] No credentials in source code.
- [ ] Integration tests added or updated.
