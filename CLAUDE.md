# CLAUDE.md — Aidly ERP Backend

## Project Overview

**Aidly ERP** is a multi-tenant, Spring Boot REST API backend for Small and Medium Enterprise (SME) management. Built by Infoaidtech, it provides modules for system administration, HRM, and more.

- **Java 21** | **Spring Boot 4.0.6** | **Maven** (wrapper included)
- **PostgreSQL** (production) | **H2** (tests)
- **Group:** `com.infoaidtech` | **Artifact:** `aidly`
- **Base package:** `com.infoaidtech.aidly`

---

## Tech Stack

| Layer       | Technology                                     |
| ----------- | ---------------------------------------------- |
| Framework   | Spring Boot 4.0.6                              |
| Security    | Spring Security + JWT (JJWT 0.12.6), stateless |
| ORM         | Spring Data JPA / Hibernate                    |
| Database    | PostgreSQL (prod), H2 (test)                   |
| DTO Mapping | MapStruct 1.6.3                                |
| API Docs    | SpringDoc OpenAPI 3.0.2 (Swagger UI)           |
| Monitoring  | Spring Boot Actuator                           |
| Boilerplate | Lombok                                         |
| Build       | Maven + Maven Wrapper (`./mvnw`)               |

---

## Architecture

### Module-Based Package Structure

```
com.infoaidtech.aidly
├── core/                    # Shared infrastructure
│   ├── auth/                # Authentication (JWT, login, sessions, RBAC interceptor)
│   │   ├── controller/
│   │   ├── dto/
│   │   ├── entity/
│   │   ├── interceptor/
│   │   ├── model/
│   │   ├── repository/
│   │   └── service/
│   ├── audit/               # Audit logging
│   │   ├── entity/
│   │   ├── interceptor/
│   │   ├── repository/
│   │   └── service/
│   ├── security/            # Security config, JWT filter, tenant context
│   └── shared/              # Cross-cutting: config, exceptions, response, audit base, utilities
│       ├── audit/           # AuditEntity, BaseEntity, JpaAuditConfig
│       ├── config/          # Jackson, Swagger, Async, Web config
│       ├── exception/       # GlobalExceptionHandler, custom exceptions
│       ├── response/        # ApiResponse wrapper
│       └── util/            # DateUtils, StringUtils, SecurityUtils, FileStorageUtil
├── sys/                     # System module (Company, Branch, User, Role, Menu, Permissions)
│   ├── controller/
│   ├── dto/
│   ├── entity/
│   ├── mapper/
│   ├── repository/
│   └── service/
├── hrm/                      # HRM module (Department, Designation, Employee)
│   ├── controller/
│   ├── dto/
│   ├── entity/
│   ├── mapper/
│   ├── repository/
│   └── service/
└── (bootstrap removed)
```

### Layered Architecture (per module)

Each module follows: **Controller → Service → Repository → Entity**, with **DTOs** and **Mappers** for data transfer.

---

## Key Design Patterns

### 1. Multi-Tenancy — STRICT Company + Branch Isolation

This is a **shared-database, separate-company** ERP. 5–15 companies (owned by different owners) share one PostgreSQL database. A data leak between companies is a **critical security violation**.

#### The golden rules — no exceptions:

**Rule 1 — `company_no` and `branch_no` ALWAYS come from auth, never from the request.**

```java
// ✅ CORRECT
Long companyNo = CompanyBranchContext.getCompanyNo();
Long branchNo  = CompanyBranchContext.getBranchNo();

// ❌ WRONG — never trust the client
Long companyNo = dto.getCompany_no();
Long branchNo  = dto.getBranch_no();
```

**Rule 2 — Every list query MUST filter by the auth-context scope.**

```java
// ✅ Branch-scoped entity (has branch_no column)
repo.findByBranchNoAndIsDeletedOrderByXxxAsc(branch(), DELETED)

// ✅ Company-scoped entity (has company_no column, no branch_no)
repo.findByCompanyNoAndIsDeletedOrderByXxxAsc(company(), DELETED)

// ❌ NEVER — exposes all companies' data
repo.findAllByIsDeletedOrderByXxxAsc(DELETED)
```

**Rule 3 — On INSERT, always set scope from auth context, not from DTO.**

```java
// ✅ CORRECT
entity.setBranchNo(branch());   // or entity.setCompanyNo(company())

// ❌ WRONG
entity.setBranchNo(dto.getBranch_no());
```

**Rule 4 — The `getListByBranch(Long branchNo)` pattern is deprecated.**
These legacy methods must delegate to `getList()` which uses auth context:

```java
public List<XxxDto> getListByBranch(Long branchNo) {
    return getList(); // branch taken from auth — parameter ignored
}
```

