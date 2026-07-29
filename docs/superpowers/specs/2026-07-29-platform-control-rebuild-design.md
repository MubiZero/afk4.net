# Platform Control Rebuild Design

Date: 2026-07-29
Status: approved product design, awaiting implementation plan

## Purpose

Rebuild `AFK4.PlatformControl.Web` as the internal browser application for the
owner and support staff of the afk4.net SaaS platform. The new product must be
clearly separate from Organization Admin, which belongs to each customer
organization and handles day-to-day club operations.

This is a big-bang frontend replacement. AFK4 has no production customers or
customer data requiring compatibility with the existing Platform Control UI.
Existing backend capabilities and API contracts may be reused only after they
are checked against the new workflows.

## Product Boundary

Platform Control serves two internal roles:

- **Platform Owner** has access to organizations, plans, subscriptions,
  invoices, platform-managed client releases, global settings, and audit.
- **Platform Support** works with organizations, club health, organization-owner
  invitations, and support notes. It cannot manage plans, SaaS money, global
  releases, or platform settings.

These roles use one application and one component system. Navigation and
actions are derived from permissions, while the backend remains the
authorization boundary. Platform Control does not contain club-operator or
organization-owner workflows.

## Information Architecture

The application is organization-centered. Its primary navigation is:

1. Overview
2. Organizations
3. Plans and billing
4. Updates
5. Audit
6. Platform settings

The top bar contains global search, a critical-attention indicator, and the
current administrator menu. Theme and language controls live in the account
menu instead of occupying the primary workspace.

Global search finds organizations, clubs, organization owners, and exact
identifiers. The selected page, organization, tab, search query, filters, and
sort order are represented in the URL so refresh, browser history, and shared
internal links preserve context.

## Overview

The Overview is an operational attention queue, not a decorative metrics
dashboard. It prioritizes:

- organizations and clubs with health problems;
- overdue SaaS invoices or subscriptions requiring action;
- failed client update rollouts;
- organization-owner invitations that are expiring or stalled;
- recent security-sensitive and platform-administrator actions.

Counts and trends may provide context, but every signal must lead to the
affected resource or a concrete action. A richer cross-platform inbox remains
a later optimization and should be driven by real operating data.

## Organizations Workspace

The organizations list is a dense, filterable table. Each row exposes the
organization name and status, club and device health, owner, plan and payment
state, last activity, and unresolved attention signals. Search and filters
must remain usable with long names, Unicode content, and a large organization
set.

Selecting an organization opens a canonical full page with these sections:

1. Summary
2. Clubs
3. Owners and access
4. Subscription
5. Invoices
6. Support
7. Change history

A drawer may provide quick read-only inspection from a list, but it must not
become a second, incomplete organization workspace. Editing and multi-step
actions happen on the canonical organization page.

## Actions and Safety

Creating an organization, inviting its first owner, assigning a subscription,
and confirming initial readiness form one continuous provisioning journey.
Each completed step remains resumable if a later step fails.

Suspension, reactivation, subscription changes, invoice corrections, and
release actions wait for authoritative backend confirmation. Irreversible or
high-impact actions name their effects before confirmation, require a reason
where appropriate, and produce an audit entry. The UI does not expose controls
that the current role can never use.

Platform-wide package publication and rollout control exist only in Platform
Control. Organization Admin may show the installed or offered version and may
coordinate a safe local restart, but it cannot publish packages or define
platform rollout policy.

## Application Architecture

The existing Platform Control screen structure is replaced rather than
incrementally restyled. The rebuild introduces:

- a route-driven application shell and permission-derived navigation;
- one typed platform API boundary with feature-focused clients;
- shared primitives for loading, empty, error, forbidden, and partial-failure
  states;
- consistent query, mutation, toast, confirmation, and form behavior;
- organization-centered feature modules with explicit responsibilities;
- a small internal component system backed by semantic design tokens.

Existing API clients are retained only when their contracts match a complete
workflow. Missing projections or mixed-responsibility endpoints are corrected
in the backend instead of being hidden by frontend joins or local authority.
Shared transport DTOs remain in `AFK4.Shared.Contracts`; domain models remain
separate.

The rebuild is implemented in vertical slices on a topic branch, but `main`
receives one coherent replacement. No runtime compatibility layer or parallel
legacy route is required.

## Visual and Interaction Direction

Platform Control is a focused internal operations tool used from a desktop
browser. The direction is restrained and high-density: neutral surfaces, clear
typographic hierarchy, tabular numbers, and color reserved for meaning and
action. It must not resemble a marketing dashboard with oversized headings,
decorative metric cards, or charts that do not support a decision.

Design settings:

- design variance: low to moderate, with predictable working layouts;
- motion intensity: low, limited to immediate state feedback and spatial
  continuity;
- visual density: high but readable, optimized for tables and inspection;
- themes: first-class light and dark themes using the same semantic tokens.

Interactive controls provide default, hover, focus-visible, active, disabled,
and loading states. Keyboard focus is always visible. Text contrast meets WCAG
AA, non-text controls meet 3:1 contrast, web targets are at least 24 by 24 CSS
pixels, and the interface remains operable at 200% zoom.

## Data and Failure States

Initial page loading uses delayed skeletons that match final geometry. Mutations
provide immediate feedback within 100 ms, disable only the submitted action
while in flight, and preserve entered data on failure.

Each feature owns useful states for:

- first-use empty data;
- filtered no-results;
- authentication expiry;
- forbidden access;
- retryable transport failure;
- validation or business-rule rejection;
- partial failure where one organization section is unavailable.

A failure in billing, health, or audit data must not make unrelated organization
sections unusable. Errors state what failed and offer a concrete retry or safe
alternative without exposing secrets or internal attack details.

## Delivery Slices

1. Foundation: shell, routes, permissions, search contract, design tokens, and
   common states.
2. Operational overview and organizations table.
3. Canonical organization page and complete provisioning/support lifecycle.
4. Plans, subscriptions, and invoices.
5. Platform-managed client updates.
6. Audit and platform settings.
7. Responsive, accessibility, localization, and removal of superseded code.

The implementation plan may divide these slices further, but must not merge a
half-present workflow that exposes controls without completing their lifecycle.

## Verification

Every slice requires focused component and behavior tests. Permission tests
cover both platform roles and direct navigation to forbidden routes. Backend
integration tests prove authorization and all sensitive mutations independently
of the UI.

Before the replacement is merged:

- the full Platform Control test suite passes;
- its production build passes;
- core flows pass browser smoke in light and dark themes;
- keyboard-only navigation and 200% zoom are checked;
- long, empty, error, forbidden, and partial-response data states are exercised;
- the final diff contains no active legacy route or duplicate authority;
- current progress and product documentation reflect the replacement.

## Out of Scope

- Organization Admin and player-facing workflow redesigns;
- a customer browser admin replacing the native Windows application;
- production deployment or migration compatibility for the old frontend;
- predictive incident scoring or a generalized workflow engine;
- physical Windows Agent, Player Shell, and manager-workstation smoke execution.

The remaining physical Windows smoke is retained as production-readiness
backlog and is not a prerequisite for beginning the Platform Control rebuild.
