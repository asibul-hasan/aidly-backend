# AG Grid — Remediation Plan / Work Memo

Status as of 2026-07-30. AG Grid **Community 36.0.2** (MIT), registered app-wide by
`provideErpGrid()` in `app.config.ts`. Framework lives in
`apps/web-client/src/app/shared/ag-grid`.

This memo is written to be handed to an engineer (or an agent) as a work brief: each item
says **what is wrong**, **why it matters**, and **how to fix it**. Items are ordered by
priority. Nothing here is speculative — every claim was verified against the running app or
the installed package source, and each is marked accordingly.

---

## Verification legend

- **[live]** — reproduced in a running browser against the real grid.
- **[src]** — confirmed by reading the installed `ag-grid-community` / `@ng-select` source.
- **[code]** — confirmed by reading this repo.

---

## P0 — Two grid components exist; the docs describe neither accurately

**What.** `shared/ag-grid` currently ships **two** grid components: `<app-erp-grid>`
(`components/erp-grid.component.ts`, the one the showcase and demo use) and `<app-grid>`
(`components/grid.component.ts`, newer, with `models/grid-types.ts` + a
`directives/grid-cell.directive.ts`). They duplicate `buildColumns`, the toolbar, the
status bar and the editor wiring. `index.ts` exports both but **no longer exports**
`models/grid-config.model`, `utils/column-builder` or `utils/grid-defaults`, which both
components still import directly. **[code]**

**Why it matters.** `sme-software-frontend/CLAUDE.md` documents a third thing: a
`GridBuilder.create()` / `col.text().done()` / `appGridCell` / `enableDevValidations()` API.
Some of that now exists in `grid-types.ts`, but the documented names and the shipped names
still disagree, and CLAUDE.md claims SYS1104 uses `<app-grid>` when
`sys1104.component.html` renders a hand-rolled `<table class="table table-bordered">`. Any
developer or agent following CLAUDE.md writes code that does not compile.

**How to fix.**
1. Pick ONE component. Recommendation: keep `<app-grid>` (it is the documented direction and
   has the cell-template directive), port the pieces `<app-erp-grid>` has that it lacks
   (pinned totals row, density, validation wiring, SQL-state batch payload), then delete
   `erp-grid.component.*` and the showcase's dependency on it.
2. Re-export `grid-config.model` / `column-builder` / `grid-defaults` from `index.ts`, or
   finish migrating both components onto `grid-types.ts` and delete the old model.
3. Rewrite the AG Grid section of CLAUDE.md against the code that actually ships. Do this
   **last**, once the API is settled, and copy real snippets out of the showcase.
4. Migrate SYS1104's hand-rolled `<table>` to the grid — it is the reference the doc claims.

---

## P1 — Theming is not wired at all

**What.** `erp-grid.component.html` puts `class="ag-theme-alpine"` on `<ag-grid-angular>`,
but **no AG Grid CSS is loaded anywhere** — `apps/web-client/project.json` `styles` lists
only tailwind, the CDK overlay sheet and `styles.scss`. Verified live: iterating
`document.styleSheets` finds **0** rules matching `ag-theme-alpine`. **[live]**

**Why it matters.** In v33+ styling comes from the Theming API (JS), so the grid silently
renders in default Quartz and the legacy class does nothing. The grid therefore ignores the
app's design tokens entirely and has no dark-mode story, while the class misleads everyone
into thinking a theme is applied.

**How to fix.** Delete the `ag-theme-alpine` class and adopt the Theming API in
`provide-erp-grid.ts`:

```ts
import { themeQuartz } from 'ag-grid-community';

export const aidlyGridTheme = themeQuartz.withParams({
  accentColor: 'var(--bs-primary)',
  backgroundColor: 'var(--bs-body-bg)',
  foregroundColor: 'var(--bs-body-color)',
  borderColor: 'var(--bs-border-color)',
  headerBackgroundColor: 'var(--bs-tertiary-bg)',
  fontFamily: 'inherit',
  fontSize: '13px',
});
```

Then set `theme: aidlyGridTheme` in `DEFAULT_ERP_GRID_OPTIONS`. Because the params read CSS
custom properties, dark mode follows the app's existing token switch for free. Do **not**
reintroduce `theme: 'legacy'` + the legacy stylesheets — that is the dead end.

---

## P1 — Currency formatting bypasses the app's decimal contract

**What.** The `currency` branch of `buildColumns` formats with a hardcoded
`toLocaleString('en-US', …)`. **[code]**

**Why it matters.** CLAUDE.md marks this CRITICAL: monetary values must go through
`AppDecimalPipe` / `CommonDataService`, which carry the active currency's precision loaded
at bootstrap. A grid that hardcodes `en-US` and its own precision will disagree with every
form field beside it.