**Rule 5 — Uniqueness checks must also be scoped.**

```java
// ✅ Branch-scoped duplicate check
repo.existsByCodeAndBranchNoAndIsDeleted(code, branch(), DELETED)

// ❌ Cross-company collision risk
repo.existsByCodeAndIsDeleted(code, DELETED)
```

#### Standard helper methods (add to every service):

```java
private Long company() {
    Long c = CompanyBranchContext.getCompanyNo();
    if (c == null) throw new ValidationException("No active company in context");
    return c;
}

private Long branch() {
    Long b = CompanyBranchContext.getBranchNo();
    if (b == null) throw new ValidationException("No active branch in context");
    return b;
}
```

#### Which scope to use:

| Entity type                                       | Filter by                       | Example                                                      |
| ------------------------------------------------- | ------------------------------- | ------------------------------------------------------------ |
| Branch-scoped (has `branch_no`)                   | `branch()`                      | FinYear, Currency, VatTax, Department, Designation, Employee |
| Company-scoped (has `company_no`, no `branch_no`) | `company()`                     | ExchangeRate, Settings                                       |
| User-scoped lookups                               | `company()` via user.company_no | Users for the current company                                |
| System-wide reference data                        | no filter needed                | Menus, Modules, Submodules                                   |

Every business entity carries `company_no` and/or `branch_no`. The current tenant context is held in `CompanyBranchContext` (ThreadLocal), set by `JwtAuthenticationFilter` from JWT claims, and cleaned up by `CompanyBranchFilter` after each request.

### 2. Soft-Delete

All entities extend `AuditEntity` which provides:

- `is_active` (Short: 1=active, 0=inactive)
- `is_deleted` (Short: 0=live, 1=deleted)
- `deleted_by`, `deleted_at`

Use `entity.performSoftDelete(userNo)` to soft-delete. Override `nullifyBusinessId()` in entities with unique business codes to free the unique constraint slot (e.g., set `companyId = null`).

### 3. Optimistic Locking

`row_version` field with `@Version` annotation. Initialized to `1L`. Spring throws `OptimisticLockingFailureException` on concurrent edit conflicts (returns 409).

### 4. Audit Fields

Automatic via Spring Data JPA auditing (`@CreatedBy`, `@CreatedDate`, `@LastModifiedBy`, `@LastModifiedDate`):

- `created_by`, `created_at` — set on INSERT, never updated
- `updated_by`, `updated_at` — refreshed on every save

### 5. JWT Authentication (Stateless)

- `JwtAuthenticationFilter` validates JWT from `Authorization: Bearer <token>` header
- Tokens carry: `userNo`, `companyNo`, `branchNo`, `userName`, `roleNos`, `sessionNo`, `tokenVersion`
- Access token TTL: 15 min (900000ms) | Refresh token TTL: 7 days (604800000ms)
- JWT secret configured via `aidly.jwt.secret` property (min 32 chars)
- **Secrets must be provided via environment variables** — no fallbacks in config files
- Refresh tokens are opaque random values, SHA-256 hashed before DB storage

### 6. RBAC (Role-Based Access Control)

- `Role` — branch-scoped (`branch_no` + `role_id` unique per branch)
- `UserRole` — user-to-role assignment
- `RolePermission` — role-to-permission mapping
- `UserBranch` — user-to-branch membership
- `RbacAuthorizationInterceptor` enforces permissions per request

### 7. Standard API Response

All endpoints return `ApiResponse<T>`:

```json
{
  "status_code": 200,
  "data": { ... },
  "message": "Operation completed successfully",
  "timestamp": "2026-05-17T10:30:00+06:00"
}
```

Use `ApiResponse.success(data)` or `ApiResponse.success(data, message)` for success. Use `ApiResponse.error(statusCode, message)` for errors.

**Note:** The JSON field is `status_code` (snake_case), not `statusCode`. DTOs use snake_case field names matching DB columns directly.

### 8. MapStruct DTO Mapping

Mappers use `@Mapper(componentModel = "spring")`. Always ignore audit fields on entity creation/update:

```java
@Mapping(target = "createdBy",  ignore = true)
@Mapping(target = "createdAt",  ignore = true)
@Mapping(target = "updatedBy",  ignore = true)
@Mapping(target = "updatedAt",  ignore = true)
@Mapping(target = "isDeleted",  ignore = true)
@Mapping(target = "deletedBy",  ignore = true)
@Mapping(target = "deletedAt",  ignore = true)
@Mapping(target = "rowVersion", ignore = true)
```

For updates, also ignore the primary key and business ID.

### 9. Security Headers

