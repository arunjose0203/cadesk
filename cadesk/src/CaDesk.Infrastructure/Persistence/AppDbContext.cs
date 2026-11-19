using System.Linq.Expressions;
using System.Reflection;
using CaDesk.Application.Abstractions;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Compliance;
using CaDesk.Domain.Documents;
using CaDesk.Domain.Tasks;
using CaDesk.Domain.Notifications;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Common;
using CaDesk.Domain.Configuration;
using CaDesk.Domain.Firms;
using CaDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CaDesk.Infrastructure.Persistence;

/// <summary>
/// Lock 1 of 2 on tenant data: every ITenantOwned set is filtered to the current firm,
/// and new rows are stamped with it. Lock 2 is PostgreSQL row-level security
/// (see TenantConnectionInterceptor and 0001_initial.sql).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : DbContext(options), IAppDbContext
{
    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserMfa> UserMfa => Set<UserMfa>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<ClientGroup> ClientGroups => Set<ClientGroup>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<ClientNote> ClientNotes => Set<ClientNote>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<TaxIdType> TaxIdTypes => Set<TaxIdType>();
    public DbSet<ComplianceType> ComplianceTypes => Set<ComplianceType>();
    public DbSet<ComplianceExtension> ComplianceExtensions => Set<ComplianceExtension>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<ClientCompliance> ClientCompliances => Set<ClientCompliance>();
    public DbSet<Deadline> Deadlines => Set<Deadline>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentFolder> DocumentFolders => Set<DocumentFolder>();
    public DbSet<DocumentUploadLink> DocumentUploadLinks => Set<DocumentUploadLink>();
    public DbSet<TaskDocument> TaskDocuments => Set<TaskDocument>();
    public DbSet<ConfigDefinition> ConfigDefinitions => Set<ConfigDefinition>();
    public DbSet<ConfigValue> ConfigValues => Set<ConfigValue>();
    public DbSet<ConfigChangeLog> ConfigChangeLog => Set<ConfigChangeLog>();
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Read by the query filters at query time. Guid.Empty = no firm = no rows.</summary>
    public Guid CurrentFirmId => tenant.HasTenant ? tenant.FirmId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Firm>(e =>
        {
            e.ToTable("firms");
            e.Property(x => x.TaxIds).HasJsonbDictionary();
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.BaseCurrency).HasMaxLength(3).IsFixedLength();
            // A firm sees only its own row.
            e.HasQueryFilter(f => f.Id == CurrentFirmId);
        });

        b.Entity<Client>(e =>
        {
            e.ToTable("clients");
            e.Property(x => x.TaxIds).HasJsonbDictionary();
            e.Property(x => x.Custom).HasJsonbDictionary();
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.HasIndex(x => new { x.FirmId, x.Code }).IsUnique().HasDatabaseName("ux_clients_firm_code");
            e.HasIndex(x => new { x.FirmId, x.Name }).HasDatabaseName("ix_clients_firm_name");
        });

        b.Entity<ClientGroup>(e => e.ToTable("client_groups"));

        b.Entity<ClientContact>(e =>
        {
            e.ToTable("client_contacts");
            e.Property(x => x.Email).HasColumnType("citext");
        });

        b.Entity<ClientNote>(e => e.ToTable("client_notes"));

        b.Entity<CustomFieldDefinition>(e =>
        {
            e.ToTable("custom_field_definitions");
            e.Property(x => x.Options).HasJsonbList();
        });

        b.Entity<TaxIdType>(e =>
        {
            e.ToTable("tax_id_types");                // platform data: same rows for every firm
            e.HasKey(x => new { x.CountryCode, x.Code });
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
        });

        b.Entity<ComplianceType>(e =>
        {
            e.ToTable("compliance_types");             // platform data: same rows for every firm
            e.HasKey(x => x.Code);
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.PeriodOverrides).HasColumnType("jsonb");
            e.Property(x => x.AppliesWhen).HasColumnType("jsonb");
        });

        b.Entity<ComplianceExtension>(e =>
        {
            e.ToTable("compliance_extensions");
            e.HasKey(x => new { x.TypeCode, x.PeriodKey });
        });

        b.Entity<Holiday>(e =>
        {
            e.ToTable("holidays");
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            // public holidays are visible to every firm; a firm's own closed days only to that firm (RLS agrees)
            e.HasQueryFilter(h => h.FirmId == null || h.FirmId == CurrentFirmId);
        });

        b.Entity<ClientCompliance>(e => e.ToTable("client_compliances"));
        b.Entity<Deadline>(e => e.ToTable("deadlines"));

        // relationships declared so EF inserts a task before its steps and history in one save
        b.Entity<WorkTask>(e =>
        {
            e.ToTable("tasks");
            e.HasOne<Deadline>().WithMany().HasForeignKey(x => x.DeadlineId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TaskChecklistItem>(e =>
        {
            e.ToTable("task_checklist_items");
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TaskComment>(e =>
        {
            e.ToTable("task_comments");
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(x => x.Params).HasColumnType("jsonb");
        });
        b.Entity<NotificationTemplate>(e =>
        {
            e.ToTable("notification_templates");
            e.Property(x => x.WaParams).HasJsonbList();
            e.HasQueryFilter(t => t.FirmId == null || t.FirmId == CurrentFirmId);
        });

        b.Entity<Document>(e => e.ToTable("documents"));
        b.Entity<DocumentFolder>(e =>
        {
            e.ToTable("document_folders");             // platform data: same rows for every firm
            e.HasKey(x => x.Code);
        });
        b.Entity<DocumentUploadLink>(e => e.ToTable("document_upload_links"));
        b.Entity<TaskDocument>(e =>
        {
            e.ToTable("task_documents");
            e.HasKey(x => new { x.TaskId, x.DocumentId });
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TaskTemplate>(e =>
        {
            e.ToTable("task_templates");
            e.Property(x => x.Items).HasJsonbStringList();
            e.HasQueryFilter(t => t.FirmId == null || t.FirmId == CurrentFirmId);
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Email).HasColumnType("citext");
            e.HasIndex(x => new { x.FirmId, x.Email }).IsUnique().HasDatabaseName("ux_users_firm_email");
            e.HasMany(x => x.UserRoles).WithOne().HasForeignKey(x => x.UserId);
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId);
        });

        b.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(x => new { x.RoleId, x.PermissionCode });
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });

        b.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasKey(x => x.Code);
        });

        b.Entity<RefreshToken>(e => e.ToTable("refresh_tokens"));

        b.Entity<UserMfa>(e =>
        {
            e.ToTable("user_mfa");
            e.HasKey(x => x.UserId);
        });

        b.Entity<Invitation>(e =>
        {
            e.ToTable("invitations");
            e.Property(x => x.Email).HasColumnType("citext");
        });

        b.Entity<ConfigDefinition>(e =>
        {
            e.ToTable("config_definitions");
            e.HasKey(x => x.Key);
            e.Property(x => x.JsonSchema).HasColumnType("jsonb");
            e.Property(x => x.DefaultValue).HasColumnType("jsonb");
            e.Property(x => x.Labels).HasColumnType("jsonb");
            e.Property(x => x.HelpText).HasColumnType("jsonb");
        });

        b.Entity<ConfigValue>(e =>
        {
            e.ToTable("config_values");
            e.Property(x => x.Value).HasColumnType("jsonb");
            // platform / country / plan rows (no firm) are visible to every firm; firm rows only to their firm
            e.HasQueryFilter(v => v.FirmId == null || v.FirmId == CurrentFirmId);
        });

        b.Entity<ConfigChangeLog>(e =>
        {
            e.ToTable("config_change_log");
            e.Property(x => x.OldValue).HasColumnType("jsonb");
            e.Property(x => x.NewValue).HasColumnType("jsonb");
            e.HasQueryFilter(c => c.FirmId == CurrentFirmId);
        });

        b.Entity<Secret>(e => e.ToTable("secrets"));

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");                  // partitioned by month in PostgreSQL; PK is (id, at)
            e.Property(x => x.OldValues).HasColumnType("jsonb");
            e.Property(x => x.NewValues).HasColumnType("jsonb");
            e.HasQueryFilter(a => a.FirmId == CurrentFirmId);
        });

        ApplyTenantFilters(b);
        ApplySnakeCaseNames(b);
        ApplyRowVersion(b);
    }

    // ---- tenant + soft-delete filters for every ITenantOwned entity ----

    private void ApplyTenantFilters(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            var method = typeof(ISoftDelete).IsAssignableFrom(et.ClrType)
                ? nameof(SetTenantSoftDeleteFilter)
                : nameof(SetTenantFilter);
            GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(et.ClrType)
                .Invoke(this, [b]);
        }
    }

    private void SetTenantFilter<T>(ModelBuilder b) where T : class, ITenantOwned =>
        b.Entity<T>().HasQueryFilter(e => e.FirmId == CurrentFirmId);

    private void SetTenantSoftDeleteFilter<T>(ModelBuilder b) where T : class, ITenantOwned, ISoftDelete =>
        b.Entity<T>().HasQueryFilter(e => e.FirmId == CurrentFirmId && e.DeletedAt == null);

    // ---- naming: snake_case columns to match the SQL migrations ----

    private static void ApplySnakeCaseNames(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes())
        {
            foreach (var p in et.GetProperties())
            {
                if (p.ClrType == typeof(uint) && p.Name == nameof(TenantEntity.Version)) continue; // xmin
                p.SetColumnName(p.Name.ToSnakeCase());
            }
            foreach (var k in et.GetKeys()) k.SetName(k.GetName()?.ToSnakeCase());
            foreach (var i in et.GetIndexes()) i.SetDatabaseName(i.GetDatabaseName()?.ToSnakeCase());
        }
    }

    // ---- optimistic concurrency on PostgreSQL xmin ----

    private static void ApplyRowVersion(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes().Where(t => typeof(TenantEntity).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(et.ClrType).Property(nameof(TenantEntity.Version))
                .HasColumnName("xmin").HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }
    }

    // ---- stamping on save ----

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Stamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void Stamp()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Firm firm)
            {
                if (entry.State == EntityState.Added) firm.CreatedAt = now;
                else if (entry.State == EntityState.Modified) firm.UpdatedAt = now;
            }

            if (entry.Entity is ITenantOwned owned)
            {
                if (entry.State == EntityState.Added)
                    owned.FirmId = tenant.FirmId; // throws TenantNotSetException if no firm
                else if (entry.State is EntityState.Modified or EntityState.Deleted && owned.FirmId != CurrentFirmId)
                    throw new InvalidOperationException("Attempt to change another firm's data was blocked.");
            }

            if (entry.Entity is IAuditable a)
            {
                if (entry.State == EntityState.Added)
                {
                    a.CreatedAt = now;
                    a.CreatedBy = tenant.UserId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    a.UpdatedAt = now;
                    a.UpdatedBy = tenant.UserId;
                }
            }
        }
    }
}