**How to fix.** Inject `AppDecimalPipe` (or the currency service behind it) into the grid
component and use it in the `valueFormatter` for `currency` / `decimal` / `number`. Delete
the `precision ?? 2` fallbacks — precision is the service's job, never the column's.

---

## P2 — Export is CSV wearing an Excel label

**What.** `GridExportService.exportToExcel()` calls `api.exportDataAsCsv()`; the toolbar
button reads "Excel". **[code]**

**Why it matters.** Real `exportDataAsExcel` is Enterprise-only, so CSV is the correct
fallback — but the method name and button lie, and the app already owns a
`TableExportService` that CLAUDE.md says all exports must route through.

**How to fix.** Rename to `exportToCsv`, relabel the button, and delegate to
`TableExportService` so grid exports match common-table exports (same headers, same
formatting, same file naming).

---

## P2 — Layout persistence loses work and omits filters

**What.** `GridStateService.saveState` is called only from `ngOnDestroy`, and saves only
`api.getColumnState()`. **[code]**

**Why it matters.** A browser refresh or tab close never persists — `ngOnDestroy` does not
run. And `getColumnState()` carries width/order/visibility/sort but **not** the filter
model, so the "filters persist" behaviour CLAUDE.md advertises does not exist.

**How to fix.** Save on the events instead of on destroy — `columnMoved`, `columnResized`
(debounced), `columnVisible`, `sortChanged`, `filterChanged` — and persist
`{ columnState, filterModel }`, restoring both in `onGridReady`. Keep the `gridKey`
contract: it is permanent, and renaming one silently discards every user's saved layout.

---

## P2 — Dead config surface

**What.** Declared but never read: `GridConfig.editMode`, `selectionMode`, `pageSize`,
`pageSizeOptions`, `showPagination`, `enableExcelCopyPaste`, `autoCalculateFooters`;
`GridColumnConfig.readOnly`; `GridToolbarConfig.showDuplicate`, `showImport`,
`showColumnChooser`, `showDensity`. `GridClipboardService` has zero callers. **[code]**

(`density`, `bottomTotal`/`footerLabel`/`footerValue`, `align`, `cellClassRules`,
`validation` and `onChange` **are** now wired — see "Recently fixed" below.)

**Why it matters.** A half-declared API is worse than an absent one: forms set
`showPagination: true` and get nothing, with no error.

**How to fix.** For each field, either wire it or delete it. Concretely:
- `selectionMode` → map to `rowSelection.mode` (`'single'`/`'multiRow'`) and honour `'none'`.
- `editMode` → `'row'` sets `editType: 'fullRow'`; `'cell'` leaves it unset.
- `pageSize` / `showPagination` → `pagination` + `paginationPageSize` + `paginationPageSizeSelector`.
- `enableExcelCopyPaste` → Community has no clipboard module **[src]**; either implement a
  paste handler on top of `GridClipboardService.parseClipboardTsv` or delete the flag and
  the service. Do not leave it declared.
- `showDuplicate` / `showImport` / `showColumnChooser` / `showDensity` → build the toolbar
  buttons (the density one now has real heights to switch between) or drop them.

---

## P2 — `duplicateRow` hardcodes the primary key

**What.** `GridApiService.duplicateRow` does `delete copyData['id']` and
`delete copyData['row_version']`, while the PK field is configurable via
`GridConfig.rowIdField`. **[code]**

**Why it matters.** Duplicating a row keyed on `branch_no` / `item_no` copies the original
PK into the new row, so the `inserted[]` batch payload ships a PK the backend must reject or
will silently update.

**How to fix.** Pass `rowIdField` into the service (it already receives it in `setRows`) and
strip that field plus `row_version`.

---

## P3 — Industry-standard gaps

Ordered by value for an ERP:

1. **No loading / empty overlays.** A pending fetch looks identical to "no data". Add a
   `loading` input mapped to `api.setGridOption('loading', …)` and an
   `overlayNoRowsTemplate`.
2. **No row-state styling.** Insert/Update/Delete are tracked and shown only in the pinned
   status column. Standard practice is `getRowClass` tinting new/modified rows, which makes
   an unsaved grid readable at a glance.
3. **Client-side row model only.** Every grid loads all rows. Community includes the
   Infinite Row Model **[src]** — wire it (or pagination) before any ledger-sized dataset
   lands.
4. **No column groups.** `GridColumnConfig` has no `children`; unavoidable for wide ERP
   grids.
5. **No i18n.** The app runs `@ngx-translate`, but the toolbar/status-bar strings are
   hardcoded English and AG Grid's `localeText` is unwired.