`SecurityConfig` enforces:

- `X-Frame-Options: DENY` — prevents clickjacking
- `Strict-Transport-Security` — enforces HTTPS (max-age 1 year, includeSubDomains)
- `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'`

### 10. Database Indexes

All entities have `@Index` annotations on columns used in WHERE clauses. Pattern: `idx_{entity}_{column}_deleted` for composite indexes with `is_deleted`. Example:

```java
@Table(name = "hrm_department", indexes = {
    @Index(name = "idx_dept_company_deleted", columnList = "company_no, is_deleted"),
    @Index(name = "idx_dept_branch_deleted", columnList = "branch_no, is_deleted")
})
```

### 11. Global Exception Handling

`GlobalExceptionHandler` (`@RestControllerAdvice`) handles:

- `NotFoundException` → 404
- `ValidationException` → 400
- `DomainException` → 500
- `MethodArgumentNotValidException` → 400 (field errors map)
- `OptimisticLockingFailureException` → 409
- `DataAccessException` → 500 (with user-friendly DB error messages)
- `TransactionSystemException` → 400 (constraint violations at flush time)

---

## API Conventions

- **Base path:** `/api/v1/{module}/{resource}`
- **Swagger UI:** `/swagger-ui.html`
- **Public endpoints:** `/api/v1/auth/login`, `/api/v1/auth/contexts`, `/api/v1/auth/refresh`, `/actuator/health`
- **CRUD verbs:** POST (create), PUT (update), DELETE (soft-delete), GET (list/detail)
- **Validation:** Use `@Valid` on `@RequestBody` parameters; use Jakarta Validation annotations on DTOs
- **Path variables:** Use entity's surrogate key (e.g., `/{departmentNo}`)
- **Pagination:** List endpoints have `/page` variants that accept `Pageable` params (`page`, `size`, `sort`)

### Pagination Pattern

All list endpoints have a paginated variant at `/page`:

```
GET /api/v1/sys/companies/page?page=0&size=20&sort=companyName,asc
GET /api/v1/hrm/employees/page?page=0&size=10&sort=fullName
```

Controller pattern:

```java
@GetMapping("/page")
public ResponseEntity<ApiResponse<Page<EntityDto>>> getListPaginated(Pageable pageable) {
    return ResponseEntity.ok(ApiResponse.success(service.getList(pageable)));
}
```

Service pattern:

```java
@Transactional(readOnly = true)
public Page<EntityDto> getList(Pageable pageable) {
    return repository.findAllByIsDeleted((short) 0, pageable)
            .map(mapper::toDto);
}
```

Repository pattern:

```java
Page<Entity> findAllByIsDeleted(short isDeleted, Pageable pageable);
```

---

## Repository Query Pattern

Always filter soft-deleted records:

```java
List<Entity> findAllByIsDeleted(short isDeleted);                    // pass (short) 0
Page<Entity> findAllByIsDeleted(short isDeleted, Pageable pageable); // paginated
Optional<Entity> findByXxxAndIsDeleted(Long xxx, short isDeleted);   // pass (short) 0
boolean existsByXxxAndIsDeleted(String xxx, Long yyy, short isDeleted);
```

Provide both `List` and `Page` variants for list queries.

---

## Query Performance Rules (Super-Fast Queries)

The database is a **remote/cloud Postgres (Aiven)** — every query is a network round trip (~10–50 ms each). Throughput is dominated by **how many round trips a request makes**, not by how clever each SQL statement is. Optimize for _fewer round trips_ first, then for index-friendly SQL.

### Global tuning (already configured — do not regress)

These live in `application.yaml` and must stay on:

- **JDBC batching:** `hibernate.jdbc.batch_size=50`, `order_inserts`, `order_updates`, `batch_versioned_data=true`.
- **`reWriteBatchedInserts=true`** (Hikari `data-source-properties`) — turns batched inserts into multi-row `INSERT … VALUES (…)`.
- **`default_batch_fetch_size=100`** — collapses lookups into `IN (…)` batches.
- **Warm pool:** Hikari `minimum-idle == maximum-pool-size` (cloud handshakes are expensive).
- **`open-in-view: false`** — connection released before view rendering.
- **SQL logging OFF** in base/dev-server/prod (`org.hibernate.SQL`, `org.hibernate.orm.jdbc.bind`, `org.hibernate.type.descriptor.sql` = WARN). Only the `local` profile may set these to DEBUG/TRACE.

### DO

