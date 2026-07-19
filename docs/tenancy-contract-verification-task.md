# Tenancy Contract Verification Task

Status: completed
Date: 2026-07-19
Completed: 2026-07-19

## Goal

Make the optional Tenancy front door independently verifiable without moving generic scoping, authentication, organization admission, or product workspace behavior into the module.

## Audit Baseline

- Framework owns generic scope and tenancy abstractions, endpoint filtering, persistence guards, runtime propagation, and optional infrastructure defaults;
- Tenancy owns the opt-in HTTP tenant context, header-resolution profile, and `/api/tenants/current` endpoint;
- Organizations-backed tenant admission remains in GMA Extensions, while product workspace language and policies remain product-owned;
- the module is allocation-light and has no persistence or cross-module dependency;
- its zero-warning build and package vulnerability audit pass against the current Framework head;
- the repository currently has no direct tests, no local reusable-module boundary guard, and no typed response contract for its endpoint;
- standalone CI validates only Windows and does not run a package vulnerability audit.

## Delivery Slice

1. Publish a typed response contract for the current-tenant endpoint without changing its wire shape.
2. Add focused tests for module metadata, forced enablement, scoped context replacement and isolation, custom-header resolution, invalid input, and registered access-policy denial.
3. Add a repository-local boundary guard that rejects dependencies on other reusable modules and product-specific source.
4. Run boundary and package checks in the standalone workflow on Windows and Ubuntu.
5. Clarify that tenant resolution selects scope but does not authenticate a caller; applications must compose authorization or an admission extension.
6. Verify the published head through GMA Skeleton and BunkFy without introducing Framework, Extensions, or product behavior changes.

## Acceptance Criteria

- the endpoint returns a public `CurrentTenantResponse` with the existing `tenantId` and `isEnabled` JSON fields;
- module registration replaces the default context with one shared scoped instance for read/write interfaces and forces tenancy on;
- separate service scopes do not share tenant state;
- missing or invalid tenant headers fail closed when the module is enabled;
- registered tenant endpoint access policies can deny the module endpoint;
- source projects reference no other reusable module and contain no product-specific code;
- standalone build, tests, boundary checks, and package audit pass on Windows and Ubuntu;
- Skeleton and BunkFy pass against the exact published Tenancy head.

## Completion Evidence

- Tenancy `f087bff` passes its zero-warning build, 4 focused tests, reusable-module boundary guard, and package vulnerability audit; GitHub Actions run `29698674709` is green on Windows and Ubuntu.
- Framework remains unchanged at `c62d4ee`, and GMA Extensions remains unchanged at `abed85e`; generic scoping and Organizations admission responsibilities did not move.
- GMA Skeleton `d1116d0` passes the complete local verification gate, including all 264 architecture tests, and GitHub Actions run `29698896788` is green on Windows and Ubuntu.
- BunkFy backend `f8ba3ef` passes the complete local verification gate; GitHub Actions validate run `29699490656` and Docker run `29699490670` are green.
- BunkFy web `0b6f75e` contains only the regenerated OpenAPI snapshot and TypeScript contract for `CurrentTenantResponse`; GitHub Actions run `29700521031` is green.
- BunkFy root `27b5cb3` includes the synchronized workspace solution and exact backend/web pins; clean-checkout GitHub Actions run `29700701789` is green.
- The clean-checkout gate first exposed missing workspace-solution and generated-contract updates; both were corrected before this task was completed.