6. **Zero tests.** No `*.spec.ts` anywhere under `shared/ag-grid`. `getDirtyRows()` decides
   what gets INSERTed/UPDATEd/DELETEd — it deserves a suite of its own, as does the
   validation pipeline.
7. **`AllCommunityModule`** registers every community feature. v33+ best practice is
   registering only the modules used, for tree-shaking.
8. **`tabToNextCell`** in `grid-defaults.ts` reimplements the default and does nothing. ERP
   grids normally use this hook to wrap to the next row and append a row on Tab from the
   last cell.

---

## Recently fixed (do not redo)

- `getRowId` is set — rows are no longer destroyed/recreated on every data change.
- `suppressRowClickSelection` (deprecated v32.2 **[src]**) removed from the defaults.
- Column `onChange`, `validation` (`__errors` + red cell classes) and numeric right-align
  are wired.
- **Select/lookup cell editor** — see the pitfalls section below.
- **Pinned bottom totals row** — `.sum()`, `.footer('Total')`, `.footerValue(fn)` on the
  column builder; recomputed from `forEachNodeAfterFilter`, so totals follow the active
  filter. Verified live: filtering to one row moved the totals to that row's values.
- **Density** — `GridConfig.density` maps to `GRID_DENSITY_HEIGHTS` in `grid-defaults.ts`
  (compact 30/26, comfortable 34/30, spacious 42/38). Default dropped the header from
  49px → 34px and rows from 41px → 30px.

⚠️ All of the above landed in **`<app-erp-grid>` only**. `<app-grid>` still needs the
pinned-totals row and density. Port them as part of the P0 consolidation.

---

## Hard-won gotchas — read before touching the select editor

These cost real debugging time and are not discoverable from the docs.

1. **The dropdown panel must NOT be appended to `body`.**
   `stopEditingWhenCellsLoseFocus: true` makes AG Grid build the editor as a *modal* popup
   (`main.cjs.js`: `const useModelPopup = gos.get("stopEditingWhenCellsLoseFocus")`)
   **[src]**, which tears the editor down on any mousedown **outside the popup element**.
   With the panel on `body`, clicking an option destroys the editor before the option's own
   click handler runs: the list closes and the selection is silently dropped. **The tell is
   that keyboard Enter works and the mouse does not.** **[live]**

2. **You cannot turn `appendTo` off with the input.** `app.component.ts` sets
   `ngSelectConfig.appendTo = 'body'` app-wide, and ng-select resolves it as
   `@let appendToValue = appendTo() || config.appendTo` **[src]** — a `||`, so `appendTo=""`
   falls straight back to `'body'`. The only override is a component-scoped `NgSelectConfig`
   provider that clones the global and clears the field (see
   `provideInlineDropdownConfig` in `select-cell-editor.component.ts`).

3. **Keeping the panel inline costs a z-index fix.** `.ag-header` is
   `position:absolute; z-index:1`, while `.ag-popup-editor` and `.ng-dropdown-panel` are
   both `z-index:auto` **[src]** — so an upward-opening list paints *behind* the column
   headers. Fixed by lifting `.ag-popup-editor:has(app-select-cell-editor)` to `z-index:10`.

4. **v36 renamed the pinned-row CSS classes.** It is `.ag-grid-pinned-bottom-rows` and
   `.ag-row-pinned`, **not** `.ag-floating-bottom`. The old selector matches nothing and
   fails silently. **[live]**

5. **`cellRendererSelector` returning `undefined` falls back to `cellRenderer`.** To
   suppress a renderer on the pinned footer row you must omit the `cellRenderer` property
   entirely and use the selector alone — otherwise the status badge and action buttons
   render on the totals row. **[live]**

6. **Signal inputs are unreadable in field initializers** (`NG8118`). Anything derived from
   `config()` — density, footer detection — belongs in `ngOnInit`, which still runs before
   the template instantiates `<ag-grid-angular>`.

7. **`modelUpdated` fires during grid init, before `gridReady` assigns `gridApi`.** Handlers
   that depend on the api must take it from the event (`event.api`), not from the field, or
   the first firing is silently skipped. **[live]**

8. **AG Grid fails silently.** A bad column type, a missing height, a stale class name — all
   render a blank or wrong grid with nothing in the console. When something looks wrong,
   inspect the DOM; do not reason from the source alone.

---

## Suggested order of work

1. P0 consolidation (one component, one model, honest CLAUDE.md).
2. Theming API + currency pipe — these two are what make the grid look like it belongs.
3. State persistence on events + filter model.
4. Wire-or-delete the dead config surface.
5. Overlays, row-state styling, pagination.
6. Tests for `getDirtyRows()` and the validation pipeline.
