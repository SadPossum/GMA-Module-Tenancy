# Tenancy Module

The Tenancy module enables tenant resolution for projects that need tenant isolation. It is optional and explicitly registered by `Host.Api`.

## Project

```text
Gma.Modules.Tenancy.Api
```

The module is intentionally small. Framework tenancy abstractions live in `Gma.Framework.Tenancy` and default/null implementations live in `Gma.Framework.Tenancy.Infrastructure`, which the framework infrastructure composes by default.

## Runtime Behavior

When Tenancy is enabled:

- tenant id is resolved from `X-Tenant-Id`;
- tenant-scoped endpoints require a tenant id;
- tenant context is available through `ITenantContext`;
- tenant-owned models implement `IScopedEntity`, usually through `ScopedAggregateRoot<TId>` or `ScopedEntity<TId>`.

When Tenancy is not registered:

- shared infrastructure provides a null/default tenant context;
- non-tenant projects can still run.

## Endpoint

Base path:

```text
/api/tenants
```

Endpoint:

```text
GET /current
```

This returns `CurrentTenantResponse`, containing the current tenant id and whether tenancy is enabled.

## Trust Boundary

`RequireTenant()` resolves and validates tenant context. It does not authenticate a caller by itself. Applications must also compose their authorization policy or register an `ITenantEndpointAccessPolicy`, such as the Organizations + Tenancy extension, before tenant-selected endpoints access protected data.

Use `RequireTenantWithIndependentAuthentication()` only for endpoints that authenticate the caller themselves before performing protected work.

## Configuration

```json
{
  "Tenancy": {
    "Enabled": true,
    "HeaderName": "X-Tenant-Id",
    "LocalDefaultTenantId": "default"
  }
}
```

`HeaderName` is validated as an HTTP header token and tenant ids are normalized by `TenantIds`: trimmed, non-empty, case-preserving, at most 128 characters, and without whitespace or control characters to match module persistence mappings. `LocalDefaultTenantId` follows the same rule and is used by the shared null tenant context when the optional Tenancy module is not registered.

## Endpoint Usage

Tenant-scoped endpoints should call:

```csharp
.RequireTenant()
```

This applies the tenant endpoint filter from `Gma.Framework.Api`.

## Persistence Usage

Tenant-scoped entities should implement tenant-scoped behavior consistently:

- store a normalized `ScopeId` through `ScopedAggregateRoot<TId>`, `ScopedEntity<TId>`, or an explicit `IScopedEntity` implementation;
- use `ScopeAwareDbContext<TContext>` and `ApplyScopeConventions(modelBuilder)` for EF scope property configuration and the named `ScopeFilter`;
- rely on the shared write guard to reject invalid, unnormalized, or mismatched scope ids before `SaveChanges`;
- avoid bypassing filters without a specific reason;
- include tenant id in unique indexes where uniqueness is tenant-local.

Do not add runtime shadow `TenantId` properties to arbitrary EF models. The allowed convention/magic is limited to shared EF helpers inspecting the current `ModelBuilder`; modules still declare tenant ownership explicitly.

## Testing

Any module with tenant-scoped data should prove:

- tenant A cannot see tenant B data;
- tenant A tokens cannot refresh or mutate tenant B sessions;
- missing tenant id is rejected where tenancy is required.

This repository directly verifies module registration, scoped context isolation, header resolution, invalid input, access-policy denial, and the current-tenant response contract on Windows and Linux.

## Future Options

The current strategy is shared database tenancy. Future strategies can be added behind shared abstractions:

- separate schema per tenant;
- separate database per tenant;
- external tenant registry;
- tenant-specific feature flags.

Do not build those until a project needs them.
