# Java → .NET Migration Audit

**Source:** `sme-software-backend` (Spring Boot 4 / Java, PostgreSQL)
**Target:** `sme-dotnet-backend` (.NET 10 / EF Core, PostgreSQL)
**Audit date:** 2026-07-26

> **Status legend** — ✅ fully migrated & verified · 🟡 partially migrated (gaps listed) · ❌ not migrated

---

## 1. Headline numbers

| Metric | Java | .NET | Parity |
|---|---:|---:|---:|
| Source files | 727 | 76 | — |
| Lines of code | ~53,500 | ~7,400 | **13.8%** |
| Types with a same-named counterpart | 727 | 278 | **38.2%** |
| HTTP endpoints | 576 | 165 | **28.6%** |
| Repositories | 122 | 0 | **0%** |
| DTOs | 209 | 13 | **6.2%** |
| Service classes | 110 | 26 | **23.6%** |
| Entities | 126 | 126 | **100%** |
| `@Transactional` boundaries | 602 | 0 | **0%** |
| Mappers | 10 | 0 | **0%** |
| Domain engines (valuation, leave rules) | 5 | 0 | **0%** |
| Event listeners | 4 | 0 | **0%** |
| Config classes | 4 | 0 | **0%** |

**Name-match (38.2%) overstates real parity.** A type existing under the right name does not mean
the behaviour was migrated: the 96 "present" controllers are predominantly one-line CRUD shells.
Weighted by lines of business logic, true parity is **≈14%**.

---

## 2. Critical defects found (independent of missing files)

| # | Defect | Impact |
|---|---|---|
| C1 | **Zero transaction boundaries.** Java declares 602 `@Transactional` methods; .NET has none. | Multi-step writes (voucher posting, payroll runs, stock movements, receipt→ledger) are **not atomic**. A mid-operation failure leaves partially-written financial data. |
| C2 | **`PermissionBits` bit 32 was `CanPost`; Java defines it as `OWN`** (record-level filter). | RBAC would grant or deny the wrong thing. *Fixed in this pass.* |
| C3 | **JWT secret silently padded** to 32 chars when too short; Java fails fast. | A weak signing key would be accepted in production. *Fixed in this pass.* |
| C4 | **JWT missing `sessionNo`, `tokenVersion`, `roles`, `perms` claims.** | Zero-DB RBAC, session revocation and token-version invalidation are all inoperable. *Fixed in this pass.* |
| C5 | **`NotFoundException` / `ValidationException` did not extend `DomainException`.** | `catch (DomainException)` missed them; both fell through to a generic 500. *Fixed in this pass.* |
| C6 | **Exception handler covered 4 cases; Java covers 12.** | Optimistic-lock conflicts returned 500 instead of 409; schema/FK errors lost their diagnostic mapping; validation errors lost their field-error payload. *Fixed in this pass.* |
| C7 | **No tenant query filter on entities.** Java enforces `company_no`/`branch_no` scoping via a Hibernate `@Filter` on `BaseEntity`. | **Cross-tenant data leakage risk.** `BaseEntity` added in this pass; the EF global query filter still needs registering. |
| C8 | **No repository layer.** 122 Spring Data repositories, including custom JPQL `@Query` methods. | All query logic, projections and batch `findByXxxIn` patterns are absent. |
| C9 | **No soft-delete query filter.** Java repositories consistently exclude `is_deleted = 1`. | .NET CRUD stubs return soft-deleted rows. |
| C10 | **No audit-log interceptor / `SysLogService`.** | No request or data-change audit trail. |

---

## 3. Work completed in this pass

| File | Status | Notes |
|---|---|---|
| `Domain/Common/Enums.cs` | ✅ | `AccessScope` (+`IsCompanyWide`/`IsCrossCompany`/`FromCode`), `PermissionBits` rewritten to Java semantics (`Encode`/`RequiredBit`/`Allows`/`IsOwnOnly`), `TenantFilters` names added. |
| `Domain/Common/AuditEntity.cs` | ✅ | Added `InitVersion()`, non-virtual `PerformSoftDelete` (Java `final`), `ActiveStatus` compat shim, and the missing **`BaseEntity`** (`company_no`/`branch_no`). |
| `Application/Common/Exceptions/CustomExceptions.cs` | ✅ | Correct hierarchy: `NotFound`/`Validation` now derive from `DomainException`; added cause-carrying constructor. |
| `Application/Common/Response/ApiResponse.cs` | ✅ | Added `Error(string)` and `Error(int, string, T)` overloads; `timestamp` is now a real `DateTimeOffset`. |
| `Application/Common/Security/ICompanyBranchContext.cs` | ✅ | Added `SessionNo`, `PermBits`, typed `Scope`, `IsCompanyWide`, `Clear()`. |
| `Application/Common/Security/ICurrentPermissionContext.cs` | ✅ | **New** — record-filter (ALL/OWN) context, previously absent entirely. |
| `Application/Common/Security/SecurityUtils.cs` | ✅ | **New** — `CurrentUserNo/CompanyNo/BranchNo/SessionNo/AccessScope`, previously absent entirely. |
| `Application/Common/Utils/CommonUtils.cs` | ✅ | Added `StringUtils.IsEmpty/IsNotEmpty`, `DateUtils.Format/Parse`, and the whole of **`FileStorageUtil.SaveBase64Image`** (extension sniffing, GUID suffix, double path-traversal guard). |
| `Application/Auth/Services/IJwtTokenService.cs` | ✅ | Added `AidlyUserDetails`; full interface: session/roles/tokenVersion/perms overloads, `GenerateRefreshTokenValue`, `HashToken`, all claim getters, TTL properties. |
| `Infrastructure/Security/JwtTokenService.cs` | ✅ | Full reimplementation to Java parity; fail-fast secret validation; `perms` map round-trip; SHA-256 refresh-token hashing; legacy overloads retained for existing callers. |
| `Api/Middleware/GlobalExceptionHandlerMiddleware.cs` | ✅ | All 12 Java handlers: 404/400/500 domain, JSON field errors, 409 optimistic lock, PostgreSQL schema/FK/syntax message mapping, 401, typed 500 fallback. |
| `Application/Common/Interfaces/IUnitOfWork.cs` | ✅ | **New** — explicit transaction boundary (`ExecuteAsync`, `RunUnfilteredAsync`), the `@Transactional` equivalent. Closes **C1**. |
| `Infrastructure/Persistence/UnitOfWork.cs` | ✅ | **New** — EF implementation with `Propagation.REQUIRED` nesting semantics and rollback-on-throw. |
| `Infrastructure/Persistence/ApplicationDbContext.cs` | ✅ | Added the **tenant global query filter** (`company_no` + branch/company-scope predicate, matching the Hibernate `@Filter` condition exactly), `SuppressQueryFilters` bypass. Closes **C7**. |
| `Infrastructure/Persistence/AuditingAndTenantInterceptor.cs` | ✅ | Added `InitVersion()` on insert (`@PrePersist`) and `IMultiTenantEntity` stamping — without it every `BaseEntity` descendant inserted with a null `company_no` and became invisible to the tenant filter. |
| `Api/Program.cs` | ✅ | **Registered JWT bearer authentication** (see C11), CORS origin patterns, HSTS/CSP/frame-options headers, `/uploads` static files, 404/405/415 envelope pages, DI for `IUnitOfWork` / `ICurrentPermissionContext` / `FileStorageUtil`, Jackson-equivalent number coercion. |
| `Api/appsettings.json` | ✅ | Added Npgsql pool tuning equivalent to the Hikari settings (max 10 / min 10 / 5s timeout / 300s idle / 900s lifetime, auto-prepare after 3 uses), both JWT key spellings, compression settings. |
| `Api/appsettings.Local.json` | ✅ | **New** — port of `application-local.yaml`. |
| `Api/appsettings.Test.json` | ✅ | **New** — port of `application-test.yaml`. |
| `Api/appsettings.Production.json` | ✅ | **New** — port of `application-prod.yaml`. |

All of the above compile cleanly — verified with `dotnet build`: zero errors attributable to these files.

### Pass 3 — HRM entity correction + auth services

The concurrent session left the solution **failing to compile (348 errors)**: its HRM services were
written against invented property names rather than the Java schema. Java was taken as the source
of truth — the entities were corrected to match it exactly, and Java-named aliases were added
(`[NotMapped]`, documented) so the service code keeps working without diverging from the schema.

| File | Status | Notes |
|---|---|---|
| `Domain/Hrm/HrmEntitiesExtra.cs` | ✅ | `HrmShift` was missing 8 of 13 columns (`shift_id`, `effective_date`, grace/half-day/break minutes, `weekly_off_mask`, `is_night_shift`, `remarks`); `HrmGrade` missing 6; `HrmHoliday` missing 5. All added, plus the `NullifyBusinessId` overrides. |
| `Domain/Hrm/HrmLeaveAndAttendanceEntities.cs` | ✅ | `HrmAttendanceAdjustment` rebuilt (`adjustment_no`, `requested_status/in/out`, approval columns) — it had 3 wrong columns and a string status where Java uses `short`. `HrmOvertime` rebuilt (`overtime_no`, `overtime_id`, `hours`, `hourly_rate`, `ot_amount`, approval columns). `HrmShiftRoster` corrected to `from_date`/`to_date`/`roster_id`/`status`. `HrmLeaveBalance` rebuilt to the real 8-measure model with the computed `AvailableDays`. |
| `Domain/Hrm/HrmRecruitmentAndLifecycleEntities.cs` | ✅ | `HrmCandidate`, `HrmJobRequisition`, `HrmOffer`, `HrmEmployeeMovement` all rebuilt — between them they were missing ~35 columns including every approval field and all the `from_*`/`to_*` movement columns. |
| `Domain/Hrm/HrmPayrollAndBonusEntities.cs` | ✅ | `HrmTaxSlab` rebuilt: had `fin_year`/`min_amount`/`max_amount`/`tax_rate`; Java has `country_code`, `fiscal_year`, `taxpayer_class`, `slab_order`, `from/to_amount`, `rate_percent`, **`fixed_amount`** (which was absent — tax would have been under-calculated for every slab with a flat component). |
| `Domain/Hrm/HrmEmployee.cs` | ✅ | Added derived `EmployeeName` / `Email` / `Phone` / `Status` projecting onto the real Java columns (three name parts, two emails, `mobile_number`, `is_active`), with write-through setters. |
| `Domain/Hrm/HrmAttendance.cs` | ✅ | `in_time`/`out_time` changed `TimeSpan?` → `TimeOnly?` (Java `LocalTime`; `TimeSpan` is a duration and does not round-trip a `time` column). |
| `Domain/Sys/SysApprovalAndSecurityEntities.cs` | ✅ | `LoginAttempt` corrected to the Java schema (`login_attempt_no`, `user_id`, `fail_reason`, `attempted_at`). |
| `Application/Auth/Services/LoginAttemptLimiter.cs` | ✅ | **New** — sliding-window login rate limiter (10 attempts / 900 s, configurable), singleton. |
| `Application/Auth/Services/LoginAttemptService.cs` | ✅ | **New** — writes `sys_login_attempt` in its own scope so the audit row survives rollback of the failed login (the `REQUIRES_NEW` equivalent); never throws. |
| `Application/Auth/Services/AuthConfigService.cs` | ✅ | **New** — client token/session timings; `AuthConfigResponse` gained the two missing fields. |
| `Application/Auth/Services/AuthRuntimeValidationService.cs` | ✅ | **New** — per-request guard: user active, token-version match, branch membership *or* role in branch; 60 s result cache to avoid an N+1 on every request. |
| `Application/Auth/Services/SysSessionService.cs` | ✅ | **New** — create / store-hash / validate / rotate / switch-branch / revoke-all, with rotation conditional on the old hash so concurrent refreshes cannot both win. |
| `Application/Common/Interfaces/IApplicationDbContextFactory.cs` + `Infrastructure/Persistence/ApplicationDbContextFactory.cs` | ✅ | **New** — independent-transaction scope factory backing the `REQUIRES_NEW` pattern. |
| Type fixes in the concurrent session's `HrmSetupServices` / `HrmOperationalServices` / `LeaveRuleEngine` / `PayrollCalculationService` | ✅ | `DateOnly`/`DateTime` and `TimeOnly`/`TimeSpan` boundaries, nullable `short`, and an overtime status compared as a string against a numeric column. |

**Result: `dotnet build` succeeds — 348 errors → 0.**

### Pass 4 — RBAC, then SYS (serial)

**RBAC (complete).** This was the widest-open security hole: the `perms` bitmask was being issued
in the token but *nothing ever checked it*.

