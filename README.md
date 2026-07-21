# Virto Commerce Sales Rep Module

The Sales Rep module turns selected users into sales representatives who serve a defined set of customer organizations. It provides a back-office application for administrators to create, assign and manage reps, and a storefront GraphQL (X-API) surface that lets a B2B storefront show the reps supporting an organization, the customers a rep serves, their orders, and dashboard statistics (order purchases, carts/projects and customer counters).

<!-- TODO: add a hero screenshot of the Sales Reps app once available, e.g.
<img width="1902" alt="Sales Reps admin app" src="https://github.com/user-attachments/assets/..." /> -->

## Key features

* Manage sales representatives from a dedicated back-office app — create, edit, block/unblock and delete
* Assign each rep the customer organizations they serve — globally or per organization
* Manage the rep's login account: store, password and lockout
* Model a rep from existing platform data (a contact, a login account and a role) with no new database tables
* Let buyers see the sales reps supporting their organization
* Let reps see the customers they serve, each with the rep's latest order for that customer
* Show a customer information card — organization, primary contact and account type
* List and filter the orders a rep created for their customers
* Show dashboard **statistics** for a rep — order purchases, average order value and order counts, with period-over-period comparison, converted to one currency
* Add cart/project statistics (e.g. *active projects*) and *my-customers* counters (customers who ordered in a period, new customers)
* Show inline per-customer purchase figures on the customers list (YTD / prior-year totals, order counts, first/last order) via a batched per-row statistics field — no N+1
* Filter the orders/customers lists and every statistics block by a single, optional, server-defined **filter rule** (aggregated order statuses / cart kinds / customer segments), and order the lists by a server-defined **sort rule** (e.g. *recent*, *my last orders*, *ytd purchases*, *name*) — the storefront sends one rule name per axis and the server maps it; both are overridable per project
* Rank a rep's **top-selling products** (dashboard + per-customer) by units or revenue over a period, optionally within a product category
* Scope the orders list to an optional created-date **period**
* Toggle the storefront Sales Rep UI per store

## Screenshots

<!-- TODO: add back-office screenshots (Sales Reps list, details blade) as GitHub asset images, e.g.
<img width="822" alt="Sales Reps list" src="https://github.com/user-attachments/assets/..." />

---

<img width="847" alt="Sales Rep details" src="https://github.com/user-attachments/assets/..." /> -->

## XAPI Specification

The storefront queries are exposed on a dedicated scoped schema at `POST /graphql/sales-rep` (with a GraphiQL UI at `/ui/graphiql/sales-rep`). Every query requires an authenticated caller and is store- and membership-scoped, so a rep only sees the customers they serve and a buyer only sees their own reps.

> **Authentication.** Every query needs a bearer token. When the rep's login account is **store-bound**, the `POST /connect/token` password grant must include the `storeId` form parameter (e.g. `storeId=B2B-store`) — otherwise the grant fails with `400 invalid_grant`.

### Query

The sales reps supporting the caller's organization:

```graphql
{
  customerSalesReps(storeId: "B2B-store", first: 10) {
    totalCount
    items {
      id
      fullName
      about
      photoUrl
      emails
      phones
    }
  }
}
```

---

The customer organizations the current rep serves, each with the rep's most recent order for that customer and inline purchase figures. Supports keyword search and paging, an optional **sort rule** (`sort` — a `salesRepCustomerSortRules` name; default *my last orders*), and an optional **filter rule** (`filter` — a `salesRepCustomerFilterRules` segment; default *All* = every served customer). Request one or more aliased `orderStatistics(from, to)` blocks per row for the YTD / prior-year purchase columns:

```graphql
{
  salesRepCustomers(storeId: "B2B-store", cultureName: "en-US", first: 20, sort: "my-last-orders") {
    totalCount
    items {
      organizationId
      organizationName
      accountId       # external/display account id (Member.OuterId); null when unset
      accountType     # business category, e.g. "Garden Center"
      iconUrl
      address {
        line1
        city
        regionName
        postalCode
        countryCode
      }
      # Inline per-row purchase columns — one aggregate query per distinct range for the whole page (no N+1).
      # Defaults to the store's currency (then the platform primary); pass currencyCode to override per column.
      ytd: orderStatistics(from: "2026-01-01T00:00:00Z", to: "2027-01-01T00:00:00Z") {
        total { amount formattedAmount }
        count
      }
      lastYear: orderStatistics(from: "2025-01-01T00:00:00Z", to: "2026-01-01T00:00:00Z") {
        total { amount formattedAmount }
      }
      lastOrder {
        number
        createdDate
        status
        statusDisplayValue
        total {
          amount
          formattedAmount
          currency {
            code
          }
        }
        itemsCount      # number of distinct line items
        itemsQuantity   # total units (sum of line-item quantities)
      }
    }
  }
}
```

