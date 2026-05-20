# Resource Keys Reference Guide

## SharedMessages.resx Keys

### HTTP Error Messages

| Key | English | French |
|-----|---------|--------|
| `Error_Unauthorized_Title` | Authentication required | Authentification requise |
| `Error_Unauthorized_Detail` | The current request does not have a valid authenticated user. | La requête actuelle n'a pas d'utilisateur authentifié valide. |
| `Error_NotFound_Title` | Resource not found | Ressource non trouvée |
| `Error_NotFound_Detail` | The requested resource could not be found. | La ressource demandée n'a pas pu être trouvée. |
| `Error_Conflict_Title` | Conflict | Conflit |
| `Error_InvalidOperation_Title` | Invalid operation | Opération invalide |

### Common Messages

| Key | English | French |
|-----|---------|--------|
| `Message_Success` | Operation completed successfully. | L'opération a été complétée avec succès. |
| `Validation_Error_Title` | Validation failed | Validation échouée |

---

## ClaimsController.resx Keys

### Claim Not Found

| Key | English | French |
|-----|---------|--------|
| `Claim_NotFound_Title` | Claim not found | Sinistre non trouvé |
| `Claim_NotFound_Detail` | The requested claim could not be found. | Le sinistre demandé n'a pas pu être trouvé. |

### Claim Validation Errors

| Key | English | French |
|-----|---------|--------|
| `Claim_Validation_Email_Required` | Email is required. | L'adresse électronique est requise. |
| `Claim_Validation_ClaimantName_Required` | Claimant name is required. | Le nom du sinistré est requis. |
| `Claim_Validation_ClaimantEmail_Required` | Claimant email is required. | L'adresse électronique du sinistré est requise. |
| `Claim_Validation_ClaimantPhone_Required` | Claimant phone is required. | Le numéro de téléphone du sinistré est requis. |
| `Claim_Validation_PolicyNumber_Required` | Policy number is required. | Le numéro de police est requis. |
| `Claim_Validation_LossDateUtc_Required` | Loss date is required. | La date de sinistre est requise. |
| `Claim_Validation_LossType_Required` | Loss type is required. | Le type de sinistre est requis. |
| `Claim_Validation_LossDescription_Required` | Loss description is required. | La description du sinistre est requise. |
| `Claim_Validation_RowVersion_Required` | Row version is required for updates. | La version de ligne est requise pour les mises à jour. |

### Claim Creation

| Key | English | French |
|-----|---------|--------|
| `Claim_Create_Conflict_Title` | Claim creation conflict | Conflit de création de sinistre |
| `Claim_Create_Conflict_Detail` | The claim number could not be reserved. Please retry the request. | Le numéro de sinistre n'a pas pu être réservé. Veuillez réessayer la demande. |

### Claim Update

| Key | English | French |
|-----|---------|--------|
| `Claim_Update_Conflict_Title` | Concurrency conflict | Conflit de concurrence |
| `Claim_Update_Conflict_Detail` | The claim has been modified by another user. Please reload the claim and try again. | Le sinistre a été modifié par un autre utilisateur. Veuillez recharger le sinistre et réessayer. |

### Audit Messages

| Key | Format | English | French |
|-----|--------|---------|--------|
| `Audit_Claim_Created` | None | Claim file created with claimant, claim, and loss information. | Dossier de sinistre créé avec les informations du sinistré, du sinistre et de la perte. |
| `Audit_Policy_Synced` | None | Policy data synchronized successfully. | Les données de police ont été synchronisées avec succès. |
| `Audit_Policy_SyncFailed` | {0} = reason | Policy data synchronization failed: {0} | La synchronisation des données de police a échoué: {0} |
| `Audit_Payment_Synced` | None | Payment data synchronized successfully. | Les données de paiement ont été synchronisées avec succès. |
| `Audit_Payment_SyncFailed` | {0} = reason | Payment data synchronization failed: {0} | La synchronisation des données de paiement a échoué: {0} |
| `Audit_Documents_Synced` | None | Document data synchronized successfully. | Les données de documents ont été synchronisées avec succès. |
| `Audit_Documents_SyncFailed` | {0} = reason | Document data synchronization failed: {0} | La synchronisation des données de documents a échoué: {0} |
| `Audit_NoteAdded` | {0} = preview | Claim note added: '{0}'. | Note de sinistre ajoutée: '{0}'. |
| `Audit_DocumentUploaded` | {0} = filename, {1} = type | Document uploaded: {0} ({1}). | Document téléchargé: {0} ({1}). |
| `Audit_WorkflowAdvanced` | None | Claim workflow advanced. | Flux de sinistre avancé. |
| `Audit_RoutedForApproval` | {0} = rationale | Claim routed for payment approval. Rationale: {0} | Sinistre acheminé pour approbation de paiement. Raison: {0} |
| `Audit_NotificationSent` | {0} = type, {1} = channel, {2} = recipient, {3} = delivery_id | Outbound {0} notification sent via {1} to {2}. Delivery ID: {3}. | Notification sortante {0} envoyée via {1} à {2}. ID de livraison: {3}. |
| `Audit_NotificationFailed` | {0} = type, {1} = channel, {2} = recipient, {3} = reason | Outbound {0} notification failed via {1} to {2}. Reason: {3} | Notification sortante {0} échouée via {1} à {2}. Raison: {3} |
| `Audit_NotificationRetried` | {0} = type, {1} = status, {2} = channel, {3} = recipient, {4} = delivery_id | Retry of {0} notification {1} via {2} to {3}. Delivery ID: {4}. | Nouvelle tentative de notification {0} {1} via {2} à {3}. ID de livraison: {4}. |

