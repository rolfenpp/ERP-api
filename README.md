# ERP System

This repository contains an **Enterprise Resource Planning (ERP) system**.  
It is designed to help manage and integrate core business processes such as:

- Inventory and stock management
- Customer and sales tracking
- Purchasing and suppliers
- Accounting and finance
- Reporting and analytics

The goal of this ERP system is to provide a **centralized platform** that improves efficiency, data consistency, and decision-making across different areas of a business.

---

## Features (planned or implemented)
- Modular design for different business functions
- Central database for unified data
- Web-based interface for accessibility
- Secure authentication and role-based access

---

## Onboarding Flow (Multi-tenant)

1. Register a company and bootstrap its Admin:
   - POST `companies/register` with `{ name, adminEmail, adminPassword }`
   - Response includes a JWT for the Admin. This Admin is scoped to the new company.

2. Admin adds users to their company:
   - POST `account/users` (with password) or `account/users/basic` (no password)
   - New users are automatically assigned to the Admin's `companyId`.

3. Admin invites user to activate:
   - POST `account/users/{userId}/invite` to generate activation (email confirm + reset) tokens
   - User completes activation via POST `account/activate` with tokens and sets their password

Notes:
- Public "bare" registration is disabled. Use `companies/register` to create tenants.
- Google OAuth sign-in is allowed only for existing users; it will not auto-create accounts.
- All data access is tenant-scoped via JWT `companyId` claim and EF Core global query filters.