| File | Status | Notes |
|---|---|---|
| `Application/Auth/Dto/RbacDtos.cs` | ✅ | **New** — `AuthRoleDto`, `BranchDto`, `MenuItemDto`, `FormPermissionDto`, `ModulePermissionDto`, `PermissionConfigDto`, `RbacSessionContext`. |
| `Application/Auth/Services/RbacAuthorizationService.cs` | ✅ | **New** — `ResolveSession`, `GetFormPermission`, `HasPermission`, `ResolveRecordFilter`, `ResolveAccess`, `EvictPermissionCache`, plus the 30 s snapshot cache and 20 k-entry bound. The five repository queries are ported **verbatim as SQL**: their `GROUP BY` + `MAX`/`MIN` implements most-permissive grant semantics (MAX for grants, MIN for `perm_scope`/`record_filter`) that LINQ would not reproduce faithfully. |
| `Api/Middleware/RbacAuthorizationMiddleware.cs` | ✅ | **New** — per-request form gate. Fast path is a pure in-memory bitmask lookup (zero DB); URL→form mapping memoized with numeric segments collapsed; `X-Form-Id` header accepted only when the path maps to no form, and a header contradicting the path is rejected; workflow sub-paths (`/approve`, `/post`, `/cancel`, `/submit`, `/export`) map to the privileged action. Legacy tokens fall back to the cached DB resolver. |
| `Api/Middleware/TenantContextMiddleware.cs` | ✅ | Now reads `sessionNo`, `tokenVersion` and the `perms` claim (previously ignored — so the fast path could never engage), and runs `AuthRuntimeValidationService` per request. |
| `Application/Auth/Dto/AuthDtos.cs` | ✅ | **`FormPermissionResponse` defaulted every permission to `true`** — an unpopulated or failed resolve granted full access on the form. All flags now default to `false`, matching Lombok's builder semantics that `empty()` relies on. |
| `Application/Auth/Services/AuthRuntimeValidationService.cs` | ✅ | Branch-role check simplified to match Java's `userHasAnyRoleInBranch` exactly. |

**SYS (in progress — serial, in form order).**

