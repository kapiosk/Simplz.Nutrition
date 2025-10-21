# Simplz.Nutrition

Minimal ASP.NET Core API demonstrating hybrid nutritional data ingestion and semantic vector search over USDA FoodData Central CSVs.

## Key Features
- Import USDA food category + food records from CSV under `Temp/` using `CsvHelper`.
- Store foods both in SQLite relational tables (optional) and a SqliteVec vector collection via Semantic Kernel.
- Generate 384-d embeddings (MiniLM via Ollama) for food descriptions; cosine similarity search endpoint.
- Extensible pattern for adding nutrients and other entities.

## Architecture Overview
- `Program.cs`: Composition root. Registers EF Core `NutritionContext`, options (`SqliteOptions`, `OllamaOptions`), vector store, Ollama embedding + chat clients, builds a Semantic Kernel with a Food search plugin.
- `Models/`: Domain + vector annotated records (`Food`, `FoodCategory`, `Nutrient`, `FoodNutrient`). Embedding vector declared on `Food.DescriptionEmbedding`.
- `Data/CSV/`: Raw DTOs mirroring USDA CSV column names via `CsvHelper` `[Name]` attributes.
- `Services/ImportService.cs`: Orchestrates reading CSV and upserting vector records (`Food`) or persisting categories.
- `Options/`: Configuration records with strongly-typed binding from `appsettings*.json`.

## Prerequisites
- .NET 10 SDK installed.
- Local Ollama instance running with models referenced in `OllamaOptions` (defaults: `all-minilm`, `llama3`). Install models:
  ```powershell
  ollama pull all-minilm
  ollama pull llama3
  ```
- USDA FoodData Central CSVs placed under `Temp/` (already structured in repo). Ensure `food.csv` & `food_category.csv` exist within a dated subfolder.

## Getting Started
```powershell
# Restore & build
dotnet build Simplz.Nutrition.sln

# (Optional) Add a migration if you changed entities
# dotnet ef migrations add <Name> --project Simplz.Nutrition.csproj --startup-project Simplz.Nutrition.csproj
# dotnet ef database update

# Run the API
dotnet run --project Simplz.Nutrition.csproj
```
Visit:
- `GET /`: Performs a vector similarity search for hard-coded query `"chicken raw"` returning top 5 matches.
- `GET /import/food`: Imports food records (vector only) from legacy `food.csv` and generates embeddings.

## Configuration
`appsettings.json` sections:
```jsonc
{
  "Sqlite": {
    "DatabaseFile": ":memory:",
    "EmbeddingDimensions": 384
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "all-minilm",
    "ChatModel": "llama3",
    "RequestTimeoutSeconds": 60
  }
}
```
Adjust `Sqlite.DatabaseFile` to a persistent filename (e.g. `nutrition.db`) for data durability. Connection string is built as `Data Source=Temp/<DatabaseFile>`.

## Import Workflow Details
1. `ImportService.ImportFoodDataAsync` reads `Temp/FoodData_Central_sr_legacy_food_csv_2018-04/food.csv` into `Data.CSV.Food` DTOs.
2. For each record, generates a 384-d embedding for `Description` via Ollama and upserts into vector collection `"Food"` keyed by `Food.Id`.
3. `ImportFoodCategoryDataAsync` persists categories to EF; nutrients & food nutrients are scaffolded (DTO files empty, methods commented out).

### Extending Imports
- Add DTO under `Data/CSV/` with `[Name]` attributes matching new CSV file.
- Implement an import method mapping DTO -> domain entity.
- For vector search: annotate model with `[VectorStoreVector(Dimensions=...)]`, retrieve collection via `VectorStore.GetCollection<TKey, TEntity>(name)` and ensure `EnsureCollectionExistsAsync` is invoked.

## Vector Search Usage
Food search plugin is created with `VectorStoreTextSearch<Food>`; you can adapt by exposing an endpoint taking a query parameter:
```csharp
app.MapGet("/search", async (string q, VectorStore store) => {
    var col = store.GetCollection<long, Food>("Food");
    var results = new List<object>();
    await foreach (var r in col.SearchAsync(q, 5))
        results.Add(new { r.Record.Id, r.Record.Name, r.Score });
    return Results.Ok(results);
});
```
Ensure queries align with what embeddings encode (currently only description text).

## Migrations & Data Persistence
- `Food.Id` is `ValueGeneratedNever()`; IDs must come from CSV.
- By default `:memory:` uses an in-memory SQLite DB each run. Switch to file-based to retain categories/nutrients.
- Vector collection persists separately through SqliteVec using same DB connection string.

## Roadmap Ideas
- Implement nutrient + food nutrient import (fill DTOs & uncomment methods).
- Add dynamic search endpoint with user-provided query.
- Batch embedding generation to improve performance.
- Add minimal test project for import & search correctness.

## Troubleshooting
- Empty search results: Ensure `/import/food` ran and embeddings generated (no exceptions). Check that Ollama is running.
- Dimension mismatch errors: Verify `EmbeddingDimensions` and `[VectorStoreVector(Dimensions: 384)]` are consistent with generator call (384).
- SQLite file not created: Confirm `Temp/` folder exists; adjust path or use absolute path in `SqliteOptions`.

## License
See `LICENSE.md`.

---
Feel free to propose enhancements or request additional docs.