- **DO read once, reuse in-memory.** If a value is needed for many rows/legs (e.g. a warehouse flag, a costing setting), look it up **once per request** and memoize in a local `Map`/variable — never inside a per-row loop. (See `InvStockPostingService.allowsNegative(..)` memo.)
- **DO let dirty checking flush managed entities.** Entities loaded inside the transaction are auto-flushed at commit as batched UPDATEs. Mutate them and let them flush — calling `repo.save()` on an already-managed entity in a loop is redundant and defeats batching.
- **DO project to DTOs for reads.** Use JPQL `select new …Dto(…)` or interface projections so only needed columns cross the wire. Don't load full entities just to map a few fields.
- **DO use `@Transactional(readOnly = true)`** on every read path (skips dirty checking; one connection for the whole method).
- **DO paginate every list** (`Pageable`). For deep/large scans prefer **keyset/seek** (`WHERE pk > :last ORDER BY pk LIMIT n`) over large `OFFSET`.
- **DO batch multi-row writes** through a single `saveAll(...)` / batched loop so `batch_size` + `reWriteBatchedInserts` engage — not one `save()` per row across a network hop.
- **DO add a composite `@Index`** matching each query's `WHERE` (+ `is_deleted`) and `ORDER BY`. Equality columns first, range/sort column last. Filtered queries on a constant (e.g. `is_deleted = 0`) are candidates for **partial indexes** in DDL.
- **DO verify slow paths with `EXPLAIN (ANALYZE, BUFFERS)`** and find offenders via `pg_stat_statements` sorted by `total_exec_time` (calls × mean) — a cheap query run thousands of times is the usual culprit here, not a slow one.
- **DO keep transactions short and DB-only.** No HTTP calls, file I/O, or heavy CPU while a connection/lock is held — it pins the pool and lengthens lock waits.
- **DO hold pessimistic locks (`lockCell` `FOR UPDATE`) for the shortest possible span** and acquire them in a consistent order to avoid deadlocks.

### DON'T

- **DON'T query inside a loop (N+1).** No `find…()` per row/leg. Fetch the set up front with `findAllByXxxIn(collection)` or a join, then map in memory.
- **DON'T call `repo.save()` on managed entities in a loop** (redundant; blocks batching). Only `save()` brand-new entities, ideally via `saveAll`.
- **DON'T use `GenerationType.IDENTITY` on high-write tables.** IDENTITY **disables JDBC insert batching** (each insert round-trips for its key). Use `GenerationType.SEQUENCE` with `allocationSize` ≥ 50 for append-heavy tables (ledger, valuation layers, audit/log). _(Migrating existing IDENTITY tables requires creating the DB sequence first — coordinate the DDL.)_
- **DON'T turn on `show-sql`/`format_sql` or `org.hibernate.SQL` DEBUG outside the `local` profile.** It is a heavy synchronous per-statement cost and leaks bind values.
- **DON'T fetch entities you only read** — avoid materializing full graphs for a count or a few fields.
- **DON'T `findAll()` without `is_deleted` filter or pagination.**
- **DON'T wrap the whole request in one giant transaction** that spans external work; keep the DB transaction tight.
- **DON'T add an index per column blindly** — every index slows writes and bloats the table. Index for real query patterns; drop unused ones (`pg_stat_user_indexes` where `idx_scan = 0`).
- **DON'T do `lock-or-create` without a DB unique constraint** on the natural key — concurrent first-inserts can both create duplicates (e.g. the `inv_stock` cell tuple needs a unique constraint).

### Reference data caching

Rarely-changing lookups (products, warehouses, UOM, currencies, settings) read on hot paths should be cached (`@Cacheable` / second-level cache) so they don't cross the network every request. Evict on write.

---

## Build & Run

```bash
# Build (includes tests)
./mvnw clean package

# Run (local profile — requires env vars)
DB_PASSWORD=xxx JWT_SECRET=xxx ./mvnw spring-boot:run -Dspring-boot.run.profiles=local

# Run (dev profile — requires env vars)
DB_PASSWORD=xxx JWT_SECRET=xxx ./mvnw spring-boot:run -Dspring-boot.run.profiles=dev

# Run tests
./mvnw test

# Docker build
docker build -t aidly-backend .
docker run -p 7860:7860 -e DB_PASSWORD=xxx -e JWT_SECRET=xxx aidly-backend
```

### Environment Variables

All secrets are provided via environment variables — no hardcoded credentials in config files.

| Variable                 | Required | Description                                           |
| ------------------------ | -------- | ----------------------------------------------------- |
| `DB_URL`                 | Yes      | PostgreSQL JDBC URL                                   |
| `DB_USERNAME`            | Yes      | Database username                                     |
| `DB_PASSWORD`            | Yes      | Database password                                     |
| `JWT_SECRET`             | Yes      | JWT signing secret (min 32 chars)                     |
| `JWT_ACCESS_EXPIRATION`  | No       | Access token TTL in ms (default: 900000 = 15 min)     |
| `JWT_REFRESH_EXPIRATION` | No       | Refresh token TTL in ms (default: 604800000 = 7 days) |