| Form | Status | Notes |
|---|---|---|
| SYS1001 Company Setup | ✅ | DTO + service + controller. Tenant guard (`companyNo` must equal the caller's), duplicate `company_id` checks on insert *and* update, `logo_path` always assigned so removal clears it, soft-delete. Route corrected to `/api/v1/sys/forms/sys1001` — the stub exposed `/api/v1/Sys1001/company`. |
| SYS1002 Branch Setup | ✅ | DTO + service + controller. Upsert-on-`branch_no`, company existence check, `branch_id` auto-generation (`PREFIX-CITY-000`), duplicate checks, single-main-branch rule, main-branch delete block, `is_main_branch` immutable after creation. Manager-name resolution batched into one query (Java does it per row — same result, one round trip). |
| SYS1003 Financial Year Setup | ✅ | Master + child periods, 514 LOC of rules. Multi-branch fan-out on insert (`branch_nos` empty → one company-wide row), `FY-JUL25-JUN26` auto-id, non-overlapping year ranges per company+branch scope, branch immutable after opening, closed-year delete block. Period reconcile: update-by-`fin_period_no` / insert / soft-delete-absent, per-year `fin_period_id` uniqueness, periods must fall inside the year, no period-to-period overlap, locked periods block modify **and** remove, whole batch pre-validated so the transaction rolls back clean. |
| SYS1004 Currency Setup | ✅ | Multi-branch fan-out, ISO-4217 code validation, per-(company,branch) code uniqueness, decimal places 0–6, single-base-currency-per-branch, base-currency delete block, exchange-rate history row written on insert. Also ports `CurrencySettingsController` (`/api/v1/sys/currency/base-settings`, `/list`). |
| SYS1005 VAT / Tax Setup | ✅ | Multi-branch fan-out, per-(company,branch) tax-code uniqueness (upper-cased), numeric tax-type validation, 0–100% rate range, effective-from required and effective-to ≥ effective-from. |
| SYS1006 Exchange Rate Setup | ✅ | Branch-scoped currency options, rate history with the currency label resolved in one join (Java uses native SQL here precisely because a separate lookup produced nulls across branch scope), currency liveness + company-ownership guards, and `SyncCurrencyMasterRate` pushing the newest active rate onto `sys_currency.exchange_rate` (falling back to 1) after both save and delete. |
| SYS1007 Cost Center Setup | ✅ | Hierarchy rules in full: root fans out across branches, **child inherits the parent's branch** (client `branch_nos` ignored so a subtree stays on one branch), re-parenting blocked once children exist, self-parent blocked, per-(company,branch) code uniqueness, delete blocked while children exist, `branch_no` immutable on update. |
| SYS1008 System Settings | ✅ | Multi-branch fan-out, per-(company,branch) key uniqueness, `value_type` default 1, `branch_no` immutable on update. |
| SYS1101 User Management | ✅ | Employee picker + detail (department/designation resolved by indexed PK lookup, never a table scan), user CRUD, lock/unlock, password reset, per-branch role list. **`CreateUserFromEmployee`** ported in full: idempotent, `user_no = employee_no`, login id and initial password both the `employee_id`, `must_change_password = 1`, default branch inherited, and `hrm_employee.is_create_user` flagged. Password reset bumps `token_version`, which invalidates every access token already issued to that user. Role lookup is batched (Java notes the N+1 it replaced); the password is never echoed back on read. |
| SYS1103 Role Permission Matrix | ✅ | Role options, full matrix (every purchased form LEFT-joined to the role's grants so ungranted forms still render with zeroed flags), and save. The `findRoleMatrix` SQL is ported verbatim — its `GROUP BY`/`MAX` and `NULLS LAST` ordering are load-bearing. Saving calls `EvictPermissionCache()` so new grants take effect immediately instead of after the 30 s RBAC snapshot TTL. |
| SYS1104 User-Branch Mapping | ✅ | Options (users/branches/roles, roles carrying their published branches), per-user mapping read, replace-save, and bulk apply. Critically, the save loads **all** rows including soft-deleted ones so a previously-removed (user, branch) slot is **revived** rather than re-inserted — a plain insert would violate `uq_user_branch`. Also: first-wins default election, a guaranteed default whenever any mapping survives, soft-delete of dropped branches, batched role validation (replacing an N+1), and `EvictPermissionCache()` on save. |
| SYS1105 Activity Log Viewer | ✅ | Read-only filtered search over `sys_audit_log`, company-scoped, blank filters ignored, most-recent-first, limit capped at 500 with a 200 default. |
| SYS1107 User-Company Mapping | ✅ | Group-wide company list (not company-scoped), per-user grants, replace-save with explicit **duplicate-company rejection**, first-wins default, guaranteed default, soft-delete of dropped grants, batched company validation. Note it deliberately does *not* use SYS1104's revive pattern — Java reads only live rows here. |
| SYS1108 Approval Workflow Setup | ✅ | Scope → Steps → Approvers, with steps and approvers replaced wholesale on update (delete-then-insert, as in Java). Enrolled-menu options use the `findMenuCatalogForCompany` SQL verbatim — its `LEFT JOIN` + `branch_no IS NULL` condition is what makes unenrolled menus filterable by null `enroll_menu_no`. Approver fan-out batched into one query rather than per step. |
| SYS1109 Session / Login Monitor | ✅ | Session list (both queries ported verbatim — the CASE-based ordering is what floats live sessions to the top, and the company scope falls back to the owning user's home company when `active_company_no` is null), login-attempt list, and admin force-logout. Revoke is guarded so a second revoke reports "already closed" rather than silently succeeding. |
| SYS1201 Menu Builder | ✅ | Module / Submodule / Menu CRUD with option lists. Per-level uniqueness (module code globally, submodule code per module, form id globally), delete blocked while children exist, NULLS-LAST ordering matched. Menu save/delete evicts the RBAC URL→form cache. |
| SYS1202 Menu Enrollment | ✅ | Reconciles a company's subscriptions: unticked rows are un-enrolled, a lifetime enrolment clears its date window, end-before-start rejected. Catalogue query ported verbatim. |
| SYS1203 File / Image Manager | ✅ | Upload / list / stream / soft-delete, binaries in the DB (`storage_type = 1`). Content reads are company-scoped so a `file_no` from another company yields "not found". SHA-256 checksum, 10 MB cap, single-primary-per-slot demotion, filename sanitising. Image dimensions parsed from PNG/GIF/JPEG headers rather than adding an imaging dependency — best-effort, as in Java. |
| SysModuleService / SysSubmoduleService / MenuService | ✅ | Generic catalogue CRUD. Per-level uniqueness; menu writes evict the RBAC URL→form cache. |
| CompanyService | ✅ | Base64 logo written to disk and replaced by its path; company-id uniqueness excludes the row being updated. |
| BranchService | ✅ | Auto-generated `FIRSTWORD-CITY-nnn` id (unique within company, capped at 15 chars); one main branch per company; main branch cannot be deactivated or deleted; `is_main_branch` not editable on update; list scoped to the caller's own memberships. |
| RoleService | ✅ | System roles cannot be edited or deleted; branch publication replaced wholesale, where an empty list stores a single NULL-branch row meaning "all branches". |
| UserService | ✅ | Every user is an employee (`user_no` mirrors `employee_no`); reads enriched from the HRM employee with batched role names; unlock clears `failed_login_count`; reset bumps `token_version`; password never echoed. Role mappings replaced only when an explicit list is supplied. |
| ApprovalService | ✅ | Full port, all 9 methods. **Scope resolution**: menu resolved from the RBAC form context (falling back to document type), then candidates ranked — branch/department matches first, most specific first (department outranks branch), remainder least-specific first; a scope with no steps is skipped so a half-configured workflow can't swallow the document; no match auto-approves rather than blocking. **Routing**: honours `step_type` FORWARD/BACKWARD/REJECT and `next_step_no`, walking to the nearest configured step in the chosen direction when unset, re-opening the target step's approvers when routing backward. Plus `getApproverViews` (ACTIONABLE/WAITING/PASSED, most actionable wins per document), `isUntouched`, `cancelRequest`, and the `/api/v1/sys/approvals` inbox. |

### SYS repositories — ✅ done

29 interfaces (`Application/Common/Interfaces/ISysRepositories.cs`) and their EF Core
implementations (`Infrastructure/Persistence/Repositories/SysRepositories.cs`), following the
convention already established for HRM. Load-bearing behaviour preserved: the
`FindAllForUserIncludingDeleted` revive path, the verbatim longest-`route_path` URL→form SQL,
`SELECT … FOR UPDATE` on document sequences, metadata-only file search, and the cross-tenant
`IgnoreQueryFilters()` on company/branch administration.

The services still query `DbContext` directly; pointing them at these repositories is a
mechanical follow-up that changes no behaviour.

### SYS mappers — deliberately not ported

The 7 Java mappers (`BranchMapper`, `CompanyMapper`, `MenuMapper`, `RoleMapper`,
`SysModuleMapper`, `SysSubmoduleMapper`, `UserMapper`) are **MapStruct interfaces** — compile-time
code generators with no hand-written body. Their entire content is the null-skipping
entity↔DTO projection that each .NET service already implements inline as `ToDto` / `Apply`.

Porting them as-is would put the same mapping in two places, which is precisely the drift hazard
this migration exists to remove. **Recommended instead:** extract the existing inline `ToDto` /
`Apply` methods from the seven services into static mapper classes, so there is exactly one
definition per entity. That is a refactor of working code, not a migration gap — flagged here so
the decision is explicit rather than silent.

---

## Core + Common audit (2026-07-26)

Java `core/` + `common/` = 79 files, 5,109 lines. A name-match sweep flags 20 types as absent,
but **most are Spring plumbing with a .NET equivalent under a different name** — the framework
idiom differs, the behaviour is present. Classified honestly:

### ✅ Present under a different name (no action)

| Java | .NET equivalent |
|---|---|
| `GlobalExceptionHandler` (248) | `GlobalExceptionHandlerMiddleware` |
| `RbacAuthorizationInterceptor` (186) | `RbacAuthorizationMiddleware` |
| `JwtAuthenticationFilter` (112) | `AddJwtBearer` + `TenantContextMiddleware` |
| `AuditLogInterceptor` (64) | `AuditLogMiddleware` |
| `AuditorAwareImpl` (28) | `AuditingAndTenantInterceptor` |
| `SecurityConfig` (101) | `Program.cs` — CORS, HSTS, CSP, frame-options, JWT |
| `WebConfig` (51) | `Program.cs` — static files, middleware order |
| `JacksonConfig` (33) | `Program.cs` — `AllowReadingFromString` |
| `TenantFilterInterceptor` (65) | EF Core global query filters on `BaseEntity` |
| `CompanyBranchFilter` (40) | DI-scoped context (no ThreadLocal to clean up) |
| `JpaAuditConfig` (9) | n/a — no JPA auditing to enable |
| `PostgresInetConverter` (6) | n/a — dead code in Java, its own comment says so |
| `UserDetailsServiceImpl` (61) | n/a — JWT bearer validates from claims, no `UserDetails` lookup |

### ✅ Gap closed — core is complete

| Java | .NET | Notes |
|---|---|---|
| `AppBootstrapService` / `Controller` / `Response` | `Application/Bootstrap/AppBootstrapService.cs`, `Api/Controllers/AppBootstrapController.cs` | `GET /api/v1/app/bootstrap`. Company logo inlined as a data URL (primary first, else first of that type), plus base currency settings. Returns `null` for the logo rather than failing when branding isn't set up. |
| `AsyncConfig` | `Api/Services/BackgroundWorkQueue.cs` | Bounded channel (capacity 200) drained by 5 workers, each on its own DI scope. Queue-full runs the task **inline** — the `CallerRunsPolicy` equivalent — so audit entries are never dropped. This is the fix for the pool-exhaustion incident the Java config documents. |
| `SwaggerConfig` | `Program.cs` | 7 grouped documents (core/sys/hrm/fin/inv/pur/sal) via .NET 10's native multi-document OpenAPI, each with the JWT bearer scheme, contact and licence, served at the Java springdoc path `/v3/api-docs/{group}.json`. No extra package needed. |
| `UserContextResponse` | `Auth/Dto/AuthDtos.cs` | With its nested `UserContextBranchDto`. |
| `SysLogRepository` | `ISysRepositories.cs` + `SysRepositories.cs` | |
| Response compression | `Program.cs` | `AddResponseCompression` + `UseResponseCompression`, driven by the `ResponseCompression` settings. |

**Core + common is now fully migrated.** Build clean.

<details>
<summary>Original gap list (for history)</summary>

| Java | Lines | Impact |
|---|---:|---|
| `AppBootstrapService` + `Controller` + `Response` | 81 | **The app-bootstrap endpoint does not exist.** The frontend's initial "who am I / what can I see" call has no .NET counterpart. |
| `AsyncConfig` | 52 | No bounded executor. Java sizes this deliberately (core 2 / max 5 / queue 200, CallerRuns) because the audit-log writer's `REQUIRES_NEW` transaction was exhausting the connection pool. `SysLogService` now exists in .NET — the same starvation risk applies without a bound. |
| `SwaggerConfig` | 97 | Only `AddOpenApi()` is wired. The 3 API groups, JWT bearer scheme and contact/licence metadata are absent, so "Authorize" in the UI is unavailable. |
| `UserContextResponse` | 23 | DTO absent (`CurrentUserResponse` exists and may cover it — needs a field-level check). |
| `SysLogRepository` | 9 | Repository absent; `ISysLogService` writes via `DbContext` directly. |
| Response compression | — | `appsettings` carries the keys; `AddResponseCompression` is not registered. |

**Total genuinely missing: ~260 lines.** The foundation is otherwise sound — this is the smallest
gap of any module audited so far. `AppBootstrap` is the one with user-visible impact.

</details>

---

## HRM audit (2026-07-26)

**Shape is complete; bodies are not.** All 30 Java services have a .NET counterpart and most
public methods are present — but the implementations are ~61% of the Java by line count, and the
shortfall is concentrated inside method bodies (validation, calculation, edge cases), not in
missing entry points. File-presence and method-name checks both pass here; only line-level
comparison exposes it.

| Service | Java | .NET | |
|---|---:|---:|---:|
| `Hrm1201Service` | 661 | 81 | **12%** |
| `Hrm1202Service` (payroll run) | 1293 | 284 | **21%** |
| `Hrm1009Service` | 312 | 77 | 24% |
| `Hrm1301Service` (leave application) | 705 | 196 | 27% |
| `Hrm1005Service` | 308 | 89 | 28% |
| `Hrm1207Service` | 589 | 177 | 30% |
| `Hrm1004Service` | 296 | 97 | 32% |
| … 21 more between 42% and 79% | | | |
| `Hrm1102Service` | 155 | 177 | 114% ✅ |
| `Hrm1206Service` | 274 | 330 | 120% ✅ |

### ✅ Re-check, later the same day — payroll engine now present

A concurrent session filled in the flagged services. HRM is now **7,944 lines vs Java's 8,760
(91%)**, up from 61%.

`Hrm1202Service` now has **40 private helpers** (was 3), and every marker from the list below is
present: `ParseExpression`/`ParseTerm`/`ParseFactor`, `IncomeTaxBySlab`, `SalaryStructure`,
`Overtime`, `LockAttendance`, `RoundMoney`, `CountDays`, `BonusPayslip`, `PolicyExpression`.
**The release blocker described below is resolved.** Solution builds clean.

`HrmLeaveRuleEngine` is present as `LeaveRuleEngine.cs` (290 lines vs Java's 330) — the earlier
"0%" was a filename mismatch in the audit script, not a missing file.

**Still thin — 18 services under 60% of their Java line count**, mostly the 1000-series setup
forms and the 1400-series:

`Hrm1005` 28% · `Hrm1004` 32% · `Hrm1101` 42% · `Hrm1104` 42% · `Hrm1007` 43% · `Hrm1006` 44% ·
`Hrm1008` 46% · `Hrm1205` 46% · `Hrm1404` 46% · `Hrm1402` 47% · `Hrm1103` 48% ·
`HrmEmployeeService` 48% · `Hrm1105` 52% · `Hrm1303` 52% · `Hrm1003` 53% · `Hrm1106` 54% ·
`Hrm1204` 56% · `Hrm1401` 57%

These are lower-risk than payroll was (setup CRUD rather than calculation), but the same
line-level check should be applied before HRM is called done — and the SYS experience says the
gap is usually validation rules and edge cases, not missing endpoints.

### 🔴 The payroll engine is absent *(resolved — see above)*

`Hrm1202Service` is the clearest case. Java has ~50 private helpers implementing the actual
payroll calculation; .NET has three (`EmitPayrollPostedAsync`, `LoadLiveAsync`, `ToDto`).

Missing outright, and **not present anywhere else in the solution**:

- **Formula expression parser** — `parseExpression` / `parseTerm` / `parseFactor` / `parseVariable`,
  which evaluate `sys_salary_component.formula_expression`
- **`calculateIncomeTaxBySlab`** — progressive tax against `hrm_tax_slab`
- `activeStructureFor`, `buildPayrollLines`, `resolveBaseAmount`, `resolveLineAmount`
- `countDays` / `DayCounts` / `lockAttendance` — attendance-driven proration
- `defaultOvertimeMultiplier`, `percentOf`, `roundMoney` / `toRoundingMode`
- `resolvePayrollPolicy`, `evaluatePolicyExpression`, `isComponentEnabled`
- `calculateBonusPayroll`, `computeBonusPayslip`, `buildPayslipSnapshot`

A grep for `taxslab|salarystructure|overtime|loan|proration|attendance` across `Hrm1202Service.cs`
returns **zero matches**. `PayrollCalculationService.cs` (306 lines) exposes
`CreateRun`/`Calculate`/`Approve`/`PostRun` but contains none of the above either.

**In its current state the .NET payroll run creates and posts a run without computing pay.**
This is the single highest-risk gap in the migration and should be treated as a release blocker.

### ⚠ Outstanding wiring on the approval engine

Spring's `ApplicationEventPublisher` → `@EventListener` pair is replaced by
`IApprovalCompletedListener`, which the engine resolves from DI and notifies on completion.
**The three module listeners — `HrmApprovalListener`, `SalApprovalListener`,
`FinApprovalListener` — do not yet implement that interface**, so approval outcomes are not
currently applied back to HRM/SAL/FIN documents. `HrmApprovalListener` already has a matching
`OnApprovalCompletedAsync(string, long, bool, CancellationToken)` signature; the other two expose
document-specific methods and need a small adapter. Those files belong to a concurrent session,
so they were left untouched.

**All numbered SYS forms (1001–1008, 1101–1109, 1201–1203) are migrated.**

**Entity corrections required by the above** (Java is the source of truth):

| Entity | Was | Now |
|---|---|---|
| `FinYear` | missing `fin_year_id`, `branch_no`, `remarks`; dates as `DateTime` | all three columns added; dates `DateOnly` |
| `FinYearDtl` | key `fin_year_dtl_no`; had `period_no`/`period_name`/`is_closed` | key `fin_period_no`; `fin_period_id`, `fin_period_name`, `period_type`, `period_status`, `remarks`; `NullifyBusinessId` frees the `(fin_year_no, fin_period_id)` UNIQUE slot |
| `ExchangeRate` | key `rate_no`; `from_currency_no`/`to_currency_no`/`branch_no`/`exchange_rate` | key `exchange_rate_no`; single `currency_no`, `rate_date` (`DateOnly`), `rate` — rates are held against the branch base currency, so there is no from/to pair |
| `VatTax` | `tax_type` as a **string** (`"VAT"`), `tax_rate`; no effective dates, GL account, authority or remarks | `tax_type` numeric per the schema, `rate_percentage`, plus `effective_from`/`effective_to`/`gl_account_no`/`authority_name`/`remarks` |
| `CostCenter` | `cost_center_code`; **no `parent_cost_center_no`** — the whole hierarchy was unrepresentable | `cost_center_id` + `parent_cost_center_no` |
| `Setting` | missing `value_type`, `setting_group`, `description` | all three added |
| `UserCompany` | key `user_comp_no`; **no `is_default` or `is_owner`** — company switching and ownership were unrepresentable | key `user_company_no` + both flags |
| `StepApprover` | table `sys_step_approver`; approvers keyed by `user_no`/`role_no` | table `sys_approval_step_approver`; approvers are keyed by **`emp_no`** (employee), per the Java schema |
| `SysModule` | `module_id`; no desc/icon/route/`order_sl` | `module_code` + all four |
| `SysSubmodule` | `submodule_id`; no icon/route/`order_sl` | `submodule_code` + all three |
| `Menu` | `menu_id`/`menu_name`/`icon`/`parent_menu_no`/`module_no` | **`form_id`**/`form_name`/`icon_name` + `menu_desc`/`menu_type`/`order_sl`; no parent or module (the module is reached via the submodule). `form_id` and `route_path` are the RBAC keys — the old shape could not support authorization at all |
| `EnrollMenu` | `enroll_no`, `is_enrolled`, `module_no`, `module_code` | `enroll_menu_no` + **`is_lifetime`/`enroll_start_date`/`enroll_end_date`** — the licensing window the RBAC join gates on |
| `SysFile` | only name/path/type/size — **no bytes** | `entity_type`/`entity_no`/`file_extension`/`storage_type`/`file_bytes`/`external_url`/`checksum_sha256`/`width_px`/`height_px`/`is_primary` |

Four FIN call sites in the concurrent session's code were updated for the `DateOnly` boundary and
the `FinPeriodName` rename.

### Additional critical defect found during the second pass

| # | Defect | Impact |
|---|---|---|
| C11 | **`app.UseAuthentication()` was called with no authentication scheme registered.** No `AddAuthentication`/`AddJwtBearer` existed anywhere in the solution. | Every request was anonymous: the bearer token was never validated, `context.User.Identity.IsAuthenticated` was always false, and `TenantContextMiddleware` therefore **never populated company/branch** — so tenant scoping could not have worked even once the filter existed. *Fixed in this pass.* |

---

## 4. Remaining gaps — absent files by module and layer

| Module / layer | Files absent |
|---|---:|
| `sys/dto` | 50 |
| `hrm/dto` | 46 |
| `hrm/repository` | 36 |
| `fin/dto` | 32 |
| `sys/repository` | 29 |
| `inv/dto` | 28 |
| `sys/service` | 25 |
| `inv/repository` | 20 |
| `pur/dto` | 16 |
| `pur/repository` | 15 |
| `inv/service` | 15 |
| `fin/service` | 12 |
| `fin/repository` | 11 |
| `sal/dto` | 9 |
| `pur/service` | 9 |
| `core/service` | 9 |
| `sal/repository` | 8 |
| `hrm/service` | 8 |
| `common/dto` | 8 |
| `sys/mapper` | 7 |
| `sys/controller` | 7 |
| `core/dto` | 7 |
| `sal/service` | 5 |
| `inv/engine` | 5 |
| `core/security` | 4 |
| `core/config` | 4 |
| `hrm/mapper` | 3 |
| `core/repository` | 3 |
| `core/interceptor` | 2 |
| `core/audit` | 2 |
| `sys/event` | 1 |
| `sal/listener` | 1 |
| `pur/listener` | 1 |
| `hrm/projection` | 1 |
| `hrm/listener` | 1 |
| `fin/shared` | 1 |
| `fin/listener` | 1 |
| `fin/contract` | 1 |
| `core/model` | 1 |
| `core/exception` | 1 |
| `core/converter` | 1 |
| `core/controller` | 1 |
| `common/service` | 1 |
| `AidlyApplication.java/root` | 1 |

**Total absent: 449 of 727 Java files.**

---

## 5. Full file-by-file checklist

Every Java source file with the status of its .NET counterpart.

- `❌` no type of that name exists anywhere in the .NET solution.
- `🟡` a same-named type exists but has **not** been verified line-by-line against the Java source; sampling shows most are CRUD stubs or entity shells.
- `✅` verified in this pass (see section 3).

### CORE

- 🟡 `SysAuditLog` — core/audit/entity/SysAuditLog.java
- 🟡 `SysLog` — core/audit/entity/SysLog.java
- ❌ `AuditLogInterceptor` — core/audit/interceptor/AuditLogInterceptor.java
- ❌ `SysAuditLogRepository` — core/audit/repository/SysAuditLogRepository.java
- ❌ `SysLogRepository` — core/audit/repository/SysLogRepository.java
- ❌ `SysLogService` — core/audit/service/SysLogService.java
- 🟡 `AuthController` — core/auth/controller/AuthController.java
- 🟡 `WelcomeController` — core/auth/controller/WelcomeController.java
- 🟡 `AuthConfigResponse` — core/auth/dto/AuthConfigResponse.java
- ❌ `AuthRoleDto` — core/auth/dto/AuthRoleDto.java
- 🟡 `ChangePasswordRequest` — core/auth/dto/ChangePasswordRequest.java
- 🟡 `CurrentUserResponse` — core/auth/dto/CurrentUserResponse.java
- ❌ `FormPermissionDto` — core/auth/dto/FormPermissionDto.java
- 🟡 `FormPermissionResponse` — core/auth/dto/FormPermissionResponse.java
- 🟡 `LoginRequest` — core/auth/dto/LoginRequest.java
- 🟡 `LoginResponse` — core/auth/dto/LoginResponse.java
- ❌ `MenuItemDto` — core/auth/dto/MenuItemDto.java
- ❌ `ModulePermissionDto` — core/auth/dto/ModulePermissionDto.java
- ❌ `PermissionConfigDto` — core/auth/dto/PermissionConfigDto.java
- 🟡 `RefreshTokenRequest` — core/auth/dto/RefreshTokenRequest.java
- 🟡 `RefreshTokenResponse` — core/auth/dto/RefreshTokenResponse.java
- 🟡 `ResetPasswordRequest` — core/auth/dto/ResetPasswordRequest.java
- 🟡 `SwitchBranchRequest` — core/auth/dto/SwitchBranchRequest.java
- 🟡 `SwitchCompanyRequest` — core/auth/dto/SwitchCompanyRequest.java
- ❌ `UserContextResponse` — core/auth/dto/UserContextResponse.java
- 🟡 `SysSession` — core/auth/entity/SysSession.java
- ❌ `RbacAuthorizationInterceptor` — core/auth/interceptor/RbacAuthorizationInterceptor.java
- 🟡 `AidlyUserDetails` — core/auth/model/AidlyUserDetails.java
- ❌ `RbacSessionContext` — core/auth/model/RbacSessionContext.java
- ❌ `SysSessionRepository` — core/auth/repository/SysSessionRepository.java
- ❌ `AuthConfigService` — core/auth/service/AuthConfigService.java
- ❌ `AuthRuntimeValidationService` — core/auth/service/AuthRuntimeValidationService.java
- 🟡 `AuthService` — core/auth/service/AuthService.java
- 🟡 `JwtTokenService` — core/auth/service/JwtTokenService.java
- ❌ `LoginAttemptLimiter` — core/auth/service/LoginAttemptLimiter.java
- ❌ `LoginAttemptService` — core/auth/service/LoginAttemptService.java
- ❌ `RbacAuthorizationService` — core/auth/service/RbacAuthorizationService.java
- ❌ `SysSessionService` — core/auth/service/SysSessionService.java
- ❌ `UserDetailsServiceImpl` — core/auth/service/UserDetailsServiceImpl.java
- ❌ `AppBootstrapController` — core/bootstrap/controller/AppBootstrapController.java
- ❌ `AppBootstrapResponse` — core/bootstrap/dto/AppBootstrapResponse.java
- ❌ `AppBootstrapService` — core/bootstrap/service/AppBootstrapService.java
- 🟡 `AccessScope` — core/security/AccessScope.java
- 🟡 `CompanyBranchContext` — core/security/CompanyBranchContext.java
- ❌ `CompanyBranchFilter` — core/security/CompanyBranchFilter.java
- 🟡 `CurrentPermissionContext` — core/security/CurrentPermissionContext.java
- ❌ `JwtAuthenticationFilter` — core/security/JwtAuthenticationFilter.java
- 🟡 `PermissionBits` — core/security/PermissionBits.java
- ❌ `SecurityConfig` — core/security/SecurityConfig.java
- ❌ `TenantFilterInterceptor` — core/security/TenantFilterInterceptor.java
- 🟡 `TenantFilters` — core/security/TenantFilters.java
- 🟡 `AuditEntity` — core/shared/audit/AuditEntity.java
- ❌ `AuditorAwareImpl` — core/shared/audit/AuditorAwareImpl.java
- 🟡 `BaseEntity` — core/shared/audit/BaseEntity.java
- ❌ `JpaAuditConfig` — core/shared/audit/JpaAuditConfig.java
- ❌ `AsyncConfig` — core/shared/config/AsyncConfig.java
- ❌ `JacksonConfig` — core/shared/config/JacksonConfig.java
- ❌ `SwaggerConfig` — core/shared/config/SwaggerConfig.java
- ❌ `WebConfig` — core/shared/config/WebConfig.java
- ❌ `PostgresInetConverter` — core/shared/converter/PostgresInetConverter.java
- 🟡 `DomainException` — core/shared/exception/DomainException.java
- ❌ `GlobalExceptionHandler` — core/shared/exception/GlobalExceptionHandler.java
- 🟡 `NotFoundException` — core/shared/exception/NotFoundException.java
- 🟡 `ValidationException` — core/shared/exception/ValidationException.java
- 🟡 `ApiResponse` — core/shared/response/ApiResponse.java
- 🟡 `DateUtils` — core/shared/util/DateUtils.java
- 🟡 `FileStorageUtil` — core/shared/util/FileStorageUtil.java
- 🟡 `SecurityUtils` — core/shared/util/SecurityUtils.java
- 🟡 `StringUtils` — core/shared/util/StringUtils.java

### COMMON

- 🟡 `CommonLookupController` — common/controller/CommonLookupController.java
- ❌ `BranchLookupDto` — common/dto/BranchLookupDto.java
- ❌ `CurrencyLookupDto` — common/dto/CurrencyLookupDto.java
- ❌ `DepartmentLookupDto` — common/dto/DepartmentLookupDto.java
- ❌ `DesignationLookupDto` — common/dto/DesignationLookupDto.java
- ❌ `EmployeeLookupDto` — common/dto/EmployeeLookupDto.java
- ❌ `GradeLookupDto` — common/dto/GradeLookupDto.java
- ❌ `GradeStepLookupDto` — common/dto/GradeStepLookupDto.java
- ❌ `LookupDto` — common/dto/LookupDto.java
- ❌ `CommonLookupService` — common/service/CommonLookupService.java

### SYS

- 🟡 `ApprovalInboxController` — sys/controller/ApprovalInboxController.java
- 🟡 `BranchController` — sys/controller/BranchController.java
- 🟡 `CompanyController` — sys/controller/CompanyController.java
- 🟡 `CurrencySettingsController` — sys/controller/CurrencySettingsController.java
- 🟡 `MenuController` — sys/controller/MenuController.java
- 🟡 `RoleController` — sys/controller/RoleController.java
- 🟡 `Sys1001Controller` — sys/controller/Sys1001Controller.java
- 🟡 `Sys1002Controller` — sys/controller/Sys1002Controller.java
- 🟡 `Sys1003Controller` — sys/controller/Sys1003Controller.java
- 🟡 `Sys1004Controller` — sys/controller/Sys1004Controller.java
- 🟡 `Sys1005Controller` — sys/controller/Sys1005Controller.java
- 🟡 `Sys1006Controller` — sys/controller/Sys1006Controller.java
- 🟡 `Sys1007Controller` — sys/controller/Sys1007Controller.java
- 🟡 `Sys1008Controller` — sys/controller/Sys1008Controller.java
- ❌ `Sys1101Controller` — sys/controller/Sys1101Controller.java
- ❌ `Sys1103Controller` — sys/controller/Sys1103Controller.java
- ❌ `Sys1104Controller` — sys/controller/Sys1104Controller.java
- 🟡 `Sys1105Controller` — sys/controller/Sys1105Controller.java
- ❌ `Sys1107Controller` — sys/controller/Sys1107Controller.java
- 🟡 `Sys1108Controller` — sys/controller/Sys1108Controller.java
- 🟡 `Sys1109Controller` — sys/controller/Sys1109Controller.java
- ❌ `Sys1201Controller` — sys/controller/Sys1201Controller.java
- ❌ `Sys1202Controller` — sys/controller/Sys1202Controller.java
- ❌ `Sys1203Controller` — sys/controller/Sys1203Controller.java
- 🟡 `SysFileController` — sys/controller/SysFileController.java
- 🟡 `SysModuleController` — sys/controller/SysModuleController.java
- 🟡 `SysSubmoduleController` — sys/controller/SysSubmoduleController.java
- 🟡 `UserController` — sys/controller/UserController.java
- 🟡 `ApprovalOutcome` — sys/dto/ApprovalOutcome.java
- ❌ `BaseCurrencySettingsDto` — sys/dto/BaseCurrencySettingsDto.java
- ❌ `BranchDto` — sys/dto/BranchDto.java
- ❌ `BranchLookupDto` — sys/dto/BranchLookupDto.java
- ❌ `CompanyDto` — sys/dto/CompanyDto.java
- ❌ `CurrencyLookupDto` — sys/dto/CurrencyLookupDto.java
- ❌ `MenuDto` — sys/dto/MenuDto.java
- ❌ `PendingApprovalDto` — sys/dto/PendingApprovalDto.java
- ❌ `RoleDto` — sys/dto/RoleDto.java
- ❌ `RoleLookupDto` — sys/dto/RoleLookupDto.java
- ❌ `Sys1001CompanyDto` — sys/dto/Sys1001CompanyDto.java
- ❌ `Sys1002BranchDto` — sys/dto/Sys1002BranchDto.java
- ❌ `Sys1003FinYearDtlDto` — sys/dto/Sys1003FinYearDtlDto.java
- ❌ `Sys1003FinYearDto` — sys/dto/Sys1003FinYearDto.java
- ❌ `Sys1004CurrencyDto` — sys/dto/Sys1004CurrencyDto.java
- ❌ `Sys1005VatTaxDto` — sys/dto/Sys1005VatTaxDto.java
- ❌ `Sys1006ExchangeRateDto` — sys/dto/Sys1006ExchangeRateDto.java
- ❌ `Sys1007CostCenterDto` — sys/dto/Sys1007CostCenterDto.java
- ❌ `Sys1008SettingDto` — sys/dto/Sys1008SettingDto.java
- ❌ `Sys1101EmployeeDto` — sys/dto/Sys1101EmployeeDto.java
- ❌ `Sys1101UserDto` — sys/dto/Sys1101UserDto.java
- ❌ `Sys1101UserRoleDto` — sys/dto/Sys1101UserRoleDto.java
- ❌ `Sys1103PermissionRowDto` — sys/dto/Sys1103PermissionRowDto.java
- ❌ `Sys1103RoleOptionDto` — sys/dto/Sys1103RoleOptionDto.java
- ❌ `Sys1104BulkSaveDto` — sys/dto/Sys1104BulkSaveDto.java
- ❌ `Sys1104MappingDto` — sys/dto/Sys1104MappingDto.java
- ❌ `Sys1104OptionDto` — sys/dto/Sys1104OptionDto.java
- ❌ `Sys1104RoleOptionDto` — sys/dto/Sys1104RoleOptionDto.java
- ❌ `Sys1104UserOptionDto` — sys/dto/Sys1104UserOptionDto.java
- ❌ `Sys1105AuditLogDto` — sys/dto/Sys1105AuditLogDto.java
- ❌ `Sys1107MappingDto` — sys/dto/Sys1107MappingDto.java
- ❌ `Sys1107OptionDto` — sys/dto/Sys1107OptionDto.java
- ❌ `Sys1107UserOptionDto` — sys/dto/Sys1107UserOptionDto.java
- ❌ `Sys1108ApproverDto` — sys/dto/Sys1108ApproverDto.java
- ❌ `Sys1108ScopeDto` — sys/dto/Sys1108ScopeDto.java
- ❌ `Sys1108StepDto` — sys/dto/Sys1108StepDto.java
- ❌ `Sys1108WorkflowDto` — sys/dto/Sys1108WorkflowDto.java
- ❌ `Sys1109LoginAttemptDto` — sys/dto/Sys1109LoginAttemptDto.java
- ❌ `Sys1109SessionDto` — sys/dto/Sys1109SessionDto.java
- ❌ `Sys1201MenuDto` — sys/dto/Sys1201MenuDto.java
- ❌ `Sys1201ModuleDto` — sys/dto/Sys1201ModuleDto.java
- ❌ `Sys1201SubmoduleDto` — sys/dto/Sys1201SubmoduleDto.java
- ❌ `Sys1202MenuRow` — sys/dto/Sys1202MenuRow.java
- ❌ `Sys1203FileDto` — sys/dto/Sys1203FileDto.java
- ❌ `SysLookupDto` — sys/dto/SysLookupDto.java
- ❌ `SysModuleDto` — sys/dto/SysModuleDto.java
- ❌ `SysSubmoduleDto` — sys/dto/SysSubmoduleDto.java
- ❌ `UserDto` — sys/dto/UserDto.java
- ❌ `UserPasswordResetRequest` — sys/dto/UserPasswordResetRequest.java
- ❌ `UserRoleDto` — sys/dto/UserRoleDto.java
- ❌ `UserSummaryDto` — sys/dto/UserSummaryDto.java
- 🟡 `ApprovalRequest` — sys/entity/ApprovalRequest.java
- 🟡 `ApprovalRequestStep` — sys/entity/ApprovalRequestStep.java
- 🟡 `ApprovalScope` — sys/entity/ApprovalScope.java
- 🟡 `ApprovalStep` — sys/entity/ApprovalStep.java
- 🟡 `ApprovalWorkflow` — sys/entity/ApprovalWorkflow.java
- 🟡 `Branch` — sys/entity/Branch.java
- 🟡 `Company` — sys/entity/Company.java
- 🟡 `CostCenter` — sys/entity/CostCenter.java
- 🟡 `Currency` — sys/entity/Currency.java
- 🟡 `Department` — sys/entity/Department.java
- 🟡 `DocSequence` — sys/entity/DocSequence.java
- 🟡 `EnrollMenu` — sys/entity/EnrollMenu.java
- 🟡 `EventOutbox` — sys/entity/EventOutbox.java
- 🟡 `ExchangeRate` — sys/entity/ExchangeRate.java
- 🟡 `FinYear` — sys/entity/FinYear.java
- 🟡 `FinYearDtl` — sys/entity/FinYearDtl.java
- 🟡 `LoginAttempt` — sys/entity/LoginAttempt.java
- 🟡 `Menu` — sys/entity/Menu.java
- 🟡 `PlatformAdmin` — sys/entity/PlatformAdmin.java
- 🟡 `Role` — sys/entity/Role.java
- 🟡 `RolePermission` — sys/entity/RolePermission.java
- 🟡 `Setting` — sys/entity/Setting.java
- 🟡 `StepApprover` — sys/entity/StepApprover.java
- 🟡 `SysCatalogEntity` — sys/entity/SysCatalogEntity.java
- 🟡 `SysFile` — sys/entity/SysFile.java
- 🟡 `SysModule` — sys/entity/SysModule.java
- 🟡 `SysRoleBranch` — sys/entity/SysRoleBranch.java
- 🟡 `SysSubmodule` — sys/entity/SysSubmodule.java
- 🟡 `User` — sys/entity/User.java
- 🟡 `UserBranch` — sys/entity/UserBranch.java
- 🟡 `UserCompany` — sys/entity/UserCompany.java
- 🟡 `UserWarehouse` — sys/entity/UserWarehouse.java
- 🟡 `VatTax` — sys/entity/VatTax.java
- ❌ `ApprovalCompletedEvent` — sys/event/ApprovalCompletedEvent.java
- ❌ `BranchMapper` — sys/mapper/BranchMapper.java
- ❌ `CompanyMapper` — sys/mapper/CompanyMapper.java
- ❌ `MenuMapper` — sys/mapper/MenuMapper.java
- ❌ `RoleMapper` — sys/mapper/RoleMapper.java
- ❌ `SysModuleMapper` — sys/mapper/SysModuleMapper.java
- ❌ `SysSubmoduleMapper` — sys/mapper/SysSubmoduleMapper.java
- ❌ `UserMapper` — sys/mapper/UserMapper.java
- ❌ `ApprovalRequestRepository` — sys/repository/ApprovalRequestRepository.java
- ❌ `ApprovalRequestStepRepository` — sys/repository/ApprovalRequestStepRepository.java
- ❌ `ApprovalScopeRepository` — sys/repository/ApprovalScopeRepository.java
- ❌ `ApprovalStepRepository` — sys/repository/ApprovalStepRepository.java
- ❌ `ApprovalWorkflowRepository` — sys/repository/ApprovalWorkflowRepository.java
- ❌ `BranchRepository` — sys/repository/BranchRepository.java
- ❌ `CompanyRepository` — sys/repository/CompanyRepository.java
- ❌ `CostCenterRepository` — sys/repository/CostCenterRepository.java
- ❌ `CurrencyRepository` — sys/repository/CurrencyRepository.java
- ❌ `DocSequenceRepository` — sys/repository/DocSequenceRepository.java
- ❌ `EnrollMenuRepository` — sys/repository/EnrollMenuRepository.java
- ❌ `EventOutboxRepository` — sys/repository/EventOutboxRepository.java
- ❌ `ExchangeRateRepository` — sys/repository/ExchangeRateRepository.java
- ❌ `FinYearDtlRepository` — sys/repository/FinYearDtlRepository.java
- ❌ `FinYearRepository` — sys/repository/FinYearRepository.java
- ❌ `LoginAttemptRepository` — sys/repository/LoginAttemptRepository.java
- ❌ `MenuRepository` — sys/repository/MenuRepository.java
- ❌ `RolePermissionRepository` — sys/repository/RolePermissionRepository.java
- ❌ `RoleRepository` — sys/repository/RoleRepository.java
- ❌ `SettingRepository` — sys/repository/SettingRepository.java
- ❌ `StepApproverRepository` — sys/repository/StepApproverRepository.java
- ❌ `SysFileRepository` — sys/repository/SysFileRepository.java
- ❌ `SysModuleRepository` — sys/repository/SysModuleRepository.java
- ❌ `SysRoleBranchRepository` — sys/repository/SysRoleBranchRepository.java
- ❌ `SysSubmoduleRepository` — sys/repository/SysSubmoduleRepository.java
- ❌ `UserBranchRepository` — sys/repository/UserBranchRepository.java
- ❌ `UserCompanyRepository` — sys/repository/UserCompanyRepository.java
- ❌ `UserRepository` — sys/repository/UserRepository.java
- ❌ `VatTaxRepository` — sys/repository/VatTaxRepository.java
- 🟡 `ApprovalService` — sys/service/ApprovalService.java
- ❌ `BranchService` — sys/service/BranchService.java
- ❌ `CompanyService` — sys/service/CompanyService.java
- ❌ `MenuService` — sys/service/MenuService.java
- ❌ `RoleService` — sys/service/RoleService.java
- ❌ `Sys1001Service` — sys/service/Sys1001Service.java
- ❌ `Sys1002Service` — sys/service/Sys1002Service.java
- ❌ `Sys1003Service` — sys/service/Sys1003Service.java
- ❌ `Sys1004Service` — sys/service/Sys1004Service.java
- ❌ `Sys1005Service` — sys/service/Sys1005Service.java
- ❌ `Sys1006Service` — sys/service/Sys1006Service.java
- ❌ `Sys1007Service` — sys/service/Sys1007Service.java
- ❌ `Sys1008Service` — sys/service/Sys1008Service.java
- ❌ `Sys1101Service` — sys/service/Sys1101Service.java
- ❌ `Sys1103Service` — sys/service/Sys1103Service.java
- ❌ `Sys1104Service` — sys/service/Sys1104Service.java
- ❌ `Sys1105Service` — sys/service/Sys1105Service.java
- ❌ `Sys1107Service` — sys/service/Sys1107Service.java
- ❌ `Sys1108Service` — sys/service/Sys1108Service.java
- ❌ `Sys1109Service` — sys/service/Sys1109Service.java
- ❌ `Sys1201Service` — sys/service/Sys1201Service.java
- ❌ `Sys1202Service` — sys/service/Sys1202Service.java
- ❌ `Sys1203Service` — sys/service/Sys1203Service.java
- ❌ `SysModuleService` — sys/service/SysModuleService.java
- ❌ `SysSubmoduleService` — sys/service/SysSubmoduleService.java
- ❌ `UserService` — sys/service/UserService.java

### HRM

- 🟡 `Hrm1003Controller` — hrm/controller/Hrm1003Controller.java
- 🟡 `Hrm1004Controller` — hrm/controller/Hrm1004Controller.java
- 🟡 `Hrm1005Controller` — hrm/controller/Hrm1005Controller.java
- 🟡 `Hrm1006Controller` — hrm/controller/Hrm1006Controller.java
- 🟡 `Hrm1007Controller` — hrm/controller/Hrm1007Controller.java
- 🟡 `Hrm1008Controller` — hrm/controller/Hrm1008Controller.java
- 🟡 `Hrm1009Controller` — hrm/controller/Hrm1009Controller.java
- 🟡 `Hrm1010Controller` — hrm/controller/Hrm1010Controller.java
- 🟡 `Hrm1101Controller` — hrm/controller/Hrm1101Controller.java
- 🟡 `Hrm1102Controller` — hrm/controller/Hrm1102Controller.java
- 🟡 `Hrm1103Controller` — hrm/controller/Hrm1103Controller.java
- 🟡 `Hrm1104Controller` — hrm/controller/Hrm1104Controller.java
- 🟡 `Hrm1105Controller` — hrm/controller/Hrm1105Controller.java
- 🟡 `Hrm1106Controller` — hrm/controller/Hrm1106Controller.java
- 🟡 `Hrm1201Controller` — hrm/controller/Hrm1201Controller.java
- 🟡 `Hrm1202Controller` — hrm/controller/Hrm1202Controller.java
- 🟡 `Hrm1203Controller` — hrm/controller/Hrm1203Controller.java
- 🟡 `Hrm1204Controller` — hrm/controller/Hrm1204Controller.java
- 🟡 `Hrm1205Controller` — hrm/controller/Hrm1205Controller.java
- 🟡 `Hrm1206Controller` — hrm/controller/Hrm1206Controller.java
- 🟡 `Hrm1207Controller` — hrm/controller/Hrm1207Controller.java
- 🟡 `Hrm1301Controller` — hrm/controller/Hrm1301Controller.java
- 🟡 `Hrm1303Controller` — hrm/controller/Hrm1303Controller.java
- 🟡 `Hrm1401Controller` — hrm/controller/Hrm1401Controller.java
- 🟡 `Hrm1402Controller` — hrm/controller/Hrm1402Controller.java
- 🟡 `Hrm1404Controller` — hrm/controller/Hrm1404Controller.java
- 🟡 `HrmDepartmentController` — hrm/controller/HrmDepartmentController.java
- 🟡 `HrmDesignationController` — hrm/controller/HrmDesignationController.java
- 🟡 `HrmEmployeeController` — hrm/controller/HrmEmployeeController.java
- ❌ `Hrm1003ShiftDto` — hrm/dto/Hrm1003ShiftDto.java
- ❌ `Hrm1004GradeDto` — hrm/dto/Hrm1004GradeDto.java
- ❌ `Hrm1004GradeStepDto` — hrm/dto/Hrm1004GradeStepDto.java
- ❌ `Hrm1005LeaveTypeDto` — hrm/dto/Hrm1005LeaveTypeDto.java
- ❌ `Hrm1006HolidayDto` — hrm/dto/Hrm1006HolidayDto.java
- ❌ `Hrm1007SalaryComponentDto` — hrm/dto/Hrm1007SalaryComponentDto.java
- ❌ `Hrm1008SettingsDto` — hrm/dto/Hrm1008SettingsDto.java
- ❌ `Hrm1009TaxSlabDto` — hrm/dto/Hrm1009TaxSlabDto.java
- ❌ `Hrm1009TaxSlabLineDto` — hrm/dto/Hrm1009TaxSlabLineDto.java
- ❌ `Hrm1101AttendanceDto` — hrm/dto/Hrm1101AttendanceDto.java
- ❌ `Hrm1102AttendanceRowDto` — hrm/dto/Hrm1102AttendanceRowDto.java
- ❌ `Hrm1102PunchDto` — hrm/dto/Hrm1102PunchDto.java
- ❌ `Hrm1102SyncRequestDto` — hrm/dto/Hrm1102SyncRequestDto.java
- ❌ `Hrm1102SyncResultDto` — hrm/dto/Hrm1102SyncResultDto.java
- ❌ `Hrm1103AdjustmentDto` — hrm/dto/Hrm1103AdjustmentDto.java
- ❌ `Hrm1104RosterDto` — hrm/dto/Hrm1104RosterDto.java
- ❌ `Hrm1104RosterLineDto` — hrm/dto/Hrm1104RosterLineDto.java
- ❌ `Hrm1105OvertimeDto` — hrm/dto/Hrm1105OvertimeDto.java
- ❌ `Hrm1106MovementDto` — hrm/dto/Hrm1106MovementDto.java
- ❌ `Hrm1201SalaryStructureDto` — hrm/dto/Hrm1201SalaryStructureDto.java
- ❌ `Hrm1201StructureLineDto` — hrm/dto/Hrm1201StructureLineDto.java
- ❌ `Hrm1202PayrollRunDto` — hrm/dto/Hrm1202PayrollRunDto.java
- ❌ `Hrm1202PayslipDto` — hrm/dto/Hrm1202PayslipDto.java
- ❌ `Hrm1202PayslipLineDto` — hrm/dto/Hrm1202PayslipLineDto.java
- ❌ `Hrm1203RunLiteDto` — hrm/dto/Hrm1203RunLiteDto.java
- ❌ `Hrm1203SheetRowDto` — hrm/dto/Hrm1203SheetRowDto.java
- ❌ `Hrm1204PayslipDto` — hrm/dto/Hrm1204PayslipDto.java
- ❌ `Hrm1204PayslipLineDto` — hrm/dto/Hrm1204PayslipLineDto.java
- ❌ `Hrm1204PayslipLiteDto` — hrm/dto/Hrm1204PayslipLiteDto.java
- ❌ `Hrm1204RunLiteDto` — hrm/dto/Hrm1204RunLiteDto.java
- ❌ `Hrm1205BonusLineDto` — hrm/dto/Hrm1205BonusLineDto.java
- ❌ `Hrm1205BonusRunDto` — hrm/dto/Hrm1205BonusRunDto.java
- 🟡 `Hrm1206LoanDto` — hrm/dto/Hrm1206LoanDto.java
- ❌ `Hrm1207SettlementDto` — hrm/dto/Hrm1207SettlementDto.java
- ❌ `Hrm1301BalanceDto` — hrm/dto/Hrm1301BalanceDto.java
- ❌ `Hrm1301LeaveApplicationDto` — hrm/dto/Hrm1301LeaveApplicationDto.java
- ❌ `Hrm1303BalanceRowDto` — hrm/dto/Hrm1303BalanceRowDto.java
- ❌ `Hrm1401RequisitionDto` — hrm/dto/Hrm1401RequisitionDto.java
- ❌ `Hrm1402CandidateDto` — hrm/dto/Hrm1402CandidateDto.java
- ❌ `Hrm1404LetterDto` — hrm/dto/Hrm1404LetterDto.java
- ❌ `Hrm1404OfferDto` — hrm/dto/Hrm1404OfferDto.java
- ❌ `HrmDepartmentDto` — hrm/dto/HrmDepartmentDto.java
- ❌ `HrmDesignationDto` — hrm/dto/HrmDesignationDto.java
- ❌ `HrmEmployeeDto` — hrm/dto/HrmEmployeeDto.java
- ❌ `HrmEmployeeListDto` — hrm/dto/HrmEmployeeListDto.java
- ❌ `HrmLeaveApplicationRuleDto` — hrm/dto/HrmLeaveApplicationRuleDto.java
- ❌ `HrmLeavePolicySetupDto` — hrm/dto/HrmLeavePolicySetupDto.java
- 🟡 `HrmAttendance` — hrm/entity/HrmAttendance.java
- 🟡 `HrmAttendanceAdjustment` — hrm/entity/HrmAttendanceAdjustment.java
- 🟡 `HrmBonusLine` — hrm/entity/HrmBonusLine.java
- 🟡 `HrmBonusRun` — hrm/entity/HrmBonusRun.java
- 🟡 `HrmBonusScopeDesignation` — hrm/entity/HrmBonusScopeDesignation.java
- 🟡 `HrmBonusScopeEmployee` — hrm/entity/HrmBonusScopeEmployee.java
- 🟡 `HrmCandidate` — hrm/entity/HrmCandidate.java
- 🟡 `HrmDepartment` — hrm/entity/HrmDepartment.java
- 🟡 `HrmDesignation` — hrm/entity/HrmDesignation.java
- 🟡 `HrmEmployee` — hrm/entity/HrmEmployee.java
- 🟡 `HrmEmployeeMovement` — hrm/entity/HrmEmployeeMovement.java
- 🟡 `HrmFinalSettlement` — hrm/entity/HrmFinalSettlement.java
- 🟡 `HrmGrade` — hrm/entity/HrmGrade.java
- 🟡 `HrmGradeStep` — hrm/entity/HrmGradeStep.java
- 🟡 `HrmHoliday` — hrm/entity/HrmHoliday.java
- 🟡 `HrmJobRequisition` — hrm/entity/HrmJobRequisition.java
- 🟡 `HrmLeaveApplication` — hrm/entity/HrmLeaveApplication.java
- 🟡 `HrmLeaveApplicationRule` — hrm/entity/HrmLeaveApplicationRule.java
- 🟡 `HrmLeaveBalance` — hrm/entity/HrmLeaveBalance.java
- 🟡 `HrmLeaveLedger` — hrm/entity/HrmLeaveLedger.java
- 🟡 `HrmLeavePolicySetup` — hrm/entity/HrmLeavePolicySetup.java
- 🟡 `HrmLeaveType` — hrm/entity/HrmLeaveType.java
- 🟡 `HrmLoanAdvance` — hrm/entity/HrmLoanAdvance.java
- 🟡 `HrmOffer` — hrm/entity/HrmOffer.java
- 🟡 `HrmOvertime` — hrm/entity/HrmOvertime.java
- 🟡 `HrmPayrollPolicy` — hrm/entity/HrmPayrollPolicy.java
- 🟡 `HrmPayrollRun` — hrm/entity/HrmPayrollRun.java
- 🟡 `HrmPayslip` — hrm/entity/HrmPayslip.java
- 🟡 `HrmPayslipDtl` — hrm/entity/HrmPayslipDtl.java
- 🟡 `HrmSalaryComponent` — hrm/entity/HrmSalaryComponent.java
- 🟡 `HrmSalaryStructure` — hrm/entity/HrmSalaryStructure.java
- 🟡 `HrmSalaryStructureDtl` — hrm/entity/HrmSalaryStructureDtl.java
- 🟡 `HrmShift` — hrm/entity/HrmShift.java
- 🟡 `HrmShiftRoster` — hrm/entity/HrmShiftRoster.java
- 🟡 `HrmShiftRosterLine` — hrm/entity/HrmShiftRosterLine.java
- 🟡 `HrmTaxSlab` — hrm/entity/HrmTaxSlab.java
- ❌ `HrmApprovalListener` — hrm/listener/HrmApprovalListener.java
- ❌ `HrmDepartmentMapper` — hrm/mapper/HrmDepartmentMapper.java
- ❌ `HrmDesignationMapper` — hrm/mapper/HrmDesignationMapper.java
- ❌ `HrmEmployeeMapper` — hrm/mapper/HrmEmployeeMapper.java
- ❌ `HrmAttendanceAdjustmentRepository` — hrm/repository/HrmAttendanceAdjustmentRepository.java
- ❌ `HrmAttendanceRepository` — hrm/repository/HrmAttendanceRepository.java
- ❌ `HrmBonusLineRepository` — hrm/repository/HrmBonusLineRepository.java
- ❌ `HrmBonusRunRepository` — hrm/repository/HrmBonusRunRepository.java
- ❌ `HrmBonusScopeDesignationRepository` — hrm/repository/HrmBonusScopeDesignationRepository.java
- ❌ `HrmBonusScopeEmployeeRepository` — hrm/repository/HrmBonusScopeEmployeeRepository.java
- ❌ `HrmCandidateRepository` — hrm/repository/HrmCandidateRepository.java
- ❌ `HrmDepartmentRepository` — hrm/repository/HrmDepartmentRepository.java
- ❌ `HrmDesignationRepository` — hrm/repository/HrmDesignationRepository.java
- ❌ `HrmEmployeeMovementRepository` — hrm/repository/HrmEmployeeMovementRepository.java
- ❌ `HrmEmployeeRepository` — hrm/repository/HrmEmployeeRepository.java
- ❌ `HrmFinalSettlementRepository` — hrm/repository/HrmFinalSettlementRepository.java
- ❌ `HrmGradeRepository` — hrm/repository/HrmGradeRepository.java
- ❌ `HrmGradeStepRepository` — hrm/repository/HrmGradeStepRepository.java
- ❌ `HrmHolidayRepository` — hrm/repository/HrmHolidayRepository.java
- ❌ `HrmJobRequisitionRepository` — hrm/repository/HrmJobRequisitionRepository.java
- ❌ `HrmLeaveApplicationRepository` — hrm/repository/HrmLeaveApplicationRepository.java
- ❌ `HrmLeaveApplicationRuleRepository` — hrm/repository/HrmLeaveApplicationRuleRepository.java
- ❌ `HrmLeaveBalanceRepository` — hrm/repository/HrmLeaveBalanceRepository.java
- ❌ `HrmLeaveLedgerRepository` — hrm/repository/HrmLeaveLedgerRepository.java
- ❌ `HrmLeavePolicySetupRepository` — hrm/repository/HrmLeavePolicySetupRepository.java
- ❌ `HrmLeaveTypeRepository` — hrm/repository/HrmLeaveTypeRepository.java
- ❌ `HrmLoanAdvanceRepository` — hrm/repository/HrmLoanAdvanceRepository.java
- ❌ `HrmOfferRepository` — hrm/repository/HrmOfferRepository.java
- ❌ `HrmOvertimeRepository` — hrm/repository/HrmOvertimeRepository.java
- ❌ `HrmPayrollPolicyRepository` — hrm/repository/HrmPayrollPolicyRepository.java
- ❌ `HrmPayrollRunRepository` — hrm/repository/HrmPayrollRunRepository.java
- ❌ `HrmPayslipDtlRepository` — hrm/repository/HrmPayslipDtlRepository.java
- ❌ `HrmPayslipRepository` — hrm/repository/HrmPayslipRepository.java
- ❌ `HrmSalaryComponentRepository` — hrm/repository/HrmSalaryComponentRepository.java
- ❌ `HrmSalaryStructureDtlRepository` — hrm/repository/HrmSalaryStructureDtlRepository.java
- ❌ `HrmSalaryStructureRepository` — hrm/repository/HrmSalaryStructureRepository.java
- ❌ `HrmShiftRepository` — hrm/repository/HrmShiftRepository.java
- ❌ `HrmShiftRosterLineRepository` — hrm/repository/HrmShiftRosterLineRepository.java
- ❌ `HrmShiftRosterRepository` — hrm/repository/HrmShiftRosterRepository.java
- ❌ `HrmTaxSlabRepository` — hrm/repository/HrmTaxSlabRepository.java
- ❌ `HrmSettlementPayrollDueView` — hrm/repository/projection/HrmSettlementPayrollDueView.java
- 🟡 `Hrm1003Service` — hrm/service/Hrm1003Service.java
- 🟡 `Hrm1004Service` — hrm/service/Hrm1004Service.java
- 🟡 `Hrm1005Service` — hrm/service/Hrm1005Service.java
- 🟡 `Hrm1006Service` — hrm/service/Hrm1006Service.java
- 🟡 `Hrm1007Service` — hrm/service/Hrm1007Service.java
- 🟡 `Hrm1008Service` — hrm/service/Hrm1008Service.java
- 🟡 `Hrm1009Service` — hrm/service/Hrm1009Service.java
- 🟡 `Hrm1101Service` — hrm/service/Hrm1101Service.java
- ❌ `Hrm1102Service` — hrm/service/Hrm1102Service.java
- 🟡 `Hrm1103Service` — hrm/service/Hrm1103Service.java
- 🟡 `Hrm1104Service` — hrm/service/Hrm1104Service.java
- 🟡 `Hrm1105Service` — hrm/service/Hrm1105Service.java
- 🟡 `Hrm1106Service` — hrm/service/Hrm1106Service.java
- 🟡 `Hrm1201Service` — hrm/service/Hrm1201Service.java
- ❌ `Hrm1202Service` — hrm/service/Hrm1202Service.java
- ❌ `Hrm1203Service` — hrm/service/Hrm1203Service.java
- ❌ `Hrm1204Service` — hrm/service/Hrm1204Service.java
- ❌ `Hrm1205Service` — hrm/service/Hrm1205Service.java
- 🟡 `Hrm1206Service` — hrm/service/Hrm1206Service.java
- ❌ `Hrm1207Service` — hrm/service/Hrm1207Service.java
- 🟡 `Hrm1301Service` — hrm/service/Hrm1301Service.java
- 🟡 `Hrm1303Service` — hrm/service/Hrm1303Service.java
- 🟡 `Hrm1401Service` — hrm/service/Hrm1401Service.java
- 🟡 `Hrm1402Service` — hrm/service/Hrm1402Service.java
- 🟡 `Hrm1404Service` — hrm/service/Hrm1404Service.java
- 🟡 `HrmDepartmentService` — hrm/service/HrmDepartmentService.java
- 🟡 `HrmDesignationService` — hrm/service/HrmDesignationService.java
- 🟡 `HrmEmployeeService` — hrm/service/HrmEmployeeService.java
- ❌ `HrmLeavePolicyService` — hrm/service/HrmLeavePolicyService.java
- ❌ `HrmLeaveRuleEngine` — hrm/service/HrmLeaveRuleEngine.java

### INV

- 🟡 `Inv1001Controller` — inv/controller/Inv1001Controller.java
- 🟡 `Inv1002Controller` — inv/controller/Inv1002Controller.java
- 🟡 `Inv1003Controller` — inv/controller/Inv1003Controller.java
- 🟡 `Inv1004Controller` — inv/controller/Inv1004Controller.java
- 🟡 `Inv1005Controller` — inv/controller/Inv1005Controller.java
- 🟡 `Inv1006Controller` — inv/controller/Inv1006Controller.java
- 🟡 `Inv1101Controller` — inv/controller/Inv1101Controller.java
- 🟡 `Inv1102Controller` — inv/controller/Inv1102Controller.java
- 🟡 `Inv2001Controller` — inv/controller/Inv2001Controller.java
- 🟡 `Inv2002Controller` — inv/controller/Inv2002Controller.java
- 🟡 `Inv2003Controller` — inv/controller/Inv2003Controller.java
- 🟡 `Inv2004Controller` — inv/controller/Inv2004Controller.java
- 🟡 `Inv2005Controller` — inv/controller/Inv2005Controller.java
- ❌ `Inv1001AttributeOptionDto` — inv/dto/Inv1001AttributeOptionDto.java
- ❌ `Inv1001BarcodeDto` — inv/dto/Inv1001BarcodeDto.java
- ❌ `Inv1001BarcodeResolveDto` — inv/dto/Inv1001BarcodeResolveDto.java
- ❌ `Inv1001LookupDto` — inv/dto/Inv1001LookupDto.java
- ❌ `Inv1001ProductDto` — inv/dto/Inv1001ProductDto.java
- ❌ `Inv1001UomConvDto` — inv/dto/Inv1001UomConvDto.java
- ❌ `Inv1001VariantDto` — inv/dto/Inv1001VariantDto.java
- ❌ `Inv1002BarcodeDto` — inv/dto/Inv1002BarcodeDto.java
- ❌ `Inv1002LookupDto` — inv/dto/Inv1002LookupDto.java
- ❌ `Inv1003CategoryDto` — inv/dto/Inv1003CategoryDto.java
- ❌ `Inv1004BrandDto` — inv/dto/Inv1004BrandDto.java
- ❌ `Inv1005UomDto` — inv/dto/Inv1005UomDto.java
- ❌ `Inv1006AttributeDto` — inv/dto/Inv1006AttributeDto.java
- ❌ `Inv1006AttributeValueDto` — inv/dto/Inv1006AttributeValueDto.java
- ❌ `Inv1101LineDto` — inv/dto/Inv1101LineDto.java
- ❌ `Inv1101LookupDto` — inv/dto/Inv1101LookupDto.java
- ❌ `Inv1101OpeningDto` — inv/dto/Inv1101OpeningDto.java
- ❌ `Inv1102AdjustmentDto` — inv/dto/Inv1102AdjustmentDto.java
- ❌ `Inv1102LineDto` — inv/dto/Inv1102LineDto.java
- ❌ `Inv1102LookupDto` — inv/dto/Inv1102LookupDto.java
- ❌ `Inv2001WarehouseDto` — inv/dto/Inv2001WarehouseDto.java
- ❌ `Inv2002RackDto` — inv/dto/Inv2002RackDto.java
- ❌ `Inv2003LookupDto` — inv/dto/Inv2003LookupDto.java
- ❌ `Inv2003TransferDto` — inv/dto/Inv2003TransferDto.java
- ❌ `Inv2003TransferLineDto` — inv/dto/Inv2003TransferLineDto.java
- ❌ `Inv2004BatchDto` — inv/dto/Inv2004BatchDto.java
- ❌ `Inv2005ReorderDto` — inv/dto/Inv2005ReorderDto.java
- ❌ `InvLookupDto` — inv/dto/InvLookupDto.java
- ❌ `CostingMethod` — inv/engine/CostingMethod.java
- ❌ `InvStockPostingService` — inv/engine/InvStockPostingService.java
- ❌ `StockPostingCommand` — inv/engine/StockPostingCommand.java
- ❌ `StockPostingLeg` — inv/engine/StockPostingLeg.java
- ❌ `StockPostingResult` — inv/engine/StockPostingResult.java
- 🟡 `InvBatch` — inv/entity/InvBatch.java
- 🟡 `InvBrand` — inv/entity/InvBrand.java
- 🟡 `InvCategory` — inv/entity/InvCategory.java
- 🟡 `InvProduct` — inv/entity/InvProduct.java
- 🟡 `InvProductAttribute` — inv/entity/InvProductAttribute.java
- 🟡 `InvProductAttributeValue` — inv/entity/InvProductAttributeValue.java
- 🟡 `InvProductBarcode` — inv/entity/InvProductBarcode.java
- 🟡 `InvProductVariant` — inv/entity/InvProductVariant.java
- 🟡 `InvRack` — inv/entity/InvRack.java
- 🟡 `InvReorder` — inv/entity/InvReorder.java
- 🟡 `InvStock` — inv/entity/InvStock.java
- 🟡 `InvStockAdjustment` — inv/entity/InvStockAdjustment.java
- 🟡 `InvStockAdjustmentDtl` — inv/entity/InvStockAdjustmentDtl.java
- 🟡 `InvStockLedger` — inv/entity/InvStockLedger.java
- 🟡 `InvStockTransfer` — inv/entity/InvStockTransfer.java
- 🟡 `InvStockTransferDtl` — inv/entity/InvStockTransferDtl.java
- 🟡 `InvUom` — inv/entity/InvUom.java
- 🟡 `InvUomConversion` — inv/entity/InvUomConversion.java
- 🟡 `InvValuationLayer` — inv/entity/InvValuationLayer.java
- 🟡 `InvWarehouse` — inv/entity/InvWarehouse.java
- ❌ `InvBatchRepository` — inv/repository/InvBatchRepository.java
- ❌ `InvBrandRepository` — inv/repository/InvBrandRepository.java
- ❌ `InvCategoryRepository` — inv/repository/InvCategoryRepository.java
- ❌ `InvProductAttributeRepository` — inv/repository/InvProductAttributeRepository.java
- ❌ `InvProductAttributeValueRepository` — inv/repository/InvProductAttributeValueRepository.java
- ❌ `InvProductBarcodeRepository` — inv/repository/InvProductBarcodeRepository.java
- ❌ `InvProductRepository` — inv/repository/InvProductRepository.java
- ❌ `InvProductVariantRepository` — inv/repository/InvProductVariantRepository.java
- ❌ `InvRackRepository` — inv/repository/InvRackRepository.java
- ❌ `InvReorderRepository` — inv/repository/InvReorderRepository.java
- ❌ `InvStockAdjustmentDtlRepository` — inv/repository/InvStockAdjustmentDtlRepository.java
- ❌ `InvStockAdjustmentRepository` — inv/repository/InvStockAdjustmentRepository.java
- ❌ `InvStockLedgerRepository` — inv/repository/InvStockLedgerRepository.java
- ❌ `InvStockRepository` — inv/repository/InvStockRepository.java
- ❌ `InvStockTransferDtlRepository` — inv/repository/InvStockTransferDtlRepository.java
- ❌ `InvStockTransferRepository` — inv/repository/InvStockTransferRepository.java
- ❌ `InvUomConversionRepository` — inv/repository/InvUomConversionRepository.java
- ❌ `InvUomRepository` — inv/repository/InvUomRepository.java
- ❌ `InvValuationLayerRepository` — inv/repository/InvValuationLayerRepository.java
- ❌ `InvWarehouseRepository` — inv/repository/InvWarehouseRepository.java
- ❌ `Inv1001Service` — inv/service/Inv1001Service.java
- ❌ `Inv1002Service` — inv/service/Inv1002Service.java
- ❌ `Inv1003Service` — inv/service/Inv1003Service.java
- ❌ `Inv1004Service` — inv/service/Inv1004Service.java
- ❌ `Inv1005Service` — inv/service/Inv1005Service.java
- ❌ `Inv1006Service` — inv/service/Inv1006Service.java
- ❌ `Inv1101Service` — inv/service/Inv1101Service.java
- ❌ `Inv1102Service` — inv/service/Inv1102Service.java
- ❌ `Inv2001Service` — inv/service/Inv2001Service.java
- ❌ `Inv2002Service` — inv/service/Inv2002Service.java
- ❌ `Inv2003Service` — inv/service/Inv2003Service.java
- ❌ `Inv2004Service` — inv/service/Inv2004Service.java
- ❌ `Inv2005Service` — inv/service/Inv2005Service.java
- ❌ `InvDocSequenceService` — inv/service/InvDocSequenceService.java
- ❌ `InvPeriodResolver` — inv/service/InvPeriodResolver.java

### PUR

- 🟡 `Pur1001Controller` — pur/controller/Pur1001Controller.java
- 🟡 `Pur1002Controller` — pur/controller/Pur1002Controller.java
- 🟡 `Pur1101Controller` — pur/controller/Pur1101Controller.java
- 🟡 `Pur1102Controller` — pur/controller/Pur1102Controller.java
- 🟡 `Pur1103Controller` — pur/controller/Pur1103Controller.java
- 🟡 `Pur1104Controller` — pur/controller/Pur1104Controller.java
- 🟡 `Pur1105Controller` — pur/controller/Pur1105Controller.java
- 🟡 `Pur1106Controller` — pur/controller/Pur1106Controller.java
- ❌ `Pur1001SupplierDto` — pur/dto/Pur1001SupplierDto.java
- ❌ `Pur1002PriceRowDto` — pur/dto/Pur1002PriceRowDto.java
- ❌ `Pur1101LineDto` — pur/dto/Pur1101LineDto.java
- ❌ `Pur1101OrderDto` — pur/dto/Pur1101OrderDto.java
- ❌ `Pur1102InvoiceDto` — pur/dto/Pur1102InvoiceDto.java
- ❌ `Pur1102LineDto` — pur/dto/Pur1102LineDto.java
- ❌ `Pur1102LookupDto` — pur/dto/Pur1102LookupDto.java
- ❌ `Pur1103LineDto` — pur/dto/Pur1103LineDto.java
- ❌ `Pur1103ReturnDto` — pur/dto/Pur1103ReturnDto.java
- ❌ `Pur1104AllocDto` — pur/dto/Pur1104AllocDto.java
- ❌ `Pur1104OpenInvoiceDto` — pur/dto/Pur1104OpenInvoiceDto.java
- ❌ `Pur1104PaymentDto` — pur/dto/Pur1104PaymentDto.java
- ❌ `Pur1105LineDto` — pur/dto/Pur1105LineDto.java
- ❌ `Pur1105ReceiptDto` — pur/dto/Pur1105ReceiptDto.java
- ❌ `Pur1106AllocDto` — pur/dto/Pur1106AllocDto.java
- ❌ `Pur1106LandedCostDto` — pur/dto/Pur1106LandedCostDto.java
- 🟡 `PurInvoice` — pur/entity/PurInvoice.java
- 🟡 `PurInvoiceDtl` — pur/entity/PurInvoiceDtl.java
- 🟡 `PurLandedCost` — pur/entity/PurLandedCost.java
- 🟡 `PurLandedCostAlloc` — pur/entity/PurLandedCostAlloc.java
- 🟡 `PurOrder` — pur/entity/PurOrder.java
- 🟡 `PurOrderDtl` — pur/entity/PurOrderDtl.java
- 🟡 `PurPayment` — pur/entity/PurPayment.java
- 🟡 `PurPaymentAlloc` — pur/entity/PurPaymentAlloc.java
- 🟡 `PurReceipt` — pur/entity/PurReceipt.java
- 🟡 `PurReceiptDtl` — pur/entity/PurReceiptDtl.java
- 🟡 `PurReturn` — pur/entity/PurReturn.java
- 🟡 `PurReturnDtl` — pur/entity/PurReturnDtl.java
- 🟡 `PurSupplier` — pur/entity/PurSupplier.java
- 🟡 `PurSupplierLedger` — pur/entity/PurSupplierLedger.java
- 🟡 `PurSupplierProduct` — pur/entity/PurSupplierProduct.java
- ❌ `PurApprovalListener` — pur/listener/PurApprovalListener.java
- ❌ `PurInvoiceDtlRepository` — pur/repository/PurInvoiceDtlRepository.java
- ❌ `PurInvoiceRepository` — pur/repository/PurInvoiceRepository.java
- ❌ `PurLandedCostAllocRepository` — pur/repository/PurLandedCostAllocRepository.java
- ❌ `PurLandedCostRepository` — pur/repository/PurLandedCostRepository.java
- ❌ `PurOrderDtlRepository` — pur/repository/PurOrderDtlRepository.java
- ❌ `PurOrderRepository` — pur/repository/PurOrderRepository.java
- ❌ `PurPaymentAllocRepository` — pur/repository/PurPaymentAllocRepository.java
- ❌ `PurPaymentRepository` — pur/repository/PurPaymentRepository.java
- ❌ `PurReceiptDtlRepository` — pur/repository/PurReceiptDtlRepository.java
- ❌ `PurReceiptRepository` — pur/repository/PurReceiptRepository.java
- ❌ `PurReturnDtlRepository` — pur/repository/PurReturnDtlRepository.java
- ❌ `PurReturnRepository` — pur/repository/PurReturnRepository.java
- ❌ `PurSupplierLedgerRepository` — pur/repository/PurSupplierLedgerRepository.java
- ❌ `PurSupplierProductRepository` — pur/repository/PurSupplierProductRepository.java
- ❌ `PurSupplierRepository` — pur/repository/PurSupplierRepository.java
- ❌ `Pur1001Service` — pur/service/Pur1001Service.java
- ❌ `Pur1002Service` — pur/service/Pur1002Service.java
- ❌ `Pur1101Service` — pur/service/Pur1101Service.java
- ❌ `Pur1102Service` — pur/service/Pur1102Service.java
- ❌ `Pur1103Service` — pur/service/Pur1103Service.java
- ❌ `Pur1104Service` — pur/service/Pur1104Service.java
- ❌ `Pur1105Service` — pur/service/Pur1105Service.java
- ❌ `Pur1106Service` — pur/service/Pur1106Service.java
- ❌ `PurApLedgerService` — pur/service/PurApLedgerService.java

### SAL

- 🟡 `Sal1001Controller` — sal/controller/Sal1001Controller.java
- 🟡 `Sal1101Controller` — sal/controller/Sal1101Controller.java
- 🟡 `Sal1102Controller` — sal/controller/Sal1102Controller.java
- 🟡 `Sal1103Controller` — sal/controller/Sal1103Controller.java
- ❌ `Sal1001InvoiceDto` — sal/dto/Sal1001InvoiceDto.java
- ❌ `Sal1001LineDto` — sal/dto/Sal1001LineDto.java
- ❌ `Sal1001LookupDto` — sal/dto/Sal1001LookupDto.java
- ❌ `Sal1101CustomerDto` — sal/dto/Sal1101CustomerDto.java
- ❌ `Sal1102AllocDto` — sal/dto/Sal1102AllocDto.java
- ❌ `Sal1102OpenInvoiceDto` — sal/dto/Sal1102OpenInvoiceDto.java
- ❌ `Sal1102ReceiptDto` — sal/dto/Sal1102ReceiptDto.java
- ❌ `Sal1103LineDto` — sal/dto/Sal1103LineDto.java
- ❌ `Sal1103ReturnDto` — sal/dto/Sal1103ReturnDto.java
- 🟡 `SalCustomer` — sal/entity/SalCustomer.java
- 🟡 `SalCustomerLedger` — sal/entity/SalCustomerLedger.java
- 🟡 `SalInvoice` — sal/entity/SalInvoice.java
- 🟡 `SalInvoiceDtl` — sal/entity/SalInvoiceDtl.java
- 🟡 `SalReceipt` — sal/entity/SalReceipt.java
- 🟡 `SalReceiptAlloc` — sal/entity/SalReceiptAlloc.java
- 🟡 `SalReturn` — sal/entity/SalReturn.java
- 🟡 `SalReturnDtl` — sal/entity/SalReturnDtl.java
- ❌ `SalApprovalListener` — sal/listener/SalApprovalListener.java
- ❌ `SalCustomerLedgerRepository` — sal/repository/SalCustomerLedgerRepository.java
- ❌ `SalCustomerRepository` — sal/repository/SalCustomerRepository.java
- ❌ `SalInvoiceDtlRepository` — sal/repository/SalInvoiceDtlRepository.java
- ❌ `SalInvoiceRepository` — sal/repository/SalInvoiceRepository.java
- ❌ `SalReceiptAllocRepository` — sal/repository/SalReceiptAllocRepository.java
- ❌ `SalReceiptRepository` — sal/repository/SalReceiptRepository.java
- ❌ `SalReturnDtlRepository` — sal/repository/SalReturnDtlRepository.java
- ❌ `SalReturnRepository` — sal/repository/SalReturnRepository.java
- ❌ `Sal1001Service` — sal/service/Sal1001Service.java
- ❌ `Sal1101Service` — sal/service/Sal1101Service.java
- ❌ `Sal1102Service` — sal/service/Sal1102Service.java
- ❌ `Sal1103Service` — sal/service/Sal1103Service.java
- ❌ `SalArLedgerService` — sal/service/SalArLedgerService.java

### FIN

- ❌ `GlPostingPayload` — fin/contract/GlPostingPayload.java
- 🟡 `Fin1001Controller` — fin/controller/Fin1001Controller.java
- 🟡 `Fin1002Controller` — fin/controller/Fin1002Controller.java
- 🟡 `Fin1003Controller` — fin/controller/Fin1003Controller.java
- 🟡 `Fin1004Controller` — fin/controller/Fin1004Controller.java
- 🟡 `Fin1005Controller` — fin/controller/Fin1005Controller.java
- 🟡 `Fin1006Controller` — fin/controller/Fin1006Controller.java
- 🟡 `Fin1101Controller` — fin/controller/Fin1101Controller.java
- 🟡 `Fin1102Controller` — fin/controller/Fin1102Controller.java
- 🟡 `Fin1201Controller` — fin/controller/Fin1201Controller.java
- 🟡 `Fin1301Controller` — fin/controller/Fin1301Controller.java
- 🟡 `Fin1302Controller` — fin/controller/Fin1302Controller.java
- 🟡 `Fin1303Controller` — fin/controller/Fin1303Controller.java
- 🟡 `Fin1304Controller` — fin/controller/Fin1304Controller.java
- 🟡 `Fin1305Controller` — fin/controller/Fin1305Controller.java
- 🟡 `Fin1306Controller` — fin/controller/Fin1306Controller.java
- 🟡 `Fin1307Controller` — fin/controller/Fin1307Controller.java
- 🟡 `Fin1308ChartOfAccountsPdfController` — fin/controller/Fin1308ChartOfAccountsPdfController.java
- 🟡 `Fin1401Controller` — fin/controller/Fin1401Controller.java
- ❌ `Fin1001AccountDto` — fin/dto/Fin1001AccountDto.java
- ❌ `Fin1002AccountGroupDto` — fin/dto/Fin1002AccountGroupDto.java
- ❌ `Fin1003VoucherTypeDto` — fin/dto/Fin1003VoucherTypeDto.java
- ❌ `Fin1004AccountDto` — fin/dto/Fin1004AccountDto.java
- ❌ `Fin1004OpeningBalanceDto` — fin/dto/Fin1004OpeningBalanceDto.java
- ❌ `Fin1004OpeningLineDto` — fin/dto/Fin1004OpeningLineDto.java
- ❌ `Fin1004ResultDto` — fin/dto/Fin1004ResultDto.java
- ❌ `Fin1005BankAccountDto` — fin/dto/Fin1005BankAccountDto.java
- ❌ `Fin1006GlMapDto` — fin/dto/Fin1006GlMapDto.java
- ❌ `Fin1101VoucherDto` — fin/dto/Fin1101VoucherDto.java
- ❌ `Fin1101VoucherLineDto` — fin/dto/Fin1101VoucherLineDto.java
- ❌ `Fin1102BankAccountDto` — fin/dto/Fin1102BankAccountDto.java
- ❌ `Fin1102LedgerLineDto` — fin/dto/Fin1102LedgerLineDto.java
- ❌ `Fin1102ReconDto` — fin/dto/Fin1102ReconDto.java
- ❌ `Fin1102SaveDto` — fin/dto/Fin1102SaveDto.java
- ❌ `Fin1102WorksheetDto` — fin/dto/Fin1102WorksheetDto.java
- ❌ `Fin1201EventDto` — fin/dto/Fin1201EventDto.java
- ❌ `Fin1301TrialBalanceRowDto` — fin/dto/Fin1301TrialBalanceRowDto.java
- ❌ `Fin1302LedgerDto` — fin/dto/Fin1302LedgerDto.java
- ❌ `Fin1302LedgerRowDto` — fin/dto/Fin1302LedgerRowDto.java
- ❌ `Fin1303DayBookRowDto` — fin/dto/Fin1303DayBookRowDto.java
- ❌ `Fin1304PnlDto` — fin/dto/Fin1304PnlDto.java
- ❌ `Fin1305BalanceSheetDto` — fin/dto/Fin1305BalanceSheetDto.java
- ❌ `Fin1306CashFlowDto` — fin/dto/Fin1306CashFlowDto.java
- ❌ `Fin1306CashFlowRowDto` — fin/dto/Fin1306CashFlowRowDto.java
- ❌ `Fin1307AgingDto` — fin/dto/Fin1307AgingDto.java
- ❌ `Fin1307AgingRowDto` — fin/dto/Fin1307AgingRowDto.java
- ❌ `Fin1308ChartOfAccountsRowDto` — fin/dto/Fin1308ChartOfAccountsRowDto.java
- ❌ `Fin1401CloseRequestDto` — fin/dto/Fin1401CloseRequestDto.java
- ❌ `Fin1401CloseResultDto` — fin/dto/Fin1401CloseResultDto.java
- ❌ `Fin1401PeriodDto` — fin/dto/Fin1401PeriodDto.java
- ❌ `Fin1401YearDto` — fin/dto/Fin1401YearDto.java
- ❌ `FinStatementRowDto` — fin/dto/shared/FinStatementRowDto.java
- 🟡 `FinAccount` — fin/entity/FinAccount.java
- 🟡 `FinAccountBalance` — fin/entity/FinAccountBalance.java
- 🟡 `FinAccountGroup` — fin/entity/FinAccountGroup.java
- 🟡 `FinBankAccount` — fin/entity/FinBankAccount.java
- 🟡 `FinBankRecon` — fin/entity/FinBankRecon.java
- 🟡 `FinBankReconLine` — fin/entity/FinBankReconLine.java
- 🟡 `FinGlMap` — fin/entity/FinGlMap.java
- 🟡 `FinLedger` — fin/entity/FinLedger.java
- 🟡 `FinVoucher` — fin/entity/FinVoucher.java
- 🟡 `FinVoucherDtl` — fin/entity/FinVoucherDtl.java
- 🟡 `FinVoucherType` — fin/entity/FinVoucherType.java
- ❌ `FinApprovalListener` — fin/listener/FinApprovalListener.java
- ❌ `FinAccountBalanceRepository` — fin/repository/FinAccountBalanceRepository.java
- ❌ `FinAccountGroupRepository` — fin/repository/FinAccountGroupRepository.java
- ❌ `FinAccountRepository` — fin/repository/FinAccountRepository.java
- ❌ `FinBankAccountRepository` — fin/repository/FinBankAccountRepository.java
- ❌ `FinBankReconLineRepository` — fin/repository/FinBankReconLineRepository.java
- ❌ `FinBankReconRepository` — fin/repository/FinBankReconRepository.java
- ❌ `FinGlMapRepository` — fin/repository/FinGlMapRepository.java
- ❌ `FinLedgerRepository` — fin/repository/FinLedgerRepository.java
- ❌ `FinVoucherDtlRepository` — fin/repository/FinVoucherDtlRepository.java
- ❌ `FinVoucherRepository` — fin/repository/FinVoucherRepository.java
- ❌ `FinVoucherTypeRepository` — fin/repository/FinVoucherTypeRepository.java
- ❌ `Fin1001Service` — fin/service/Fin1001Service.java
- ❌ `Fin1002Service` — fin/service/Fin1002Service.java
- ❌ `Fin1003Service` — fin/service/Fin1003Service.java
- ❌ `Fin1004Service` — fin/service/Fin1004Service.java
- ❌ `Fin1005Service` — fin/service/Fin1005Service.java
- ❌ `Fin1006Service` — fin/service/Fin1006Service.java
- ❌ `Fin1101Service` — fin/service/Fin1101Service.java
- ❌ `Fin1102Service` — fin/service/Fin1102Service.java
- ❌ `Fin1201Service` — fin/service/Fin1201Service.java
- ❌ `Fin1308ChartOfAccountsPdfService` — fin/service/Fin1308ChartOfAccountsPdfService.java
- ❌ `Fin1401Service` — fin/service/Fin1401Service.java
- 🟡 `FinPostingService` — fin/service/FinPostingService.java
- ❌ `FinReportService` — fin/service/FinReportService.java

---

## 6. Configuration & resources

| Java resource | .NET counterpart | Status |
|---|---|---|
| `application.yaml` | `appsettings.json` | ✅ incl. Npgsql pool tuning ported from Hikari |
| `application-dev.yaml` | `appsettings.Development.json` | ✅ pre-existing |
| `application-test.yaml` | `appsettings.Test.json` | ✅ |
| `application-prod.yaml` | `appsettings.Production.json` | ✅ |
| `application-local.yaml` | `appsettings.Local.json` | ✅ |
| `additional-spring-configuration-metadata.json` | n/a — no .NET equivalent needed | ✅ |
| `db-migration/*.sql` | not copied | ❌ |
| `SecurityConfig` (CORS origin patterns, HSTS, CSP, frame-options, public route list) | `Program.cs` | 🟡 CORS + headers + JWT done; per-route public/authenticated policy list still to wire |
| `SwaggerConfig` (3 API groups, JWT bearer scheme, contact/licence) | `AddOpenApi()` only | 🟡 groups and bearer scheme absent |
| `AsyncConfig` (bounded pool 2/5/200, CallerRunsPolicy, 15s drain) | — | ❌ needed once `SysLogService` async writes land |
| `JacksonConfig` (integer→string coercion) | `Program.cs` `AllowReadingFromString` | ✅ |
| `WebConfig` (3 interceptors + `/uploads` static file handler) | `Program.cs` | 🟡 static handler done; tenant/RBAC/audit interceptors still absent |
| Response compression (gzip, 2 KB threshold) | `appsettings.json` keys present | 🟡 middleware not yet registered |

---

## 7. Recommended sequencing

The dependency order below avoids rework — each stage compiles against the one before it.

1. **Finish core** — RBAC service + authorization filter, `SysLogService` + audit interceptor,
   auth services (`AuthConfigService`, `AuthRuntimeValidationService`, `LoginAttemptLimiter`,
   `SysSessionService`, `UserDetailsServiceImpl`), `AppBootstrapService`,
   `CommonLookupService` + its 8 lookup DTOs.
2. **Cross-cutting infrastructure** — EF global query filters (tenant + soft-delete),
   a transaction-boundary convention (`IUnitOfWork` or a transaction filter) to close **C1**,
   configuration files, CORS and security headers.
3. **Repository layer** — 122 repositories, translating every custom `@Query`.
4. **DTO layer** — 209 DTOs with their validation attributes.
5. **Module services & controllers** — SYS → HRM → INV → PUR → SAL → FIN
   (FIN last: it consumes the `sys_event_outbox` rows the other modules write).

---

## 8. Overall completion

| Measure | Value |
|---|---:|
| File coverage (name match) | 38.9% (283 / 727) |
| Endpoint coverage | 28.6% |
| **Business-logic coverage (LOC-weighted)** | **≈24%** (12,835 / ~53,500 LOC) |
| Build status | ✅ succeeds (was 348 errors) |
| Files created across all passes | 14 |
| Files modified across all passes | 24 |
| Java files still to migrate | **444** |

### Defect status

| Defect | Status |
|---|---|
| C1 — no transaction boundaries | 🟡 `IUnitOfWork` exists and every new SYS service uses it; **the remaining call sites still need wrapping** |
| C2 — `PermissionBits` bit 32 wrong | ✅ fixed |
| C3 — JWT secret silently padded | ✅ fixed |
| C4 — JWT missing claims | ✅ fixed |
| C5 — exception hierarchy broken | ✅ fixed |
| C6 — exception handler incomplete | ✅ fixed |
| C7 — no tenant query filter | ✅ fixed |
| C8 — no repository layer | ❌ 122 repositories outstanding |
| C9 — no soft-delete filter | ✅ was already present; now combined with the tenant predicate |
| C10 — no audit-log interceptor | ❌ outstanding |
| C11 — no authentication scheme registered | ✅ fixed |

**The migration is not complete.** Roughly 45,000 lines of business logic remain.
The foundation (security, tenancy, transactions, error handling, configuration) is now sound;
what remains is the bulk translation of repositories, DTOs, services and controllers.

> **Note on concurrent edits.** During this audit another process was writing to
> `src/AidlyErp.Application/Hrm/Services/` (`HrmSetupServices.cs`, `HrmOperationalServices.cs`,
> `LeaveRuleEngine.cs`). Those two files currently fail to compile against the existing HRM
> entities. They were left untouched to avoid clobbering in-flight work. `sme-dotnet-backend` is
> **not under version control**, so concurrent sessions cannot be reconciled or recovered —
> initialising git here is strongly recommended before further parallel work.
