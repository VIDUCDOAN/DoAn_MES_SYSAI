namespace MiniMES.Web.Models;

public sealed class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; }
    public int SessionVersion { get; set; }
}

public sealed class Product
{
    public int Id { get; set; }
    public string ModelCode { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public int Version { get; set; }
    private DateTime _createdAt;
    // SQL datetime2 không giữ DateTime.Kind; các cột thời gian hệ thống này luôn là UTC.
    public DateTime CreatedAt
    {
        get => DateTime.SpecifyKind(_createdAt, DateTimeKind.Utc);
        set => _createdAt = value;
    }
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public string Who { get; set; } = "";
    private DateTime _when;
    public DateTime When
    {
        get => DateTime.SpecifyKind(_when, DateTimeKind.Utc);
        set => _when = value;
    }
    public string WhereIp { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string? OldDataJson { get; set; }
    public string? NewDataJson { get; set; }
    public string CorrelationId { get; set; } = "";
}