### Spring Profiles

| Profile | Purpose                | Config File                            |
| ------- | ---------------------- | -------------------------------------- |
| `local` | Local development      | `application-local.yaml` (gitignored)  |
| `dev`   | Development server     | `application-dev.yaml`                 |
| `prod`  | Production             | `application-prod.yaml`                |
| `test`  | Unit/integration tests | `application-test.yaml` (H2 in-memory) |

---

## Mandatory API Isolation Rule (CRITICAL)

**Every API must enforce 4 dimensions:**

| Dimension    | Source                                | Required                                 | Notes                              |
| ------------ | ------------------------------------- | ---------------------------------------- | ---------------------------------- |
| `company_no` | `CompanyBranchContext.getCompanyNo()` | **ALWAYS**                               | Company isolation — never from DTO |
| `branch_no`  | `CompanyBranchContext.getBranchNo()`  | **ALWAYS** (nullable for company-scoped) | Branch isolation — never from DTO  |
| `is_deleted` | Filter `WHERE is_deleted = 0`         | **ALWAYS**                               | Soft-delete filter on every query  |
| `role_no`    | `CompanyBranchContext` / JWT claims   | **ALWAYS**                               | Role-based permission check        |

### The rule:

- **Every list query** must filter by `company_no` AND `is_deleted` at minimum
- **Every list query** must filter by `branch_no` if the entity is branch-scoped
- **Every write operation** (insert/update/delete) must verify the current user's role has permission for that form (`SYS_{FORM_ID}`)
- **`role_no` comes from the login branch** — each user has a role per branch via `sys_user_branch.role_no`
- **Permission check is done by `RbacAuthorizationInterceptor`** at the request level via `X-Form-Id` header
- **Services must NOT bypass the interceptor** — always send `X-Form-Id` from frontend

### Frontend requirement:

Every API call from Angular **must** send these headers:

```
X-Company-No: {company_no}
X-Branch-No:  {branch_no}
X-User-No:    {user_no}
X-Form-Id:    SYS_{FORM_ID}
```

### Backend requirement:

Every service **must** use auth context for scoping:

```java
Long companyNo = CompanyBranchContext.getCompanyNo();  // NEVER from DTO
Long branchNo  = CompanyBranchContext.getBranchNo();    // NEVER from DTO
```

**NEVER read `company_no`, `branch_no`, `role_no` from the DTO/request body.**
These values come from the JWT token and auth headers. The frontend sends them as headers (`X-Company-No`, `X-Branch-No`, `X-User-No`), and the backend `CompanyBranchContext` resolves them from the authenticated session. Even if the DTO has these fields, always use auth context.

---

## What To Do

- **New entity:** Extend `AuditEntity`. Override `nullifyBusinessId()` if it has a unique business code.
- **New module:** Create package under `com.infoaidtech.aidly.{module}` with `controller/`, `service/`, `repository/`, `entity/`, `dto/`, `mapper/` sub-packages.
- **New mapper:** Use MapStruct with `@Mapper(componentModel = "spring")`. Ignore all audit fields. Provide `toDto()`, `toEntity()`, and `updateEntityFromDto()` methods.
- **New service:** Use `@Service`, `@RequiredArgsConstructor`, `@Slf4j`. Annotate methods with `@Transactional` (or `@Transactional(readOnly = true)` for reads).
- **New controller:** Use `@RestController`, `@RequestMapping("/api/v1/{module}/{resource}")`, `@RequiredArgsConstructor`, `@Tag` for Swagger. Return `ResponseEntity<ApiResponse<T>>`. Add `/page` endpoint for paginated listing.
- **New repository:** Provide both `List<T>` and `Page<T>` variants for list queries. Add `@Index` annotations on entity for columns used in WHERE clauses.
- **Current user:** Use `SecurityUtils.currentUserNo()` or `SecurityUtils.currentPrincipal()`.
- **Tenant context:** Use `CompanyBranchContext.getCompanyNo()` / `.getBranchNo()`.
- **Validation:** Use Jakarta Validation annotations (`@NotBlank`, `@NotNull`, `@Size`, etc.) on DTOs.
- **Date range inputs:** Always apply `min` and `max` guards to prevent invalid ranges (e.g., set `[max]="toDate"` on the from-date input, and `[min]="fromDate"` on the to-date input).

## Form-Wise API Architecture (CRITICAL)

**Every form has its own dedicated Controller, Service, and DTOs.** Forms do NOT call generic CRUD controllers.

