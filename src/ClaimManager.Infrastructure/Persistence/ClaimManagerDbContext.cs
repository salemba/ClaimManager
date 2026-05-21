namespace ClaimManager.Infrastructure.Persistence;

using ClaimManager.Domain.Audit;
using ClaimManager.Domain.Claims;
using ClaimManager.Domain.ClaimantCommunication;
using ClaimManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ClaimManager.Application.Common.Interfaces;

public sealed class ClaimManagerDbContext(DbContextOptions<ClaimManagerDbContext> options)
    : IdentityDbContext<ClaimManagerUser, ClaimManagerRole, Guid>(options), IApplicationDbContext
{
    public DbSet<Claim> Claims => Set<Claim>();

    public DbSet<ClaimAudit> ClaimAudits => Set<ClaimAudit>();

    public DbSet<IntegrationHealthIncident> IntegrationHealthIncidents => Set<IntegrationHealthIncident>();

    public DbSet<ClaimNote> ClaimNotes => Set<ClaimNote>();

    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();

    public DbSet<ClaimCommunication> ClaimCommunications => Set<ClaimCommunication>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ClaimManagerUser>().ToTable("users");
        builder.Entity<ClaimManagerRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<Claim>(entity =>
        {
            entity.ToTable("claims");
            entity.HasKey(claim => claim.Id).HasName("pk_claims");

            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.ClaimNumber)
                .HasColumnName("claim_number")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(claim => claim.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(claim => claim.ClaimantName)
                .HasColumnName("claimant_name")
                .HasMaxLength(160)
                .IsRequired();
            entity.Property(claim => claim.ClaimantEmail)
                .HasColumnName("claimant_email")
                .HasMaxLength(320)
                .IsRequired();
            entity.Property(claim => claim.ClaimantPhone)
                .HasColumnName("claimant_phone")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(claim => claim.PolicyNumber)
                .HasColumnName("policy_number")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(claim => claim.LossDateUtc)
                .HasColumnName("loss_date_utc")
                .IsRequired();
            entity.Property(claim => claim.LossType)
                .HasColumnName("loss_type")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(claim => claim.LossDescription)
                .HasColumnName("loss_description")
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(claim => claim.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.Property(claim => claim.UpdatedAtUtc)
                .HasColumnName("updated_at_utc");
            entity.Property(claim => claim.CreatedByUserId)
                .HasColumnName("created_by_user_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(claim => claim.UpdatedByUserId)
                .HasColumnName("updated_by_user_id")
                .HasMaxLength(64);
            entity.Property(claim => claim.BlockerType)
                .HasColumnName("blocker_type")
                .HasMaxLength(64);
            entity.Property(claim => claim.BlockerReason)
                .HasColumnName("blocker_reason")
                .HasMaxLength(500);
            entity.Property(claim => claim.OwnedByUserId)
                .HasColumnName("owned_by_user_id")
                .HasMaxLength(64);
            entity.Property(claim => claim.NextExpectedAction)
                .HasColumnName("next_expected_action")
                .HasMaxLength(256);
            entity.Property(claim => claim.HasDataIntegrityWarning)
                .HasColumnName("has_data_integrity_warning")
                .HasDefaultValue(false)
                .IsRequired();
            entity.Property(claim => claim.DataIntegrityWarningMessage)
                .HasColumnName("data_integrity_warning_message")
                .HasMaxLength(500);
            entity.Property(claim => claim.ActiveDataIntegrityIssuesJson)
                .HasColumnName("active_data_integrity_issues_json")
                .HasColumnType("jsonb");
            entity.Property(claim => claim.LastReconciliationDetailsJson)
                .HasColumnName("last_reconciliation_details_json")
                .HasColumnType("jsonb");
            entity.Property(claim => claim.PolicyHolder)
                .HasColumnName("policy_holder")
                .HasMaxLength(160);
            entity.Property(claim => claim.CoverageType)
                .HasColumnName("coverage_type")
                .HasMaxLength(64);
            entity.Property(claim => claim.PolicyEffectiveDate)
                .HasColumnName("policy_effective_date");
            entity.Property(claim => claim.PolicyExpirationDate)
                .HasColumnName("policy_expiration_date");
            entity.Property(claim => claim.PolicySyncedAtUtc)
                .HasColumnName("policy_synced_at_utc");
            entity.Property(claim => claim.PaymentReference)
                .HasColumnName("payment_reference")
                .HasMaxLength(128);
            entity.Property(claim => claim.PaymentStatus)
                .HasColumnName("payment_status")
                .HasMaxLength(64);
            entity.Property(claim => claim.PaymentAmount)
                .HasColumnName("payment_amount")
                .HasPrecision(18, 4);
            entity.Property(claim => claim.PaymentCurrency)
                .HasColumnName("payment_currency")
                .HasMaxLength(8);

            entity.Property(claim => claim.PaymentSettledAt)
                .HasColumnName("payment_settled_at");

            entity.Property(claim => claim.PaymentSyncedAtUtc)
                .HasColumnName("payment_synced_at_utc");

            entity.Property(claim => claim.DocumentSyncedAtUtc)
                .HasColumnName("document_synced_at_utc");

            entity.Property(e => e.RowVersion)
                .IsRowVersion();

            entity.HasIndex(claim => claim.ClaimNumber)
                .IsUnique()
                .HasDatabaseName("ix_claims_claim_number");

            entity.HasMany(claim => claim.Notes)
                .WithOne()
                .HasForeignKey(note => note.ClaimId)
                .HasConstraintName("fk_claim_notes_claims_claim_id")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(claim => claim.Documents)
                .WithOne()
                .HasForeignKey(document => document.ClaimId)
                .HasConstraintName("fk_claim_documents_claims_claim_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClaimAudit>(entity =>
        {
            entity.ToTable("claim_audits");
            entity.HasKey(audit => audit.Id).HasName("pk_claim_audits");

            entity.Property(audit => audit.Id).HasColumnName("id");
            entity.Property(audit => audit.ClaimId).HasColumnName("claim_id").IsRequired();
            entity.Property(audit => audit.Action)
                .HasColumnName("action")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(audit => audit.Summary)
                .HasColumnName("summary")
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(audit => audit.PerformedAtUtc)
                .HasColumnName("performed_at_utc")
                .IsRequired();
            entity.Property(audit => audit.PerformedByUserId)
                .HasColumnName("performed_by_user_id")
                .HasMaxLength(64)
                .IsRequired();

            entity.HasIndex(audit => new { audit.ClaimId, audit.PerformedAtUtc })
                .HasDatabaseName("ix_claim_audits_claim_id_performed_at_utc");

            entity.HasOne<Claim>()
                .WithMany()
                .HasForeignKey(audit => audit.ClaimId)
                .HasConstraintName("fk_claim_audits_claims_claim_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntegrationHealthIncident>(entity =>
        {
            entity.ToTable("integration_health_incidents");
            entity.HasKey(incident => incident.Id).HasName("pk_integration_health_incidents");

            entity.Property(incident => incident.Id).HasColumnName("id");
            entity.Property(incident => incident.BoundaryName)
                .HasColumnName("boundary_name")
                .HasMaxLength(128)
                .IsRequired();
            entity.Property(incident => incident.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(incident => incident.Description)
                .HasColumnName("description")
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(incident => incident.StartedAtUtc)
                .HasColumnName("started_at_utc")
                .IsRequired();
            entity.Property(incident => incident.ResolvedAtUtc)
                .HasColumnName("resolved_at_utc");

            entity.HasIndex(incident => new { incident.BoundaryName, incident.StartedAtUtc })
                .HasDatabaseName("ix_integration_health_incidents_boundary_name_started_at_utc");
        });

        builder.Entity<ClaimNote>(entity =>
        {
            entity.ToTable("claim_notes");
            entity.HasKey(note => note.Id).HasName("pk_claim_notes");

            entity.Property(note => note.Id).HasColumnName("id");
            entity.Property(note => note.ClaimId).HasColumnName("claim_id").IsRequired();
            entity.Property(note => note.Content)
                .HasColumnName("content")
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(note => note.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.Property(note => note.CreatedByUserId)
                .HasColumnName("created_by_user_id")
                .HasMaxLength(64)
                .IsRequired();

            entity.HasIndex(note => new { note.ClaimId, note.CreatedAtUtc })
                .HasDatabaseName("ix_claim_notes_claim_id_created_at_utc");
        });

        builder.Entity<ClaimDocument>(entity =>
        {
            entity.ToTable("claim_documents");
            entity.HasKey(document => document.Id).HasName("pk_claim_documents");

            entity.Property(document => document.Id).HasColumnName("id");
            entity.Property(document => document.ClaimId).HasColumnName("claim_id").IsRequired();
            entity.Property(document => document.FileName)
                .HasColumnName("file_name")
                .HasMaxLength(256)
                .IsRequired();
            entity.Property(document => document.FileType)
                .HasColumnName("file_type")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(document => document.ContentType)
                .HasColumnName("content_type")
                .HasMaxLength(256);
            entity.Property(document => document.FileSizeBytes)
                .HasColumnName("file_size_bytes")
                .IsRequired();
            entity.Property(document => document.StorageIdentifier)
                .HasColumnName("storage_identifier")
                .HasMaxLength(512)
                .IsRequired();
            entity.Property(document => document.UploadedAtUtc)
                .HasColumnName("uploaded_at_utc")
                .IsRequired();
            entity.Property(document => document.UploadedByUserId)
                .HasColumnName("uploaded_by_user_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(document => document.Source)
                .HasColumnName("source")
                .HasMaxLength(32)
                .HasDefaultValue("uploaded")
                .IsRequired();

            entity.HasIndex(document => new { document.ClaimId, document.UploadedAtUtc })
                .HasDatabaseName("ix_claim_documents_claim_id_uploaded_at_utc");
            entity.HasIndex(document => document.StorageIdentifier)
                .IsUnique()
                .HasDatabaseName("ix_claim_documents_storage_identifier");
        });

        builder.Entity<ClaimCommunication>(entity =>
        {
            entity.ToTable("claim_communications");
            entity.HasKey(c => c.Id).HasName("pk_claim_communications");

            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.ClaimId).HasColumnName("claim_id").IsRequired();
            entity.Property(c => c.CommunicationType)
                .HasColumnName("communication_type")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(c => c.Channel)
                .HasColumnName("channel")
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(c => c.Recipient)
                .HasColumnName("recipient")
                .HasMaxLength(320)
                .IsRequired();
            entity.Property(c => c.Subject)
                .HasColumnName("subject")
                .HasMaxLength(256)
                .IsRequired();
            entity.Property(c => c.Body)
                .HasColumnName("body")
                .HasMaxLength(4000)
                .IsRequired();
            entity.Property(c => c.CorrelationId)
                .HasColumnName("correlation_id")
                .HasMaxLength(128);
            entity.Property(c => c.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasDefaultValue("pending")
                .IsRequired();
            entity.Property(c => c.AttemptCount)
                .HasColumnName("attempt_count")
                .HasDefaultValue(0)
                .IsRequired();
            entity.Property(c => c.LastAttemptAtUtc)
                .HasColumnName("last_attempt_at_utc");
            entity.Property(c => c.DeliveryId)
                .HasColumnName("delivery_id")
                .HasMaxLength(128);
            entity.Property(c => c.FailureReason)
                .HasColumnName("failure_reason")
                .HasMaxLength(500);
            entity.Property(c => c.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.Property(c => c.CreatedByUserId)
                .HasColumnName("created_by_user_id")
                .HasMaxLength(64)
                .IsRequired();

            entity.HasIndex(c => new { c.ClaimId, c.CreatedAtUtc })
                .HasDatabaseName("ix_claim_communications_claim_id_created_at_utc");

            entity.HasOne<Claim>()
                .WithMany()
                .HasForeignKey(c => c.ClaimId)
                .HasConstraintName("fk_claim_communications_claims_claim_id")
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClaimManagerRole>().HasData(ClaimManagerSeedData.Roles);
        builder.Entity<ClaimManagerUser>().HasData(ClaimManagerSeedData.Users);
        builder.Entity<IdentityUserRole<Guid>>().HasData(ClaimManagerSeedData.UserRoles);
        builder.Entity<Claim>().HasData(ClaimManagerSeedData.Claims);
    }
}