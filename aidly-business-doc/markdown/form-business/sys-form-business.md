# SYS Form Business

<!-- Consolidated from previous SYS plans, menu seeds, and implementation notes. -->

Each SYS form is served under `/api/v1/sys/forms/{formId}` and must use `X-Form-Id` for RBAC. Frontend screens use `permissionService.useLocalPermissions('<FORM_ID>')`, `page-form-layout`, and the shared common table rules.

| Form | Name | How It Works |
| --- | --- | --- |
| SYS_1001 | Company Setup | Maintains legal company identity, tax/business registration, contact profile, and active status. Company is the tenant anchor for all branch, finance, HR, inventory, purchase, and sales data. |
| SYS_1002 | Branch Setup | Maintains physical/operational branches under a company. One branch may be the main/default branch. Branch access drives login context and branch-scoped data. |
| SYS_1003 | Financial Year Setup | Maintains company-wide or branch-specific financial years. Empty branch selection means `branch_no = NULL` and applies to the full company. Generates accounting periods and blocks editing/actions when year status is closed. |
| SYS_1004 | Currency Setup | Maintains currencies and exchange-rate basis. Empty branch selection means company-wide currency. Base currency locks exchange rate to 1.0 and updates app-wide currency display settings. |
| SYS_1005 | VAT / Tax Setup | Maintains tax/VAT/TDS/AIT rules, effective dates, rates, authority, GL account, and branch/company scope. Used by document modules for tax calculation. |
| SYS_1006 | Exchange Rate Setup | Maintains dated exchange rates by currency. Uses the same common-table structure and date behavior as SYS1003/accounting-period style tables. Saving or deleting a rate must immediately sync `sys_currency.exchange_rate` to the latest active non-deleted rate for that currency; if no active history remains, the master rate falls back to `1`. |
| SYS_1007 | Cost Center Setup | Maintains hierarchical or branch-scoped cost centers for tagging financial and operational transactions. Root cost centers can apply company-wide. |
| SYS_1008 | System Settings | Maintains company/system options and policy toggles that affect form behavior and document processing. |
| SYS_1101 | User Management | Creates login users from employees, handles username/status/lock state, password reset, and role mapping visibility. |
| SYS_1102 | Role Management | Maintains company role templates. Roles are assigned per branch through user-branch mapping and are used by role permissions. |
| SYS_1103 | Role Permission Matrix | Grants per-menu permissions (`can_view`, `can_insert`, `can_update`, `can_delete`, `can_approve`) for a role. Saving evicts RBAC cache. |
| SYS_1104 | User Branch Mapping | Assigns which branches an employee login can access and the role held at each branch. One row is default login branch; inactive rows keep history but block access. |
| SYS_1105 | Activity Log Viewer | Read-only audit/activity viewer for system actions and traffic. |
| SYS_1107 | User Company Mapping | Assigns users to companies in multi-company scenarios. One company can be marked default. |
| SYS_1108 | Approval Workflow Setup | Configures document approval workflows, bands, approver roles, and active/inactive state. |
| SYS_1109 | Session / Login Monitor | Monitors sessions, login activity, and live session state. |
| SYS_1110 | Approval Inbox | Lists pending approval requests and lets approvers approve/reject within permission limits. |
| SYS_1201 | Dynamic Menu Builder | Vendor catalog editor for global `sys_module`, `sys_submodule`, and `sys_menu`. Requests explicitly carry `X-Form-Id: SYS_1201`. |
| SYS_1202 | Menu Enrollment | Enrolls purchased global menus for a company/branch and controls what forms are available to a tenant. |
| SYS_1203 | File / Image Manager | Manages uploaded assets and metadata used by forms, documents, and company branding. |

## Shared SYS Form Rules

- Every form must be RBAC-gated by form id.
- Empty branch multi-select means company-wide when the related table supports `branch_no = NULL`.
- Closed accounting years/periods disable edits and destructive actions.
- Common tables use the SYS1003 accounting-period table style: shared header/footer colors, compact search height, date click opens the date picker, minimum 20vh table body, and dynamic height from remaining content area.
- Sidebar sort is three-state: default source order, ascending, descending.
