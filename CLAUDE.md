# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All `dotnet` commands must be run from the `./src` directory.

```bash
dotnet restore
dotnet build --no-restore -warnaserror
dotnet format --verify-no-changes             # check code style (CI enforces this)
dotnet format                                 # auto-fix code style
dotnet test --no-build --verbosity normal     # run integration tests (requires Docker for Testcontainers)
dotnet tool restore && dotnet stryker --mutation-level Complete --break-at 86
dotnet pack --configuration Release -p:Version=<version> --output .
```

CI additionally passes `-p:AssemblyVersion` (pinned to the major) and `-p:FileVersion`; see `.github/workflows/publish-nuget.yml`. Stryker leaves a mutated assembly in `bin/`, so rebuild before running `dotnet test --no-build` after it.

## Architecture

This is the **updating (write)** part of the PostgreSQL storage implementation for the Pure relational schema ecosystem. It has no public abstractions of its own — it provides one public decorator that implements `IPostgreSqlStoredSchemaDataSet` (from `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions`). Creating tables, reading rows, inserting rows and deleting rows live in the sibling packages `Pure.RelationalSchema.Storage.PostgreSQL.Creation`, `…Querying`, `…Insertion` and `…Deletion`. The design deliberately mirrors Insertion and Deletion so the write packages stay symmetric.

**Dependencies:** `Pure.RelationalSchema.Storage.PostgreSQL.Abstractions` is the only package from this family that the library references. Tests additionally reference the published Creation, Insertion and Querying packages to prove that rows written by Insertion are updated and read back through Querying, and that constraints created by Creation (unique index, foreign key) reject an update. There is no library dependency on Querying, Creation, Insertion or Deletion.

**Key relationships:**

- `PostgreSqlStoredSchemaDataSetWithUpdatedRows` (public) wraps an `IPostgreSqlStoredSchemaDataSet` and updates grouped by `ITable`. Each inner `IGrouping<IEnumerable<KeyValuePair<IColumn, ICell>>, KeyValuePair<IColumn, ICell>>` is one update: its `Key` is the cells identifying the rows (the `WHERE`), its elements are the cells to set (the `SET`). Like the Insertion and Deletion decorators, it exposes **only the tables present in the supplied groupings** (an `OrderedDictionary` keyed by `TableHash`), and passes `Connection` and `Schema` through from the wrapped dataset. This signature was decided by the owner: a set of cells is `IEnumerable<KeyValuePair<IColumn, ICell>>` (so `IRow.Cells` fits), with no tuple types and no new public pair interface.
- `PostgreSqlStoredTableDataSetWithUpdatedRows` (internal) is the per-table decorator. On first access of any member it lazily builds one `UpdateStatement` per update in the supplied order, skipping updates with no cells to set (`SET` with nothing is invalid SQL), joins them with spaces and executes them as **one command**; nothing is executed when no statement is left. Then it delegates every member to the wrapped `IStoredTableDataSet`. Npgsql sends the statements of one command in an implicit transaction, so one failing statement rolls back the whole table (tests rely on this). The `throw new ArgumentException("Value was not updated.")` arms are unreachable and mirror Insertion/Deletion; they account for the uncovered branches and NoCoverage mutants.
- `UpdateStatement` (internal) builds `UPDATE "<schema>"."<table>" SET "c" = 'v', … WHERE "k" = 'v' AND …;`. The `WHERE` is left out for an empty key through `StringChoice` over `EmptyCondition` (an empty key updates every row, by design). Cells are rendered in the supplied order and as supplied: duplicates are not removed, columns are not checked against the table. PostgreSQL decides (`42601` multiple assignments, `42703` undefined column, `23505`, `23503`).
- `CellEquality` (internal) renders one cell as `"<column hash>" = '<value>'`; the same text serves as a `SET` assignment and a `WHERE` comparison.
- `SingleQuoteEscapedString` (internal) doubles embedded `'`, as in Deletion. Values are plain (not `E''`) literals, so backslashes stay literal with `standard_conforming_strings` on; this matches what Insertion stores with its `E''` escaping (tested).
- All SQL is composed as an `IString` value using `Pure.Primitives.String.Operations` combinators. Do not use string interpolation in library code.
- The caller owns the `IDbConnection`. It is shared with every other dataset using it, so no type here may dispose it (the `IDbCommand` is disposed).

