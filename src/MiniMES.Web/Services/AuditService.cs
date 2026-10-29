using MiniMES.Web.Models;
using MiniMES.Web.Repositories;

namespace MiniMES.Web.Services;

public sealed class AuditService(AuditRepository audits)
{
    public Task<IReadOnlyList<AuditEntry>> ListAsync() => audits.ListAsync();
}
