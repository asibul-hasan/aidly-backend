# Aidly ERP — Form Design Guide

The complete pattern catalogue for building ERP screens: which layout to pick,
the exact markup for each, and the sizing rules that make them all behave.

Companion doc: `../form-design-standard.md` (tokens, building blocks, checklist).
Rules that override everything here: `sme-software-frontend/CLAUDE.md`.

---

## 0. Pick your archetype

Every screen is one of five shapes. Choose by what the user is doing, then copy
the reference form — do not invent a sixth.

| # | Archetype | Use when | Reference |
|---|---|---|---|
| 1 | **Master–detail, list sidebar** | Browse many flat records, edit one | **FIN_1001** Chart of Accounts |
| 2 | **Master–detail, tree sidebar** | Records form a hierarchy (parent/child) | **FIN_1002** Account Group |
| 3 | **Master–detail + filters** | The list needs narrowing before selection | **FIN_1101** Voucher Entry |
| 4 | **Single grid** | Operate on rows in place, no record selector | **SYS_1003** Financial Year → Periods |
| 5 | **Stacked-grid master–detail** | Parent grid drives one or more child grids | **SYS_1108** Approval Workflow Setup |

All five share the same shell:

```
<app-page-form-layout>            ← page card: header, action bar, split body
 ├─ [extraHeaderActions]          ← extra .btn-action buttons (Print, Post…)
 ├─ [sidebarFilters]              ← 2-col filter grid   (archetype 3)
 ├─ [sidebarList]                 ← list or tree        (archetypes 1–3)
 ├─ [sidebarFooter]               ← <app-sidebar-pagination>
 ├─ [tabBarContent]               ← optional page-level tab bar
 └─ [formContent]                 ← the editable form / grids
```

---

## 1. Page shell & full-page height

The shell is a **fixed-height flex column**. Nothing inside it should use
viewport math (`calc(100vh - 320px)`) — that desyncs from the real chrome.

```
.page-card              height:100%, flex column, overflow hidden
 ├─ .page-card-header   fixed band (title, subtitle, action buttons)
 ├─ tab bar             fixed band (optional)
 └─ .row (split body)   flex:1, min-height:0, overflow hidden
     ├─ sidebar col     h-full flex column (filters | list | footer)
     └─ content col     h-full flex column, overflow auto
         └─ .page-card-body   flex:1, padding 8px, gap 4px
             └─ [formContent]  ← styled BY the layout (see below)
```

**The layout owns `[formContent]`.** It is styled by
`page-form-layout.component.scss` as
`display:flex; flex-direction:column; flex:1 1 auto; min-height:0; width:100%`
so it always fills the body and the page scrolls exactly once.

```html
<!-- ✅ correct: no classes needed at all -->
<div formContent>…</div>
<!-- ✅ also fine (explicit, Tailwind) -->
<div formContent class="h-full flex flex-col">…</div>
<!-- ❌ NEVER: dead Bootstrap classes — they resolve to nothing, so the wrapper
     can't fill its parent; with an overflow class you get a nested scrollbar
     and dead white space under the card. -->
<div formContent class="h-100 d-flex flex-column overflow-y-auto">…</div>
```

**Cards inside `[formContent]`** are `.form-section-card`, which is
`flex-shrink: 0` — they keep their natural height and the column scrolls.
A card that must *fill* remaining height adds `.fill-card`
(`flex: 1 1 auto; min-height: 0`), which opts out of the no-shrink rule.

---

## 2. Archetype 1 — master–detail with a **list** sidebar

Reference: **FIN_1001** Chart of Accounts.