The `address` is structured (the default organization address, or its first) — the storefront formats it for display, e.g. `City, Region`. It's loaded only when selected: requesting `address` loads the organization's addresses; omit it and only scalar columns (`organizationName`, `iconUrl`, …) are read.

---

A single customer information card:

```graphql
{
  salesRepCustomer(organizationId: "7b8c...") {
    organizationId
    organizationName
    accountType
    iconUrl
    phone
    address {
      line1
      city
      regionName
      postalCode
      countryCode
    }
    primaryContact {
      fullName
      emails
      phones
    }
  }
}
```

---

The orders the rep created for their customers, paged, ordered by an optional **sort rule** (`sort` — a `salesRepOrderSortRules` name; default *recent*) and filtered by an optional **filter rule** (`filter` — a `salesRepOrderFilterRules` status). Add `organizationId` to scope to one customer, and an optional `period` to scope by created date:

```graphql
{
  salesRepOrders(
    storeId: "B2B-store"
    filter: "New"
    sort: "recent"
    period: { from: "2026-05-01T00:00:00Z", to: "2026-06-01T00:00:00Z" }
    first: 20
  ) {
    totalCount
    items {
      id
      number
      organizationName
      createdDate
      status
      statusDisplayValue
      total {
        amount
        formattedAmount
        currency {
          code
        }
      }
      itemsCount      # number of distinct line items
      itemsQuantity   # total units (sum of line-item quantities)
    }
  }
}
```

---

#### Filter rules

Lists and statistics blocks are filtered by a single, optional **named filter rule**, not by raw statuses/types. The storefront reads the selectable rules from a discovery query and sends back one rule `name` in the unified `filter` argument; the server resolves it to the underlying filter — order statuses, a cart type/status set, a customer segment, or a product category — and a rule can be a composite (e.g. a business `"inactive"` → `Cancelled` + `Failed`). Omit `filter` for the baseline set (everything the rep may see, minus soft-deleted/prototype); an unrecognized name fails **closed** (no data), never "return everything". Rule sets are overridable per project.

Four rule domains, each with its own discovery query:

```graphql
{
  # orders list + order statistics
  salesRepOrderFilterRules(storeId: "B2B-store", cultureName: "en-US") { name localizedName }
  # cart / project statistics
  salesRepCartFilterRules(storeId: "B2B-store", cultureName: "en-US") { name localizedName }
  # customers list + "my customers" counts (customer segments; a single "All" baseline by default)
  salesRepCustomerFilterRules(storeId: "B2B-store", cultureName: "en-US") { name localizedName }
  # top sellers list (category badges; the store catalog's top-level non-hidden categories)
  salesRepTopSellerFilterRules(storeId: "B2B-store", cultureName: "en-US") { name localizedName }
}
```

---

#### Sort rules

The orders, customers and top-sellers lists are ordered by a single, optional **named sort rule** — a *separate axis* from the filter rules above (a filter chooses *which* records, a sort chooses their *order*), so the two are never crossed into one combinatorial list. The storefront reads the selectable orderings from a discovery query and sends back one rule `name` in the built-in `sort` argument; the server maps it to the actual ordering. Omit `sort` (or send an unknown name) and the domain's **default** ordering applies — a sort only reorders, so unlike a filter it never fails closed. `customerSalesReps` is exempt (a plain list). Rule sets are overridable per project.

```graphql
{
  # orders list — default "recent" (newest first)
  salesRepOrderSortRules(storeId: "B2B-store", cultureName: "en-US") { name localizedName }
  # customers list — default "my-last-orders"; also "ytd-purchases" and "name"
  salesRepCustomerSortRules(storeId: "B2B-store", cultureName: "en-US") { name localizedName }
  # top sellers list — default "by-units"; also "by-revenue"
  salesRepTopSellerSortRules(storeId: "B2B-store", cultureName: "en-US") { name localizedName }
}
```

The customers list's `my-last-orders` and `ytd-purchases` orderings are **order-derived** — they can't be expressed as a member column, so the server ranks the served organizations by the rep's own per-organization order aggregate (creator-scoped, the same data-isolation rule) and pages the result; `name` is a plain member-column sort.

---

#### Order statistics

