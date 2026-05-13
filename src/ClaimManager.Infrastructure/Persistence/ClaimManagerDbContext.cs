namespace ClaimManager.Infrastructure.Persistence;

using ClaimManager.Domain.Audit;
using ClaimManager.Domain.Claims;
using ClaimManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public sealed class ClaimManagerDbContext(DbContextOptions<ClaimManagerDbContext> options)
    : IdentityDbContext<ClaimManagerUser, ClaimManagerRole, Guid>(options)
{
    public DbSet<Claim> Claims => Set<Claim>();

    public DbSet<ClaimAudit> ClaimAudits => Set<ClaimAudit>();

    public DbSet<ClaimNote> ClaimNotes => Set<ClaimNote>();

    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();

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

            entity.HasIndex(document => new { document.ClaimId, document.UploadedAtUtc })
                .HasDatabaseName("ix_claim_documents_claim_id_uploaded_at_utc");
            entity.HasIndex(document => document.StorageIdentifier)
                .IsUnique()
                .HasDatabaseName("ix_claim_documents_storage_identifier");
        });

        builder.Entity<ClaimManagerRole>().HasData(ClaimManagerSeedData.Roles);
        builder.Entity<ClaimManagerUser>().HasData(ClaimManagerSeedData.Users);
        builder.Entity<IdentityUserRole<Guid>>().HasData(ClaimManagerSeedData.UserRoles);
        builder.Entity<Claim>().HasData(ClaimManagerSeedData.Claims);
    }
}