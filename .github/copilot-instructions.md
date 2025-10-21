# Copilot Instructions for Simplz.Nutrition

These guidelines help AI coding agents work productively in this repository.

## 1. Architecture Overview
- ASP.NET Core minimal API (single `Program.cs`) hosting endpoints and wiring DI.
- Data layer via EF Core `NutritionContext` (SQLite). Domain models in `Models/` separate from CSV import DTOs in `Data/CSV/`.
- Vector search layer: Microsoft Semantic Kernel + SqliteVec. Foods stored both relationally (optionally) and as vector records keyed by `Food.Id` with a 384-d embedding (`Food.DescriptionEmbedding`).
- Import workflow (`ImportService`) reads USDA FoodData Central CSVs (stored under `Temp/`) using `CSVReader` and upserts vector records and/or EF entities.
- Ollama local models provide embedding + chat completion; configured through `OllamaOptions`.

## 2. Key Files & Responsibilities
- `Program.cs`: Composition root. Registers DbContext, options, vector store, embedding + chat clients, builds a `Kernel` with a text search plugin over the Food vector collection. Endpoints: `/` vector search demo; `/import/food` triggers import.
- `Data/NutritionContext.cs`: EF configuration. Note: `Food.Id` is `ValueGeneratedNever()`; caller must supply IDs from CSV.
- `Models/Food.cs`: Annotated for vector store (`[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]`). Embedding dimensions fixed at 384 and cosine distance.
- `Services/ImportService.cs`: Import routines. Only food + food category import implemented; nutrient-related imports commented out pending CSV DTOs.
- `Options/*.cs`: Bind config sections `Sqlite` and `Ollama`. `SqliteOptions.DatabasePath` builds connection string using `Temp` folder.

## 3. Conventions & Patterns
- Separation: `Models/` = persistence + vector annotated types; `Data/CSV/` = raw external schema for ingestion via CsvHelper attributes (`[Name("fdc_id")]`, etc.). Map manually in import service.
- Embeddings: Always request 384 dimensions to match attribute on `Food.DescriptionEmbedding`. Mismatch will cause search errors.
- Vector collection name hard-coded: `"Food"`. Ensure consistency when adding new collections.
- IDs from CSV become primary keys; do not rely on auto-increment for `Food`.
- Options bound with `IOptions<T>`; prefer injecting `IOptions<T>.Value` where needed.
- Use DI singletons for expensive services (embedding generator, chat client, CSV reader). `ImportService` is scoped per request.

## 4. Developer Workflows
- Build: `dotnet build Simplz.Nutrition.sln`.
- Run: `dotnet run --project Simplz.Nutrition.csproj` then hit `https://localhost:<port>/` or `/import/food`.
- Add migration (if schema changes): `dotnet ef migrations add <Name> --project Simplz.Nutrition.csproj --startup-project Simplz.Nutrition.csproj` then (optional) uncomment migrate block in `Program.cs` or run `dotnet ef database update`.
- Import data: Call `/import/food` after starting server and ensure CSVs exist under `Temp/...`. Extend by uncommenting methods and adding DTOs for nutrients.

## 5. Extending Functionality
- New vector-search entity: Create model with `[VectorStoreKey]` + `[VectorStoreVector(Dimensions=...)]`; register a collection in DI Kernel creation; add plugin via `VectorStoreTextSearch<T>.CreateWithSearch("<Name>SearchPlugin")`.
- Additional import: Add CSV DTO under `Data/CSV/`, implement import method mapping raw fields to domain model, persist via EF or vector store.
- Changing embedding model/dimensions: Update Ollama model + adjust `Food.DescriptionEmbedding` attribute and all generation calls to match dimensions.

## 6. Pitfalls & Gotchas
- SQLite connection string uses relative path `Temp/<DatabaseFile>`; ensure folder exists or change to absolute path to persist data.
- Embedding generation is async per record; large imports may need batching + concurrency control.
- Migrations currently define tables that the import might bypass (vector store vs EF). Keep relational and vector representations in sync if you start querying EF for Foods.
- Empty CSV DTO files (`Data/CSV/FoodNutrient.cs`, `Data/CSV/Nutrient.cs`) indicate planned but not implemented mappings.

## 7. Testing & Verification Tips
- Quick search test: After import, GET `/` should return top 5 foods for query `"chicken raw"` with scores.
- Validate embedding dimension: Inspect `Food.DescriptionEmbedding?.Length` or ensure no exception thrown during `collection.SearchAsync`.

## 8. Adding Endpoints
- Use minimal API pattern: `app.MapGet("/route", (Injected deps) => { ... });`. Inject `Kernel`, `VectorStore`, `ImportService`, or DbContext directly.

Please review and suggest any missing project-specific practices or workflows to refine further.