Aggregated order purchases for the rep — omit `organizationId` for the cross-customer dashboard, or pass it to scope to one customer. Request any number of **aliased** `period(from, to)` blocks and `comparison(current, previous)` blocks in one query; a per-request loader coalesces them, so a range used by both a period and a comparison is aggregated once. Money fields expose `amount` + `formattedAmount`; each block takes an optional `filter` (see above).

```graphql
{
  salesRepCustomerOrderStatistics(organizationId: "7b8c...", currencyCode: "USD", cultureName: "en-US") {
    currencyCode
    ytd: period(from: "2026-01-01T00:00:00Z", to: "2027-01-01T00:00:00Z") {
      total { amount formattedAmount }
      count
      average { amount formattedAmount }
      lastOrderDate
    }
    sinceDate: period {   # omit both bounds → all-time; firstOrderDate is the "customer since" date
      firstOrderDate
      lastOrderDate
    }
    newOrders: period(from: "2026-01-01T00:00:00Z", to: "2027-01-01T00:00:00Z", filter: "New") {
      total { amount }
      count
    }
    ytdVsLastYear: comparison(
      current:  { from: "2026-01-01T00:00:00Z", to: "2027-01-01T00:00:00Z" }
      previous: { from: "2025-01-01T00:00:00Z", to: "2026-01-01T00:00:00Z" }
    ) {
      totalChange { amount formattedAmount }
      totalChangePercent
      countChange
      countChangePercent
    }
  }
}
```

---

#### Cart / project statistics

The same shape for carts/projects (dashboard *Active Projects*). `filter` here is a cart *kind*; the built-in default is `"active-carts"` (non-empty carts that are **not** wishlists) — and `count` is the primary metric:

```graphql
{
  salesRepCustomerCartStatistics(currencyCode: "USD", cultureName: "en-US") {
    activeCarts: period(from: "2026-01-01T00:00:00Z", to: "2027-01-01T00:00:00Z", filter: "active-carts") {
      count
      total { amount formattedAmount }
      lastCartDate
    }
  }
}
```

---

#### My customers

Customer counters for the dashboard *My Customers* card — how many customers the rep serves, how many ordered in a period, and how many are new in it (with optional period-over-period comparison). Each `period`/`comparison` also takes an optional `filter` — a customer segment from `salesRepCustomerFilterRules` (a single *All* baseline by default):

```graphql
{
  salesRepCustomerCounts {
    assignedCustomers
    thisMonth: period(from: "2026-05-01T00:00:00Z", to: "2026-06-01T00:00:00Z") {
      orderingCustomers
      newCustomers
    }
    monthOverMonth: comparison(
      current:  { from: "2026-05-01T00:00:00Z", to: "2026-06-01T00:00:00Z" }
      previous: { from: "2026-04-01T00:00:00Z", to: "2026-05-01T00:00:00Z" }
    ) {
      orderingCustomersChange
      orderingCustomersChangePercent
      newCustomersChange
    }
  }
}
```

---

#### Top sellers

The rep's top-selling products (dashboard *Top Sellers*, and per-customer when an `organizationId` is passed). Ranked over an optional `period` by a **sort rule** (`salesRepTopSellerSortRules`; default `by-units`, or `by-revenue`), returning the top `take` (default 5, max 10). An optional category `filter` (a `salesRepTopSellerFilterRules` name — a top-level category) restricts the ranking to that category's subtree. Each row's name/sku/image/category come straight from the order line-item snapshot (no catalog read); `revenue` is Money.

```graphql
{
  salesRepTopSellers(
    storeId: "B2B-store"
    sort: "by-units"
    period: { from: "2026-01-01T00:00:00Z", to: "2027-01-01T00:00:00Z" }
    take: 5
    cultureName: "en-US"
    # optional: organizationId (scope to one customer); filter: "<category id>" (restrict to a category subtree)
  ) {
    rank
    productId
    name
    sku
    imageUrl
    units
    revenue { amount formattedAmount currency { code } }
  }
}
```

All statistics and rankings obey the same **data-isolation rule** as the rest of the module: they count only the data the calling rep *created* (their own orders/carts), within the organizations they serve — never another rep's or employee's data.

## How it works

A sales rep is not a new entity — the module composes three pieces of existing platform data, so it owns **no database tables** and adds **no EF migrations**:

* a **Contact** (`Member`, from the Customer module) — the rep's profile; its id is the canonical id of a sales rep;
* an **ApplicationUser** (platform security) — the login account;
* a **role granting `sales-rep:access`** — assigned globally (serves everyone) and/or per organization via `OrganizationMembership` (serves specific customers).