### Pattern

```
/api/v1/sys/forms/sys1001/   → Sys1001Controller → Sys1001Service → Sys1001CompanyDto
/api/v1/sys/forms/sys1002/   → Sys1002Controller → Sys1002Service → Sys1002BranchDto
/api/v1/sys/forms/sys1101/   → Sys1101Controller → Sys1101Service → Sys1101UserDto
```

### Rules

1. **One controller per form** — located at `/api/v1/sys/forms/{formId}/`
2. **One service per form** — `Sys{FormId}Service` with form-specific business logic
3. **Form-specific DTOs** — `Sys{FormId}{Entity}Dto` with only the fields the form needs
4. **No cross-form API calls** — a form only calls its own controller
5. **Return data ascending by primary key** — all list queries use `ORDER BY {pk} ASC`
6. **Generic CRUD controllers** (`/api/v1/sys/users`, `/api/v1/sys/branches`, etc.) are for external/API consumers only — forms never call them
7. **Common-table contract** — when a frontend form renders a tabular list with `<app-common-table>`, the form-specific backend DTO must expose snake_case fields that exactly match the frontend `ColumnDef.field` values. Do not make the frontend translate ad-hoc field names, and do not route table data through generic CRUD controllers.

### Example

```java
@RestController
@RequestMapping("/api/v1/sys/forms/sys1101")
public class Sys1101Controller {
    private final Sys1101Service service;

    @GetMapping("/employees")
    public ResponseEntity<ApiResponse<List<Sys1101EmployeeDto>>> getEmployeeList() { ... }

    @GetMapping("/users/{userNo}")
    public ResponseEntity<ApiResponse<Sys1101UserDto>> getUserDetail(@PathVariable Long userNo) { ... }
}
```

---

## Approval Workflow (CRITICAL — configured, never hard-coded)

**Any document that needs an approval gate (leave, payroll run, job requisition, purchase order, stock adjustment, separation, final settlement, …) MUST route through the shared Approval layer — it is NOT a per-form `if/else`.** The approver, the number of steps, and the value thresholds are _configuration_, set per company in **SYS_1108 Approval Workflow Setup**, never coded into the document service.

### The two layers

| Layer       | Table                                                  | Owner                                | Role                                                                                                                                                                                                                                                |
| ----------- | ------------------------------------------------------ | ------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Config**  | `sys_approval_workflow`                                | **SYS_1108 Approval Workflow Setup** | Per `document_type`: an ordered set of **steps** (`step_number`), each routed to an **`approver_role_no`** within an **amount band** (`amount_min`..`amount_max`); `is_final = 1` marks the terminal step.                                          |
| **Runtime** | `sys_approval_request` (+ `sys_approval_request_step`) | the shared `ApprovalService` engine  | The live request raised against one document: `document_type`, `document_pk`, `amount`, `current_step`, `status` (1=Pending 2=Approved 3=Rejected 4=Cancelled). Each step records an `action` (1=Pending 2=Approved 3=Rejected) by the acting user. |

### The engine is LIVE (`ApprovalService` + event-decoupled callback)

Implemented in `com.infoaidtech.aidly.sys.service.ApprovalService` (+ `ApprovalRequestRepository`, `ApprovalRequestStepRepository`, inbox at `ApprovalInboxController` → `/api/v1/sys/approvals`). Modules stay decoupled via a Spring event:

- `approvalService.raise(documentType, documentPk, documentNo, amount)` → `ApprovalOutcome` (`auto()` when no workflow covers the amount, else `pending(requestNo)`).
- `approvalService.act(requestNo, approve, remarks)` → validates the acting user holds the current step's `approver_role_no`, advances the chain, and on the terminal step publishes **`ApprovalCompletedEvent(documentType, documentPk, approved)`**.
- Each module registers an `@EventListener` (e.g. `HrmApprovalListener`) that dispatches by `document_type` to its `applyApprovalOutcome(pk, approved)`. The listener runs **synchronously in the engine's transaction**, so side-effects commit atomically with the approval. **This breaks the circular dependency** (document → ApprovalService → event; listener → document service) — never inject a document service into `ApprovalService`.

### How an approval-gated form works