### Synchronization Errors

| Key | Format | English | French |
|-----|--------|---------|--------|
| `Sync_Error_PolicySystemUnreachable` | {0} = error message | Policy system was unreachable or returned an unexpected error: {0} | Le système de police était inaccessible ou a renvoyé une erreur inattendue: {0} |
| `Sync_Error_PaymentSystemUnreachable` | {0} = error message | Payment system was unreachable or returned an unexpected error: {0} | Le système de paiement était inaccessible ou a renvoyé une erreur inattendue: {0} |
| `Sync_Error_DocumentRepositoryUnreachable` | {0} = error message | Document repository was unreachable or returned an unexpected error: {0} | Le référentiel de documents était inaccessible ou a renvoyé une erreur inattendue: {0} |
| `Sync_Error_PolicyNotFound` | None | Policy not found for the recorded policy number. | Police non trouvée pour le numéro de police enregistré. |

### Reconciliation Messages

| Key | Format | English | French |
|-----|--------|---------|--------|
| `Reconciliation_Retried` | {0} = dependencies | Reconciliation retried {0}. | Réconciliation retentée {0}. |
| `Reconciliation_Recovered` | {0} = dependencies | Recovered: {0}. | Récupérés: {0}. |
| `Reconciliation_NoRecovered` | None | No previously unresolved dependencies were recovered during this attempt. | Aucune dépendance non résolue précédemment n'a été récupérée lors de cette tentative. |
| `Reconciliation_AllResolved` | None | All claim integration dependencies are now aligned. | Toutes les dépendances d'intégration des sinistres sont maintenant alignées. |
| `Reconciliation_StillUnresolved` | {0} = dependencies | Still unresolved: {0}. | Toujours non résolus: {0}. |

### Workflow

| Key | English | French |
|-----|---------|--------|
| `Workflow_InvalidTransition_Title` | Invalid workflow transition | Transition de flux invalide |

### Notification

| Key | Format | English | French |
|-----|--------|---------|--------|
| `Notification_NotFound_Title` | None | Notification not found | Notification non trouvée |
| `Notification_NotFound_Detail` | None | The requested notification could not be found for this claim. | La notification demandée n'a pas pu être trouvée pour ce sinistre. |
| `Notification_RetryNotAllowed_Title` | None | Retry not allowed | Nouvelle tentative non autorisée |
| `Notification_RetryNotAllowed_Detail` | {0} = status | Cannot retry a notification in '{0}' state. Only failed notifications can be retried. | Impossible de réessayer une notification dans l'état '{0}'. Seules les notifications échouées peuvent être retentées. |

### Document Upload

| Key | Format | English | French |
|-----|--------|---------|--------|
| `Document_Upload_Note_MaxLength` | {0} = length | Preview truncated at {0} characters | Aperçu tronqué à {0} caractères |

### Claim Update Summary

| Key | Format | English | French |
|-----|--------|---------|--------|
| `Claim_UpdateSummary_NoChanges` | None | Claim file reviewed with no material changes. | Dossier de sinistre examiné sans changements matériels. |
| `Claim_UpdateSummary_Changed` | {0} = field, {1} = old_value, {2} = new_value | {0} updated from '{1}' to '{2}'. | {0} mis à jour de '{1}' à '{2}'. |

---

## Key Naming Conventions

### Pattern: `Category_Subcategory_Item`

- **Category**: Main feature area (Claim, Audit, Sync, Validation, etc.)
- **Subcategory**: Specific operation or error type (NotFound, SyncFailed, etc.)
- **Item**: Specific message type (Title, Detail, Reason, etc.)

### Examples
- `Claim_NotFound_Title` → Claim feature, NotFound error, Title message
- `Audit_Policy_SyncFailed` → Audit feature, Policy operation, SyncFailed message
- `Sync_Error_PolicySystemUnreachable` → Sync feature, Error category, specific system

### Format Parameters

All format parameters use `{0}`, `{1}`, etc. in resource files and are applied via `string.Format()`:

```csharp
string.Format(localizer["Audit_Policy_SyncFailed"], failureReason)
```

---

## Adding New Keys

1. Add English key to `ClaimsController.resx`
2. Add French translation to `ClaimsController.fr.resx`
3. Use consistent naming convention
4. Document in this reference guide
5. Update code to use new key with appropriate localization

## Validation Key Mapping

Validation error keys match command/request property names:

```csharp
// Property name
public string ClaimantName { get; set; }

// Validation key
Claim_Validation_ClaimantName_Required

// Used in code
LocalizeValidationError("ClaimantName", "required error", localizer)
```
