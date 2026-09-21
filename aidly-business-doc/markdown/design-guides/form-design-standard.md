# Aidly ERP — Global Form Design Standard

Reference implementation: **FIN_1001 Chart of Accounts**
(`features/fin/forms/fin1001`). Every ERP form follows this architecture.
Established 2026-07-20 during the Tailwind migration; visuals match the live
Bootstrap-era design 1:1 (verified against aidlyerp.infoaidtech.net by
computed-style diff).

---

## 1. The rule that governs everything: NO per-form CSS

A form ships **two files**: `*.component.html` and `*.component.ts`
(plus its `services/`). The only allowed style on the component is the host
sizing block:

```ts
styles: [`:host { display: block; height: 100%; }`]
```

All styling comes from, in order of preference:

1. **Shared building-block components** (`shared/components/form-ui`,
   `page-form-layout`, `common-table`, `sidebar-pagination`)
2. **Tailwind utilities** in the template (layout/spacing only)
3. **Global form classes** (`assets/styles/_form-page.scss`,
   `_components.css`) — the visual tokens of the system

If a form seems to need custom CSS, the fix belongs in a shared component or
the global layer — never in the form.

## 2. Standard page hierarchy

```
<app-page-form-layout>                     page shell: header + toolbar + split body
 ├─ [extraHeaderActions]  → extra .btn-action buttons (e.g. Print)
 ├─ [sidebarFilters]      → optional ng-select filters
 ├─ [sidebarList]         → master list rows (.list-group-item pattern)
 ├─ [sidebarFooter]       → <app-sidebar-pagination [pager]="model.pager">
 └─ [formContent]
     └─ <form [formGroup]>
         └─ <app-form-section title icon>        one per titled card
             ├─ <app-form-field label [span]>    labeled control cell
             │   └─ <input class="form-control"> | <ng-select> | …
             └─ <app-check-field label text [span] formControlName>
```

## 3. Building blocks (`shared/components/form-ui`)

Exported through `SharedModule` — forms need no extra imports.

### `<app-form-section title icon [flush]>`
Titled section card (`.form-section-card`): header with icon + title,
collapsible chevron (auto via `CollapsibleCardDirective`), body = **12-column
grid** (`grid grid-cols-12 gap-2`). `flush` renders a `p-0` body with no grid
— use it to host `<app-common-table>` (SYS1003 pattern). Header extras project
via `sectionActions`.

### `<app-form-field label for required [span] [xlSpan] [info]>`
Grid cell with `.field-label` (+ red `*` when `required`) above the projected
control. `span` = columns of 12 from the `md` breakpoint (mirrors old
`col-md-*`); full-width below `md`. Validation messages appear automatically —
`ValidationMessageDirective` attaches to any projected `formControlName`
control and renders the 11px danger message under the field.

### `<app-check-field label text [span] formControlName>`
Bordered 36px checkbox (`.custom--checkbox`) as a **ControlValueAccessor**.
Emits `1`/`0` (DB-first SMALLINT contract; accepts `true/false` on write).
`label` = field name above; `text` = short affirmative inside
(top `Status` → inner `Active`).

### Example (the whole CoA form body)

```html
<app-form-section title="Account" icon="menu_book">
  <app-form-field label="Account Code" for="account_code" required [span]="3">
    <input id="account_code" formControlName="account_code" class="form-control" />
  </app-form-field>
  <app-form-field label="Account Group" for="account_group_no" required [span]="4">
    <ng-select id="account_group_no" formControlName="account_group_no" [items]="model.groups()" />
  </app-form-field>
  <app-check-field label="Status" text="Active" [span]="2" formControlName="is_active" />
</app-form-section>
```

## 4. Design tokens (live-verified values)