A user *is* a sales rep whenever they hold the `sales-rep:access` permission — never by matching a role id or name. Searching for reps returns the union of users holding the global role and users holding the role through a per-organization membership.

```mermaid
graph LR
    C["Contact / Member<br/>(profile)"]
    U["ApplicationUser<br/>(login account)"]
    R["Role → sales-rep:access"]
    O1["Organization A"]
    O2["Organization B"]

    C -- MemberId --- U
    U -- global role --> R
    U -- OrganizationMembership --> O1
    U -- OrganizationMembership --> O2
    O1 -. per-org role .-> R
    O2 -. per-org role .-> R
```

### Statistics, filter rules and sort rules

The dashboard numbers are **aggregated in the database**: the module reads the Orders and Cart stores directly (grouped `SUM` / `COUNT` / `MAX`) instead of loading rows into memory, then converts every order/cart currency to the requested one at current rates. The requested currency is resolved once per query by a shared policy (`ISalesRepCurrencyResolver`, used by every money-bearing query): an explicit `currencyCode` argument if given, else the store's default currency, else the platform primary. Every statistics query is scoped two ways — to the organizations the rep serves (membership) **and** to the data the rep *created* (their own orders/carts) — the same data-isolation rule the rest of the module follows.

**Filter rules** are the single, server-owned vocabulary for "which records count". A rule has a stable `name` and resolves to the underlying filter — order statuses, a cart type/status set, or a customer segment — as an overridable mapping (`IFilterRuleResolver`), applied as one optional `filter` argument (omit → the baseline set; unknown name → fail closed). Within a domain the **same resolver drives every reader** — the orders list and the order statistics; the customers list and the "my customers" counts — so a filtered list and its matching statistic always reconcile (a component test asserts `salesRepOrders.totalCount == statistics.count` for a given rule). Extensibility:

* **Add/recompose rules** — register a replacement resolver (`ISalesRepOrderFilterRuleResolver` / `ISalesRepCartFilterRuleResolver` / `ISalesRepCustomerFilterRuleResolver` / `ISalesRepTopSellerFilterRuleResolver`); the last registration wins. Customer segments ship with a single **All** baseline (passthrough); the seam is there for projects to add real segments.
* **A rule the standard criteria can't express** (e.g. *"stale, or item-less"* orders, or an *"active"* customer segment) — the resolver applies onto the reader's criteria, and each reader exposes a seam to add the predicate: a `BuildQuery` override on the statistics/counts services, or narrowing the members search (`ObjectIds`) for the customers list. Wire it for every reader in the domain so they stay consistent.

