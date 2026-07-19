namespace Gma.Modules.Tenancy.Contracts;

public sealed record CurrentTenantResponse(string TenantId, bool IsEnabled);