**Naming convention:** the tables must already exist with hash-based names. Schema, table and column identifiers are the first 63 characters (PostgreSQL's identifier limit) of `HexString` over `SchemaHash` / `TableHash` / `ColumnHash` from `Pure.RelationalSchema.HashCodes`. This convention must match the Creation, Querying, Insertion and Deletion packages, since it is the only contract between them and this one. `TrimmedHash` is deliberately duplicated in each repo rather than shared; it expects input of at least 63 characters (a 64-character SHA-256 hex string in practice).

**Testing:** Integration tests use `Testcontainers.PostgreSql` to spin up a real PostgreSQL instance per test class instance (`IAsyncLifetime`). `DatabaseFixture` creates the hash-named schema and two tables with raw SQL — `Users` (`Id`, `Name`, `Age`, `Active`, unique index on `Id`) and `Orders` (`OrderId`, `Note`) — seeds rows with `DatabaseFixture.Seed` (raw `INSERT` of text values, quotes escaped) and reads them back with `DatabaseFixture.Stored`, which returns every row as `'|'`-joined text, sorted. The wrapped dataset is the in-memory `Fakes/FakeStoredSchemaDataSet`, keyed by `TableHash` because `Table.GetHashCode()` throws. The decorator is lazy, so touch a member first to trigger the update.

- `Fakes/Update` and `Fakes/TableUpdates` are the two grouping levels of the constructor argument; `Fakes/TableRows` groups rows for the Insertion package.
- `PostgreSqlStoredSchemaDataSetWithUpdatedRowsOverQueryingTests` creates its own schema with Creation (unique index and foreign key), inserts with Insertion and wraps the real Querying dataset, mirroring the README usage.
- `UpdateStatementTests` asserts the exact SQL text (mutation-testing targets) and uses 64-character schema/table names because `TrimmedHash` takes a fixed 63-character prefix.
- Non-generic enumeration is exercised through `Fakes/NonGenericItems`; `Cast<T>()` short-circuits when the source already implements `IEnumerable<T>` and would not call the non-generic `GetEnumerator`.
- On stored datasets use `.AsEnumerable()` with `Assert.Empty` / `Assert.Single`, because the bare overloads are ambiguous between `IEnumerable` and `IAsyncEnumerable`.

CI runs mutation testing via `dotnet-stryker` at `Complete` level with an 86% break threshold and requires >=99% line coverage (warning at 99%).

**CI style check:** the `Code style check by csharpier` step in `.github/workflows/build-and-test.yml` is commented out on purpose. csharpier wants `) { }` on one line for chained constructors with an empty body, while `dotnet format` (IDE0055 with `csharp_new_line_before_open_brace = all`) wants `{ }` on its own line, so both cannot pass on `PostgreSqlStoredSchemaDataSetWithUpdatedRows.cs`. `dotnet format` is the authoritative check.

**Multi-targeting:** net8.0, net9.0, net10.0. `IsAotCompatible` is `true` and the build must stay free of IL2026/IL3050 warnings. Do not use `AsQueryable()` in library code (annotated as trim/AOT-unsafe).

**Package validation:** `EnablePackageValidation = true`. There is no `PackageValidationBaselineVersion` until the first release; set it to the last published version after each release so breaking API changes fail the build.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag name becomes the `PackageVersion`. Packages are published to both GitHub Packages and NuGet.org.

## Code Style

Enforced via `.editorconfig` and `dotnet format --verify-no-changes` in CI:

- No `var` — always use explicit types
- No expression-bodied methods or constructors — properties, indexers, accessors, and lambdas may be expression-bodied
- File-scoped namespace declarations
- Private fields prefixed with `_` (underscore camelCase)
- No non-private instance fields
- `using` directives outside the namespace, System directives sorted first
- Max line length: 90 characters
- Braces on new lines (`csharp_new_line_before_open_brace = all`)
- No implicit object creation (`new()` without type) when the type is apparent

## Commit Messages

Do not mention Claude or AI assistance in commit messages.