1. **Submit** → `approvalService.raise(...)`. **Auto** (no workflow) → the service calls its own `applyApprovalOutcome(pk, true)` directly. **Pending** → set the document to its in-review status and store `approval_request_no`.
2. **Approve / reject** → a user whose role = the current step's `approver_role_no` acts via the inbox (`/api/v1/sys/approvals/{no}/approve|reject`) or a form button that delegates to `approvalService.act(...)`. The engine advances/terminates and fires the event.
3. **Document reacts to the outcome**, it does not decide it — the consume/ledger/lock/post happens in `applyApprovalOutcome`, triggered by the event (or directly on the auto path).
4. **`document_type`** registry: `HRM_LEAVE` (wired), `HRM_PAYROLL`, `HRM_ATT_ADJ`, `HRM_REQUISITION`, `HRM_SEPARATION`, `HRM_FINAL_SETTLEMENT`, `PUR_PO`, `INV_ADJ`, `SAL_RETURN`, …
5. **Fallback** — no `sys_approval_workflow` rows for a `document_type` ⇒ **auto-approved** so an unconfigured company isn't blocked.

### Rules

- **DO** gate every approvable document on `ApprovalService` + SYS_1108 config; the form service exposes **submit** (raise) + an `applyApprovalOutcome(pk, approved)` the listener calls.
- **DO** authorize approve/reject by the **current step's `approver_role_no`** (engine does this) plus the RBAC `approve` action.
- **DO** make privileged side-effects idempotent and inside the engine transaction (the listener is synchronous).
- **DON'T** hard-code approver roles, step counts, or thresholds in a document service; **DON'T** inject a document/HRM service into `ApprovalService` (use the event).
- **Status:** **`HRM_1301` Leave is wired** to the engine (reference impl: `Hrm1301Service.submit/approve/reject` + `HrmApprovalListener`). **`HRM_1202` Salary Processing and `HRM_1103` Attendance Adjustment are still interim** (direct approve) — wire them with the same pattern (a `HrmApprovalListener` case + `applyApprovalOutcome` + raise-on-submit).
- **Frontend:** an approval-gated form's **Submit** raises the request; **Approve/Reject** live in the approval inbox (current step + `canApprove()`).

---

## Database Schema Changes — MANDATORY WORKFLOW (CRITICAL)

Any change that adds or alters a **table, column, constraint, index, or seed
row** MUST produce BOTH deliverables below. One without the other is incomplete
work — the module DB file is the single source of truth for the current schema,
and it silently rots if only the migration is written.

### 1. A dated migration file (the thing the developer runs)

Path: `db-migration/`
Name: **`YYYY-MM-DD_HHMM_{module}_{table_or_feature}.sql`**

```
db-migration/2026-07-21_2220_hrm_leave_management.sql
db-migration/2026-07-22_0930_inv_stock_valuation_layer.sql
```

The date **and time** matter: several scripts can be produced in one day, and
the filename must sort into the exact order they are meant to run.

Requirements for the script:

- **Idempotent** — safe to run repeatedly (`IF NOT EXISTS`,
  `DROP CONSTRAINT IF EXISTS` + `ADD CONSTRAINT`,
  `INSERT … WHERE NOT EXISTS`).
- **Pure DDL/DML only.** No functions, procedures, triggers, views, or `DO`
  blocks — **all queries and business logic live in the middleware; the
  database holds tables only.**
- **Copy-paste runnable** as-is into pgAdmin, wrapped in `BEGIN; … COMMIT;`.
- **Never drop a column to rename it** — use `ALTER TABLE … RENAME COLUMN` so
  data is preserved. PostgreSQL has no conditional rename, so put renames in a
  clearly marked one-time section with a `SELECT` the developer runs first.
- **Never** rely on `ddl-auto`; the application must not alter the schema.

### 2. Update the module DB file in `../aidly-business-doc/`

| Module | File |
|---|---|
| SYS | `sys-db.md` |
| HRM | `hrm-db.md` |
| FIN | `fin-db.md` |
| INV | `inv-db.md` |
| PUR | `pur-db.md` |
| SAL | `sal-db.md` |

For every migration you write:

1. **Edit the `CREATE TABLE` block in place** so it shows the table's CURRENT
   shape — add the new columns, fix any drift. Tag each new line with a
   `-- YYYY-MM-DD` comment so history stays readable.
2. **Add new tables and indexes** to that module's schema section.
3. **Append an entry to the module's "Applied Migration Log"** (see the bottom
   of `hrm-db.md` for the format): the date, the migration filename, and a
   short summary of renames / added columns / added tables / seeds.
4. Put each change in the file that OWNS the table — e.g. approval-workflow
   columns go in `sys-db.md` even when the work was driven by an HRM feature.

**Cross-check before finishing:** every column the JPA entity maps must exist in
the module DB file. Entities extending `AuditEntity` need all nine audit
columns (`is_active`, `is_deleted`, `created_by/at`, `updated_by/at`,
`deleted_by/at`, `row_version`) — a missing `is_deleted` breaks every
`findBy…AndIsDeleted(…)` repository call at runtime.

## What NOT To Do