```html
<app-page-form-layout
  [title]="'Chart of Accounts'" [subtitle]="'GL account master'" [icon]="'menu_book'"
  [recordsCount]="model.filtered().length" [sidebarTitle]="'Accounts'"
  [searchTerm]="model.searchTerm()" (searchTermChange)="onSearchTermChange($event)"
  [sortDirection]="model.sortDirection()"
  (sortDirectionChange)="model.sortDirection.set($event); model.pager.reset()"
  (save)="onSave()" (new)="onNew()" (reset)="onReset()" (delete)="onDelete()"
  [showSave]="permissionService.canInsert() || permissionService.canUpdate()"
  [showNew]="permissionService.canInsert()"
  [showDelete]="permissionService.canDelete() && !!model.selectedNo()"
  [isSaving]="model.isSaving()" [isLoading]="model.isLoading()">

  <div sidebarList>
    @for (item of model.pager.paged(); track item.account_no) {     <!-- pager, NOT filtered() -->
      <button type="button" class="list-group-item list-group-item-action"
              [class.active]="model.selectedNo() === item.account_no"
              (click)="select(item)">
        <span class="status-dot status-dot--corner"
              [class.status-dot--active]="item.is_active === 1"
              [class.status-dot--inactive]="item.is_active !== 1"
              [matTooltip]="item.is_active === 1 ? 'Active' : 'Inactive'"></span>
        <h6 class="list-item-title mb-0 truncate" style="padding-right:16px">…</h6>
        <p class="list-item-subtitle mb-0 truncate">…</p>
        <div class="list-item-meta">
          <span class="list-item-chip"><mat-icon>apartment</mat-icon><span>…</span></span>
          <span class="list-item-tag list-item-tag--primary">System</span>
        </div>
      </button>
    }
    @if (model.filtered().length === 0) {
      <div class="list-empty-state"><mat-icon>menu_book</mat-icon><p>No records found</p></div>
    }
  </div>

  <div formContent>
    <form [formGroup]="formdata" autocomplete="off">
      <app-form-section title="Account" icon="menu_book"> … </app-form-section>
    </form>
  </div>

  <app-sidebar-pagination sidebarFooter [pager]="model.pager" />
</app-page-form-layout>
```

**Non-negotiables**

- Rows come from **`model.pager.paged()`**; `<app-sidebar-pagination>` is
  mandatory. Rendering `filtered()` directly is wrong.
- Status = **corner dot**, never an Active/Inactive text pill.
  Type/category = `.list-item-tag`.
- Sorting: give the paginator a `sortKey` and bind `[pager]` on the layout —
  no per-form `sortDirection` signal needed.
- Never add `p-3`, `mb-1`, borders or dividers to rows; the global
  `.list-group-item` owns the card look, hover and active accent.
- Scroll the active row into view on selection (see CLAUDE.md snippet).

---

## 3. Archetype 2 — master–detail with a **tree** sidebar

Reference: **FIN_1002** Account Group (also SYS_1007 Cost Center).
Use when rows have a parent/child column. Project `<app-tree-view>` into
`[sidebarList]` — nothing else changes.

```html
<div sidebarList>
  <app-tree-view
    [data]="$any(model.groups())"
    [config]="treeConfig"
    [selectedId]="model.selectedNo()"
    [searchTerm]="model.searchTerm()"
    (nodeClick)="onNodeClick($event)" />
</div>
```

```ts
readonly treeConfig: TreeViewConfig = {
  idField: 'account_group_no',
  parentField: 'parent_group_no',                                  // null/0 ⇒ root
  labelField: (row) => `${row['account_group_id']} ${row['group_name']}`,
  statusDot: (row) => row['is_active'] === 1,
  icon: ({ hasChildren, expanded }) =>
    hasChildren ? (expanded ? 'folder_open' : 'folder') : 'account_tree',
  emptyText: 'No account groups found', emptyIcon: 'account_tree',
};
```

- The tree owns expand/collapse, connector rails, its slim toolbar and the
  empty state. Search is passed in — it filters and auto-expands to matches.
- **`nodeClick` follows the same contract as `rowClick`**: it fires only when
  the selected node changes, so re-clicking the open node never repeats the
  detail fetch. Expand/collapse still responds to every click. Bind
  `[selectedId]` — that is what the guard compares against.
- Set `[showSort]="false"` on the layout: hierarchy order is meaningful.
- Optional: `sublabelField`, `badge`, `indent` (default 22),
  `showConnectors`, `autoExpandRoots`, `showToolbar`.
- Guard the parent dropdown against self/descendant selection in the model
  (`parentOptions()`), or users can build a cycle.

---

## 4. Archetype 3 — sidebar **filters**

Reference: **FIN_1101** Voucher Entry.
Filters project as **direct children** of `[sidebarFilters]`; the layout lays
them out on a 2-column grid (8px gap, blowout-guarded with `minmax(0,1fr)`).

```html
<div sidebarFilters>
  <ng-select class="col-span-2" …/>   <!-- col-span-2 = full row -->
  <app-date-picker …/>                 <!-- these two pair up automatically -->
  <app-date-picker …/>
</div>
```

- **No wrapper divs, no width classes.** A wrapper (or a Bootstrap `.row/.col-6`)
  re-introduces the grid blowout that pushed filters outside the sidebar.
- Every filter change calls `model.pager.reset()`, or rows strand on a page
  that no longer exists.