| Element | Spec |
|---|---|
| Page card | radius 10px, 1px `--border-color`, fills shell height |
| Page header | ~60px, padding 4px 8px, icon box 30px (radius 14, `--indigo-g`), title 15px/700, subtitle `small` `--text-secondary` |
| Action buttons | `.btn-action` 32px h, radius 8, 17px icons, 12px/600 label; save/new = gradients, reset/delete = soft tints |
| Page body | 8px padding, 4px gap between cards |
| Section card | radius 10, `--card-shadow`, 4px bottom margin; header 8px 12px, title 15px/700; body 8px 12px |
| Field grid | 12 columns, 8px gap (`gap-2`) both axes |
| Label | 13px/600 `--text-secondary`, 5px gap to control, red `*` for required |
| Text input | `.form-control` ≈35px (6px 12px padding, 14.5px/500), radius 6, `--input-border` |
| ng-select / checkbox | 36px, radius 6, same border/focus tokens |
| Focus state | `--input-focus-border` + `--input-focus-ring` ring (all controls share it) |
| Validation | 11px `--danger` message under the control, `.is-invalid` on the control |
| Sidebar row | radius 10, 9px 11px padding, corner status dot, indigo active accent |
| Breakpoints | Bootstrap-compatible: sm 576 / md 768 / lg 992 / xl 1200 (mapped in `tailwind.css`) |

Colors, dark mode, and fonts come exclusively from `_themes.scss` CSS
variables — never hard-code hex in forms.

## 4a. The `[formContent]` wrapper — owned by the layout

The page shell styles the projected `[formContent]` element itself
(`display:flex; flex-direction:column; flex:1 1 auto; min-height:0`), so it
always fills `.page-card-body` and the page scrolls **once**. Forms decide only
what goes inside it.

- ✅ `<div formContent class="h-full flex flex-col">` (Tailwind, or no class at all)
- ❌ `h-100 d-flex flex-column` — dead Bootstrap classes. They resolve to
  nothing, leaving a plain block that can't fill its parent; combined with an
  `overflow-y-auto` it produces a **nested scrollbar plus dead white space**
  under the card. All 36 forms were swept on 2026-07-21.

## 4b. Sidebar filters

Project filter controls as **direct children** of `[sidebarFilters]` — the page
shell lays them out on a 2-column grid (8px gap; blowout-guarded with
`minmax(0,1fr)`). Add Tailwind `col-span-2` on a control that should span the
full row. No wrapper `<div>`s, no per-form spacing classes. Custom-template
table cells receive `{ $implicit: row, column }` — declare `let-row`, never
`let-row="row"`.

## 5. Tables and tabs

- Any tabular data → `<app-form-section flush>` + `<app-common-table>`
  (SYS1003 Accounting Periods remains the canonical table).

**Table height contract** — every table always has a definite height:

| Config passed | Result |
|---|---|
| *(nothing)* | **20vh** — the standard table |
| `height: '<any>'` | exactly that |
| `maxHeight: '100%'` | fills the flex parent (fill-layout idiom; parent must be a flex column with `min-height: 0`) |
| `minHeight: '<any>'` | raises the floor **without** opting out of the default |

Never pass viewport math (`calc(100vh - 320px)`) — see §"Content Area Height"
in CLAUDE.md. The card is a strict flex column: toolbar and paginator are fixed
bands, only the scroll area flexes, so the sticky totals row and the paginator
can never be clipped.
- Page-level tabs → `.form-tab-btn` bar (see CLAUDE.md “Tabs”); project into
  `tabBarContent`.

## 6. Cascade rules (why utilities sometimes need `!`)

Tailwind v4 utilities are **layered**; the legacy global SCSS is **unlayered**
and wins ties. When a utility must override a global class on the *same*
element, use the important form (`!p-2`, `!gap-2`, `!text-indigo`). Needed for:
mat-icon colors/sizes, and any spacing override on elements that global
classes already pad (e.g. `.page-card-body`). Never work around this with
per-form CSS.

## 7. Checklist for a new form

1. Copy `fin1001` as the skeleton (model/data services + component).
2. Header: `app-page-form-layout` inputs + permission-driven show flags.
3. Sidebar: `.list-group-item` rows from `model.pager.paged()`, corner status
   dot, `<app-sidebar-pagination>` footer (CLAUDE.md list rules apply).
4. Body: one `<app-form-section>` per card; fields as
   `<app-form-field>` / `<app-check-field>` with `[span]`s summing to 12 per
   visual row.
5. Selects: options from module constants; GL accounts formatted `Code Name`.
6. No CSS file. If you wrote one, move it to the shared layer or delete it.