**Sort rules** are the parallel axis for *ordering* (`ISortRuleResolver` — `ISalesRepOrderSortRuleResolver` maps a rule to the order search's sort expression; `ISalesRepCustomerSortRuleResolver` maps it to a spec; `ISalesRepTopSellerSortRuleResolver` maps it to the Top Sellers ranking metric). Kept a *separate* input from filter rules, so a domain's *N* filters and its handful of orderings never multiply into one combinatorial list. A sort only reorders, so an unknown/empty selection resolves to the domain **default** — it never fails closed. The customers list's order-derived orderings (*my last orders*, *ytd purchases*) can't be a member column, so the handler ranks the served organizations by the rep's per-organization order aggregate — one grouped query (`GetStatisticsByOrganizationAsync`), the same aggregate that backs the inline per-row purchase columns.

**Top Sellers** is an *orders-only* ranking, **aggregated in the database** like the statistics above: it groups the rep's own order line items by product with `SUM` (units = Σ quantity, revenue = Σ price × quantity) straight from the Orders store — returning one row per product/currency instead of loading raw line items — then folds a currency mix to the requested currency in memory. A line item is a self-contained snapshot (name / sku / image / category are denormalized on it), so the ranking and the row display need no catalog read. Its one catalog touch is the category badges (`ISalesRepTopSellerFilterRuleResolver`): the store catalog's top-level non-hidden categories, and a selected badge expands to its subtree of category ids that the ranking then filters on.

## Administration

The module ships an embedded VC-Shell application (menu title **Sales Reps**) with a Sales Reps list plus supporting views (**Blocked**, **Not assigned**, **Organizations**, **Not assigned organizations**) and a details blade covering the whole aggregate: **Account** (login email, password, store, role), **Profile** (name, salutation, birth date, time zone, language, currency, about), **Contact methods** (emails, phones, addresses), and **Served organizations** (multi-select), with **Block / Unblock** actions.

It is backed by a REST API under `/api/sales-rep`. Managing a rep is a customer-management action, so endpoints reuse existing permissions — the Customer module's member permissions for the profile and platform security permissions for the account (exactly as the customer member-detail *Accounts* widget does):

| Method & route | Purpose | Permissions |
|----------------|---------|-------------|
| `POST /api/sales-rep/search` | Search sales reps (global ∪ per-org). | `customer:read` |
| `GET /api/sales-rep/roles` | Roles granting `sales-rep:access` (seeds a default if none). | `customer:read` |
| `GET /api/sales-rep/{id}` | Get a rep aggregate by contact id. | `customer:read` |
| `POST /api/sales-rep` | Create a rep (contact + account + memberships). | `customer:create` + `platform:security:create` |
| `PUT /api/sales-rep` | Update a rep (profile + account + inline password). | `customer:update` + `platform:security:update` |
| `DELETE /api/sales-rep?ids=` | Delete reps; cascades to the account. | `customer:delete` + `platform:security:delete` |
| `POST /api/sales-rep/{id}/block` | Lock the rep's account. | `platform:security:update` |
| `POST /api/sales-rep/{id}/unblock` | Unlock the rep's account. | `platform:security:update` |
| `POST /api/sales-rep/{id}/password` | Set a new account password. | `platform:security:update` |

Full REST documentation is browsable through Swagger on any running platform instance at `https://{platform-host}/docs/index.html?urls.primaryName=VirtoCommerce.SalesRep`.

## Permissions

| Permission | Meaning |
|------------|---------|
| `sales-rep:access` | **Defines** a sales rep. Held by the rep via a role — globally and/or per organization. It is *not* an admin permission and does not gate the management API. |

The first time a rep is saved and no role yet grants `sales-rep:access`, the module seeds a default role named **"Sales Representative"**. Admins may freely rename or delete it — reps are identified by the permission, never by this role's id.

## Settings

| Setting | Scope | Type | Default | Purpose |
|---------|-------|------|---------|---------|
| `SalesRep.Enabled` | Per store (public) | Boolean | `true` | Toggles visibility of the Sales Rep UI on a store's storefront. |

`SalesRep.Enabled` is a presentation switch only — it does *not* gate the backend X-API or the data it returns (those stay secured by rep-membership scoping). It is registered for the `Store` type and marked public, so the storefront reads it from `store.settings.modules`.

## Dependencies

| Module | Why |
|--------|-----|
| `VirtoCommerce.Customer` | Contacts, organizations, `OrganizationMembership`, member permissions. |
| `VirtoCommerce.Orders` | Customer orders — search + hydration, and direct repository aggregation for order statistics. |
| `VirtoCommerce.Cart` | Shopping carts / wishlists — direct repository aggregation for cart (project) statistics. |
| `VirtoCommerce.Store` | Store scoping for accounts and X-API queries; per-store settings. |
| `VirtoCommerce.Catalog` | Top Sellers category badges — the store catalog's top-level categories and subtree expansion (`ICategorySearchService`). |
| `VirtoCommerce.Xapi` | GraphQL infrastructure for the scoped storefront schema. |

## Documentation

* Epic: [VCST-5142 — Sales Rep Hub](https://virtocommerce.atlassian.net/browse/VCST-5142)
* Pull requests:
  * [#1 — VCST-5293: Sales rep VC-Shell administration UI](https://github.com/VirtoCommerce/vc-module-sales-rep/pull/1)
  * [#2 — VCST-4907 / VCST-5304 / VCST-5308: X-API endpoints for customers and sales reps](https://github.com/VirtoCommerce/vc-module-sales-rep/pull/2)

> **Scope note.** The [Sales Rep Hub epic](https://virtocommerce.atlassian.net/browse/VCST-5142) describes the full storefront experience (KPI dashboards, customer tier badges, cross-customer order views, customer lists, etc.). This module delivers the backend foundation for it — the administration app, the REST API and the storefront X-API data surface, including the dashboard **statistics** data (order/cart/customer KPIs and filter rules). The complete storefront Sales Rep Hub UI (and features such as loyalty tiers, coupon tracking and list management) is built on top of this module in the frontend and is not part of this repository.

## References

* [Virto Commerce Documentation](https://docs.virtocommerce.org)
* [Customer module](https://github.com/VirtoCommerce/vc-module-customer)
* [Experience API (X-API)](https://github.com/VirtoCommerce/vc-module-x-api)

## License

Copyright (c) Virto Solutions LTD. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at

<https://virtocommerce.com/open-source-license>

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