- Declare `[hasActiveFilters]` so the layout knows a filter is narrowing the
  list. (The "Clear all" button is intentionally disabled — each control has
  its own clear, and the search box has its ×.)

---

## 5. Archetype 4 — single grid

Reference: **SYS_1003** Financial Year → Accounting Periods — the canonical
table. Any tabular data uses `<app-common-table>`; never hand-roll `<table>`,
`mat-table`, or a Bootstrap table.

```html
<app-form-section title="Accounting Periods" icon="table_view" flush>
  <app-common-table [columns]="tableColumns" [data]="tableData()"
                    [config]="tableConfig" (rowClick)="onRowClick($event)" />
</app-form-section>
```

`flush` gives the section a zero-padding body so the table meets the card edge.
(Legacy equivalent: `<div class="card-body p-0">`.)

**Column recipes**

```ts
tableColumns: ColumnDef[] = [
  { title: 'Code',   field: 'code',   sort: true, cellType: 'text', width: '150px', pin: 'left' },
  { title: 'Name',   field: 'name',   sort: true, cellType: 'text' },
  { title: 'Date',   field: 'date',   sort: true, cellType: 'date', dateFormat: 'dd/MM/yyyy' },
  { title: 'Amount', field: 'amount', sort: true, cellType: 'currency',
    align: 'right', precision: 2, bottomTotal: true },
  { title: 'Status', field: 'status', sort: true, cellType: 'badge', badgeConfig: statusBadge },
];
```

| Need | Do this |
|---|---|
| Money / quantities | `cellType: 'currency'` or `'decimal'` — precision comes from the active currency; right-aligned automatically |
| Whole counts | `cellType: 'number'` (0 decimals) |
| **Years, IDs, codes** | `cellType: 'text'` — `number` adds thousands grouping (`2,026`) |
| Status | `cellType: 'badge'` + shared `statusBadge` mapper |
| Column totals | `bottomTotal: true`; label the row with `footerLabel: 'Total'` on another column; computed totals via `footerValue: (rows) => …` |
| Frozen columns | `pin: 'left'` / `pin: 'right'` (native sticky; offsets and z-index handled) |
| Lock a width | `resizable: false` (all columns are drag-resizable by default) |
| Cell ≠ `row[field]` | **must** set `value: (row) => …` so search, sort and export use the visible text |
| Custom cell | `cellType: 'custom-template'` + `customTemplate`; declare **`let-row`** (never `let-row="row"`) |

**Row interaction contract (AG-Grid style, global — no per-form wiring)**

| Gesture | Behaviour |
|---|---|
| Click a row | Becomes the active row; `(rowClick)` fires **once** |
| Click the **same** row again | Nothing — no emit, no state churn, **no repeat API call** |
| Click a different row | `(rowClick)` fires once for the new row |
| Double-click | Opens the row in edit mode; **never** double-fires `(rowClick)` |
| Click a control in a cell | Handled by that control only — the row does not react |

Row identity decides "did the selection change". Default: `__uid` → `id` →
object reference. **If the model replaces row objects immutably** (`.map(r => ({...r}))`),
set an explicit business key or every re-click looks like a change:

```ts
config: Partial<TableConfig> = { rowKey: 'req_id' };   // or (row) => row['req_id']
```

Programmatic API (use instead of touching `selection` directly):

| Method | Use |
|---|---|
| `selectRow(row)` | Select + notify; no-op if already active |
| `setActiveRow(row \| null)` | Mirror a parent's state **without** emitting — for model → table sync (cannot loop) |
| `clearSelection()` | Drop the active row (after New/Reset) so clicking it again re-emits |

`selection` (the checkbox `SelectionModel`) is now independent of the active
row, so row clicks no longer wipe checkbox selections. Deleting/acting on the
active row clears it automatically, as does a row disappearing from the data.

**Table height contract** — the card is *exactly as tall as the table*:

| Config | Result |
|---|---|
| *(nothing)* | height **auto**, with a **20vh floor** — grows with rows, never scrolls internally, no empty scroll box |
| `height: '<value>'` | exactly that (scrolls inside) |
| `maxHeight: '100%'` | fills a flex parent — the fill idiom; parent must be a flex column with `min-height: 0` |
| `minHeight: '<value>'` | raises the floor only |

Toolbar and paginator are fixed bands; only the scroll area flexes, so the
sticky totals row and paginator can never be clipped.

---

## 6. Archetype 5 — stacked-grid master–detail

