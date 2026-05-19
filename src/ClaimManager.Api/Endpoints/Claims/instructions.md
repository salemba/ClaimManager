Refactor summary and instructions

What I changed
- Added localization resources at src/ClaimManager.Api/Resources/Resource.fx.resx.
- Injected `IStringLocalizer<Resource>` into ClaimsController endpoints and helper methods.
- Replaced hard-coded status titles/details and sync error messages with localized strings from `Resource.fx.resx`.
- Updated `BuildSyncFailureReason` to accept localized templates and format them.
- Started adding XML documentation for endpoints (several methods updated). Ensure all remaining methods are similarly documented if needed.

How to wire up localization
1. In `Program.cs` register the resource type and localization services:

   builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
   builder.Services.AddSingleton(typeof(Resource));

   // Ensure controllers and endpoints can resolve IStringLocalizer<Resource>
   builder.Services.AddControllers();

2. Verify the resource file `Resource.fx.resx` is marked as an Embedded Resource in the .csproj or set <EmbeddedResource> automatically by SDK projects.

3. Optional: add culture providers and supported cultures if you want runtime culture selection.

Notes and next steps
- I injected `IStringLocalizer<Resource>` into many methods; ensure the DI container can provide it for endpoints registered as delegates (minimal APIs). If using minimal APIs you may need to bind parameters via `WithMetadata(new EndpointNameMetadata(...))` or use factory patterns.
- I adjusted helper methods to pass `localizer` along. If you prefer a single `IStringLocalizer` instance stored in a static field or via a typed class, consider refactoring further.
- Continue adding XML doc comments for remaining methods if full coverage is required.

If you want, I can:
- Finish XML documentation for every endpoint and helper method.
- Convert `Resource.fx.resx` into strongly-typed `Resource` class via `dotnet resgen` or use `IStringLocalizer<T>` access patterns.
- Create localized variants (Resource.fx.fr.resx) and wiring for culture selection.
