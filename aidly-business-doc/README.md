# Aidly ERP — Business Documentation Repository

This directory contains business logic, database schemas, user manuals, design guidelines, and mockups for the Aidly ERP system.

---

## 📁 Directory Structure & File Types

### 📄 `docx/` — Word Documents (`.docx`)
Contains official Word user manuals and module specification documents.
- `Aidly_ERP_Finance_Module_User_Manual.docx` — Finance User Manual
- `Aidly_ERP_HRM_Module_User_Manual.docx` — HRM User Manual
- `Aidly_ERP_SYS_Module_User_Manual.docx` — System Administration User Manual
- `HRM_Leave_module.docx` — HRM Leave Module Specifications
- `leave management HR doc.docx` — Leave Management Business Rules

---

### 💾 `sql/` — Database Schemas (`.sql`)
Contains raw PostgreSQL DDL scripts and schema migrations.
- `inv_schema_postgres.sql` — Inventory Module Schema Definition

---

### 🎨 `html/` — HTML Prototypes (`.html`)
Contains static wireframes and HTML UI redesign mockups.
- `hrm1006-redesign.html` — HRM Employee Promotion & Transfer Form UI Redesign

---

### 📝 `markdown/` — Markdown Specifications (`.md`)
Subdivided into targeted categories:

#### 1. `markdown/business-logic/` (`*-business.md`)
High-level business rules, module workflows, and domain models:
- `fin-business.md` — Finance & General Ledger Rules
- `hrm-business.md` — Human Resource Management Rules
- `inv-business.md` — Inventory Management Rules
- `pur-business.md` — Purchase & Procurement Rules
- `sal-business.md` — Sales & Order Processing Rules
- `sys-business.md` — System Administration & Security Rules

#### 2. `markdown/database-schemas/` (`*-db.md`)
Detailed table structures, column definitions, keys, and indexes:
- `fin-db.md`, `hrm-db.md`, `inv-db.md`, `pur-db.md`, `sal-db.md`, `sys-db.md`

#### 3. `markdown/form-business/` (`*-form-business.md`)
Form-by-form business logic, field behaviors, and validation rules:
- `fin-form-business.md`, `hrm-form-business.md`, `inv-form-business.md`, `pur-form-business.md`, `sal-form-business.md`, `sys-form-business.md`

#### 4. `markdown/user-manuals/` (`*-user-manual.md`)
Text-based module user guides:
- `fin-user-manual.md` — Finance User Manual

#### 5. `markdown/design-guides/`
UI/UX standards, design system rules, and technical implementation blueprints:
- `form-design-guide.md` — Comprehensive Form Layout & Component Guide
- `form-design-standard.md` — Form Design Standards & Micro-interactions
- `hrm-payroll-standard-plan.md` — Payroll Engine Standard Architecture Plan