- **Do NOT use `spring.jpa.hibernate.ddl-auto: update` or `create`.** Database columns and schema changes MUST be created manually by the developer (e.g., through SQL scripts applied directly to the database). The middleware/application should NEVER alter the schema.
- **Do NOT hard-delete records.** Always use soft-delete via `performSoftDelete()`.
- **Do NOT store plain-text passwords.** Use `BCryptPasswordEncoder`.
- **Do NOT use session-based authentication.** The app is stateless (JWT).
- **Do NOT skip `isDeleted` filtering** in repository queries.
- **Do NOT expose internal surrogate keys** as primary identifiers in business logic (use business IDs like `departmentId`, `companyId`).
- **Do NOT create custom response wrappers.** Use `ApiResponse<T>`.
- **Do NOT forget `@Valid`** on `@RequestBody` parameters in controllers.
- **Do NOT use `@Transactional` on read-only queries.** Use `@Transactional(readOnly = true)`.
- **Do NOT add MapStruct mappings for audit fields.** Always `ignore = true` for `createdBy`, `createdAt`, `updatedBy`, `updatedAt`, `isDeleted`, `deletedBy`, `deletedAt`, `rowVersion`.
- **Do NOT hard-code company/branch numbers.** Always read from `CompanyBranchContext` or JWT claims.
- **Do NOT accept `company_no` or `branch_no` from the request DTO for filtering/scoping.** These MUST come from `CompanyBranchContext`. The DTO fields are for reference only.
- **Do NOT call `findAllByIsDeleted()` in any service `getList()` method.** Always use a company- or branch-scoped variant. `findAllByIsDeleted` is only acceptable in system-wide reference queries (menus, modules, etc.).
- **Do NOT skip the scope filter on uniqueness checks.** `existsByCodeAndIsDeleted` is a cross-company bug; use `existsByCodeAndBranchNoAndIsDeleted` or `existsByCodeAndCompanyNoAndIsDeleted`.
- **Do NOT use camelCase DTO field names.** DTOs use snake_case matching DB column names (e.g., `company_no`, `is_active`, `branch_name`). The SNAKE_CASE Jackson strategy has been removed — field names must be snake_case directly in the Java class.
- **Do NOT use `findAlL()` without `isDeleted` filter.** Records may include soft-deleted entries.
- **Do NOT commit secrets.** JWT secret and DB credentials come from environment variables or config files excluded from version control.
- **Do NOT commit `application-local.yaml`.** It's gitignored — contains local dev credentials.
- **Do NOT use `findAlL()` without pagination** for list endpoints. Provide both `List` (legacy) and `Page` (preferred) variants.

---

## CORS Allowed Origins

- `http://localhost:*`
- `https://*.infoaidtech.net`
- `https://infoaidtech.net`
- `https://*.hf.space`

---

## Key Files Reference

| File                                | Purpose                                                                  |
| ----------------------------------- | ------------------------------------------------------------------------ |
| `AidlyApplication.java`             | Main entry point (`@EnableAsync`)                                        |
| `SecurityConfig.java`               | Security filter chain, CORS, password encoder                            |
| `JwtAuthenticationFilter.java`      | JWT validation, sets SecurityContext + CompanyBranchContext              |
| `CompanyBranchFilter.java`          | ThreadLocal cleanup after each request                                   |
| `CompanyBranchContext.java`         | ThreadLocal tenant context (companyNo, branchNo, userNo, sessionNo)      |
| `JwtTokenService.java`              | JWT generation, validation, claim extraction                             |
| `ApiResponse.java`                  | Standard API response wrapper                                            |
| `AuditEntity.java`                  | Base entity with soft-delete, audit fields, optimistic locking           |
| `BaseEntity.java`                   | Extends AuditEntity, adds companyNo + branchNo for multi-tenant entities |
| `GlobalExceptionHandler.java`       | Centralized exception handling                                           |
| `SecurityUtils.java`                | Current user/principal helper                                            |
| `RbacAuthorizationInterceptor.java` | RBAC enforcement                                                         |
| `LoginAttemptLimiter.java`          | Brute-force protection                                                   |

---

## Deployment

- **Docker:** Multi-stage build (Maven build → Eclipse Temurin 21 JRE)
- **Docker health check:** `HEALTHCHECK` using `/actuator/health` (30s interval, 3s timeout, 3 retries)
- **Port:** 7860 (Hugging Face Spaces requirement)
- **Hugging Face:** Auto-synced from GitHub, runs with `--server.port=7860`
- **Spring profiles:** `local`, `dev`, `prod`, `test`
- **CI/CD:** GitHub Actions runs tests on push/PR to `main` (no `-DskipTests`)
