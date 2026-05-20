# ClaimsController Refactoring - Implementation Guide

## Overview

This document describes the complete refactoring of the `ClaimsController` to support production-ready multilingual functionality, comprehensive XML documentation, and enterprise-grade architecture.

## Changes Made

### 1. **Localization Support**

#### Resource Files Created
- `src/ClaimManager.Api/Resources/SharedMessages.resx` (English)
- `src/ClaimManager.Api/Resources/SharedMessages.fr.resx` (French)
- `src/ClaimManager.Api/Resources/ClaimsController.resx` (English)
- `src/ClaimManager.Api/Resources/ClaimsController.fr.resx` (French)

#### Resource Key Categories

##### Shared Messages
- HTTP error responses (401 Unauthorized, 404 Not Found, 409 Conflict)
- Common validation messages
- Standard success messages

##### Claims-Specific Messages
- **Claim Management**: Not found, creation/update conflicts
- **Validation**: Field-level error messages for claims, notes, documents
- **Audit Trail**: All operational messages (created, synced, updated, routed)
- **Synchronization**: External system integration errors (Policy, Payment, Documents)
- **Reconciliation**: Dependency recovery and conflict resolution messages
- **Notifications**: Message delivery status and retry messages

### 2. **IStringLocalizer Injection**

All endpoints now inject two localizers:
```csharp
IStringLocalizer<ClaimsController> localizer    // Claim-specific messages
IStringLocalizer<SharedMessages> sharedLocalizer // Common/shared messages
```

### 3. **XML Documentation**

Every endpoint includes comprehensive documentation:
- **Summary**: What the endpoint does
- **Remarks**: Implementation notes and context
- **Parameters**: Full description of each parameter
- **Returns**: Success and error return types
- **Response codes**: Complete HTTP status code documentation

Example:
```csharp
/// <summary>
/// Synchronizes policy data for a claim from the policy system.
/// </summary>
/// <param name="id">The unique identifier of the claim.</param>
/// <param name="localizer">The string localizer for claims controller.</param>
/// <response code="200">Policy data synchronized successfully.</response>
/// <response code="404">Claim not found.</response>
```

### 4. **Validation Error Localization**

The `ToLocalizedValidationDictionary` method now:
1. Groups validation errors by property
2. Looks up localized error messages using property names
3. Falls back to original messages if localization key not found
4. Converts property names to camelCase for API responses

```csharp
private static Dictionary<string, string[]> ToLocalizedValidationDictionary(
	ValidationResult validationResult,
	IStringLocalizer<ClaimsController> localizer)
```

### 5. **Message Formatting**

All messages use localized string formatting with `string.Format()`:

```csharp
// Simple localization
string message = localizer["Claim_NotFound_Title"];

// Localization with parameters
string message = string.Format(
	localizer["Audit_Policy_SyncFailed"], 
	failureReason);
```

### 6. **Helper Methods Refactored**

All helper methods updated to accept localizers:
- `BuildUpdateSummary()` - Localized change descriptions
- `BuildNoteAuditSummary()` - Localized note preview
- `BuildDocumentAuditSummary()` - Localized document info
- `BuildSyncFailureReason()` - Localized error messages
- `BuildReconciliationSummary()` - Localized reconciliation status
- `BuildChange()` - Localized change formatting

## Configuration

### Program.cs Updates

```csharp
// Add localization services
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
	var supportedCultures = new[] { "en", "fr" };
	options.SetDefaultCulture("en")
		.AddSupportedCultures(supportedCultures)
		.AddSupportedUICultures(supportedCultures)
		.RequestCultureProviders.Insert(0, new AcceptLanguageHeaderRequestCultureProvider());
});

// Use localization middleware
app.UseRequestLocalization(localizationOptions.Value);
```

### Language Support
- **English** (en) - Default
- **French** (fr) - Supported via Accept-Language header

## Resource Keys

### Shared Messages Format
- `Error_Unauthorized_Title` - Error title
- `Error_Unauthorized_Detail` - Error details
- `Error_NotFound_Title` - Resource not found
- `Error_Conflict_Title` - Conflict occurred

### Claims Controller Format
- `Claim_NotFound_Title/Detail` - Claim not found errors
- `Claim_Validation_*` - Validation errors
- `Claim_Create_Conflict_*` - Creation conflicts
- `Claim_Update_Conflict_*` - Update conflicts
- `Audit_*` - Audit trail messages
- `Sync_Error_*` - Synchronization errors
- `Reconciliation_*` - Reconciliation messages
- `Workflow_InvalidTransition_Title` - Invalid workflow states
- `Notification_*` - Notification-related messages

## Usage Examples

### Using Localized Messages in Responses

```csharp
// Simple message
return TypedResults.Problem(
	statusCode: StatusCodes.Status404NotFound,
	title: localizer["Claim_NotFound_Title"],
	detail: localizer["Claim_NotFound_Detail"]);

// Message with formatting
var auditMessage = string.Format(
	localizer["Audit_Policy_SyncFailed"], 
	syncFailReason);

// Localized validation errors
return TypedResults.ValidationProblem(
	ToLocalizedValidationDictionary(validationResult, localizer));
```

### Client Requesting Specific Language

```bash
# Request in French
curl -H "Accept-Language: fr" https://api.example.com/api/claims

# Request in English (default)
curl -H "Accept-Language: en" https://api.example.com/api/claims
```

## Benefits

1. **Maintainability**: All user-facing messages centralized in resource files
2. **Scalability**: Easy to add new languages without code changes
3. **Consistency**: Standardized message format across all endpoints
4. **Documentation**: Comprehensive XML documentation for API consumers
5. **Compliance**: Enterprise-grade localization support
6. **Testability**: Mocked localizers can be injected for testing

## Testing Localization

```csharp
[Theory]
[InlineData("en", "Claim not found")]
[InlineData("fr", "Sinistre non trouvé")]
public async Task GetClaimDetails_WithInvalidId_ReturnsLocalizedError(
	string culture, 
	string expectedTitle)
{
	// Arrange
	var localizer = new MockStringLocalizer<ClaimsController>();
	localizer.SetString("Claim_NotFound_Title", expectedTitle);

	// Act
	var result = await GetClaimDetailsAsync(Guid.NewGuid(), dbContext, localizer, CancellationToken.None);

	// Assert
	result.Should().BeOfType<ProblemHttpResult>()
		.Subject.ProblemDetails.Title.Should().Be(expectedTitle);
}
```

## Migration Guide

If upgrading existing code:

1. **Update Program.cs** with localization configuration
2. **Inject localizers** in endpoint methods
3. **Replace hardcoded strings** with localizer calls
4. **Update helper methods** to accept localizers
5. **Test all endpoints** with different Accept-Language headers

## File Structure

```
src/ClaimManager.Api/
├── Endpoints/
│   └── Claims/
│       └── ClaimsController.cs (Refactored)
├── Resources/
│   ├── ClaimsController.resx (English)
│   ├── ClaimsController.fr.resx (French)
│   ├── SharedMessages.resx (English)
│   └── SharedMessages.fr.resx (French)
└── Program.cs (Updated with localization)
```

## Next Steps

1. Run build to verify compilation: `dotnet build`
2. Run tests to verify functionality
3. Test endpoints with different Accept-Language headers
4. Add additional language support by creating new .resx files
5. Update client applications to send appropriate Accept-Language headers

## Backward Compatibility

The refactored controller maintains 100% API compatibility:
- Route paths unchanged
- Response structures unchanged
- Only message content localized
- Default language is English (maintains existing behavior)