Reference: **SYS_1108** Approval Workflow Setup — a parent grid whose selection
drives child grids, with no record sidebar.

```html
<app-page-form-layout [title]="'Approval Workflow Setup'" [icon]="'approval'"
                      [hasSidebar]="false" …>
  <div formContent class="gap-2 overflow-y-auto">

    <!-- Level 1 — master grid -->
    <div class="form-section-card card fill-card" style="flex:1 1 33%; min-height:250px">
      <div class="card-header flex items-center gap-2 flex-wrap">…</div>
      <div class="card-body p-0">
        <app-common-table [columns]="scopeColumns" [data]="scopeTableData()"
                          [config]="scopeTableConfig" (rowClick)="selectScope($event)" />
      </div>
    </div>

    <!-- Level 2 — child grid, driven by the master selection -->
    <div class="form-section-card card fill-card">…</div>
  </div>
</app-page-form-layout>
```

- `[hasSidebar]="false"` — the master grid *is* the selector.
- Each grid card is `.fill-card` so the stack shares the height; give each a
  `flex` ratio and a `min-height` so no grid collapses.
- Child grids use `showSearch:false, pagination:false, dense:true, compact:true`
  — they are detail panes, not browsers.
- Selecting a master row must clear/reload the child grids in the model, and
  disable child add-actions until a master row exists.

---

## 7. Form body — sections & fields

Applies to every archetype. Forms ship **no CSS**.

```html
<app-form-section title="Account" icon="menu_book">
  <app-form-field label="Account Code" for="account_code" required [span]="3">
    <input id="account_code" formControlName="account_code" class="form-control" />
  </app-form-field>

  <app-form-field label="Account Group" for="account_group_no" required [span]="4">
    <ng-select id="account_group_no" formControlName="account_group_no"
               [items]="model.groups()" bindValue="account_group_no" />
  </app-form-field>

  <app-check-field label="Status" text="Active" [span]="2" formControlName="is_active" />
</app-form-section>
```

- `[span]` = columns of 12 from the `md` breakpoint (replaces `col-md-*`);
  spans on one visual row sum to 12. `[xlSpan]` refines at `xl`.
- `<app-check-field>` is a CVA emitting the DB-first `1`/`0` — bind it with
  `formControlName`, never `[checked]` + `(change)`.
- Validation messages render automatically under any projected
  `formControlName` control. Don't add per-form error markup.
- Header extras project via `sectionActions`.
- Select options come from `{module}.constants.ts` — never inline arrays.
- GL accounts always display as `Code Name` (`1010 Cash in Hand`).

---

## 8. Post-save selection & list order

| Action | Reselect |
|---|---|
| Create | the **new** record's PK from `res.data` |
| Update | `selectedXxxNo()` — keep the selection |
| Delete | `null` — clear the form |

Sidebar lists always sort **descending by PK** (newest first). Never reselect
by array index.

---

## 9. Review checklist

1. Correct archetype, copied from its reference form?
2. No component CSS file (only the `:host` block)?
3. No dead Bootstrap classes (`d-flex`, `h-100`, `fw-*`, `me-*`, `col-md-*`,
   `text-end`, `ms-auto`)? Tailwind only.
4. Sidebar rows from `pager.paged()` + `<app-sidebar-pagination>`?
5. Filters as direct children of `[sidebarFilters]`?
6. Table sizing left to the contract unless the form genuinely fills height?
7. Years/IDs as `text`, money as `currency`/`decimal`?
8. `pager.reset()` on every search/filter/sort change?
9. Console clean — no template errors (they abort change detection app-wide)?

---

## 10. Gotchas that cost real debugging time

- **Tailwind utilities are layered; legacy global SCSS is not** — unlayered CSS
  wins ties. Use the `!` form (`!p-2`, `!text-indigo`) to beat a global class on
  the same element; never add a per-form stylesheet.
- **Material `mat-icon` sets its own colour/size** — icon utilities need `!`.
- **`tsc --noEmit` does not type-check host-binding strings** — only the Angular
  compiler does. Watch the build output too.
- **Editing a `_partial.scss` can log "No output file changes"** and serve stale
  CSS — touch `styles.scss` to force a recompile. Component-scoped SCSS never
  appears in `styles.css`; it is bundled into JS.
- **`overflow: hidden` sets a flex item's automatic min-size to 0**, so it can be
  squeezed to a sliver. That's why `.form-section-card` is `flex-shrink: 0`.
- **A percentage height inside an auto-height parent resolves to 0.** Don't put
  a blanket `height: 100%` on a component host.
