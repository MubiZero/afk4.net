# AFK4 Phase 12 Operator Update Management Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an Operator App workflow for managing already-published signed update packages and rollouts through the existing backend update endpoints.

**Architecture:** This slice stays inside the approved cloud-first update architecture. The backend remains the authority for package registration, package state transitions, rollout creation, rollout state transitions, and rollout status reads. The Operator App adds typed API client methods and MVVM commands over existing shared contracts; it does not build installers, upload artifacts, or manage signing keys.

**Tech Stack:** .NET 10, WPF + MVVM, shared update contracts, ASP.NET Core backend endpoints already implemented in Phase 9, xUnit.

---

## Scope

This slice implements:

- Operator App typed API methods for:
  - `POST /api/branches/{branchId}/updates/packages`;
  - `POST /api/branches/{branchId}/updates/packages/{packageId}/state`;
  - `POST /api/branches/{branchId}/updates/rollouts`;
  - `POST /api/branches/{branchId}/updates/rollouts/{rolloutId}/state`;
- Update Settings ViewModel inputs and commands for registering package metadata,
  changing package state, creating rollouts, changing rollout state, and
  refreshing rollout status;
- Settings permission exposure for `updates.packages.manage`,
  `updates.rollouts.manage`, and `updates.status.view`;
- a dense WPF Settings panel for package and rollout operations.

## Non-Goals

- No installer authoring decision.
- No CI release job.
- No artifact upload from the Operator App.
- No signing key handling in the Operator App.
- No provider-specific object-store, CDN, or key-vault integration.

## Tasks

- [x] Add RED tests for the Operator update API client management methods.
- [x] Add RED tests for update package registration, rollout creation, and
  state-change ViewModel workflows.
- [x] Extend `IOperatorUpdateApiClient`, `HttpOperatorUpdateApiClient`, and the
  unconfigured client.
- [x] Extend `UpdateStatusWorkspaceViewModel` with package and rollout
  management inputs, commands, validation, and success state propagation.
- [x] Expose the Settings update panel for update-management permissions.
- [x] Add WPF controls for package registration, package state, rollout
  creation, and rollout state.
- [x] Run targeted Operator App tests.
- [x] Run full solution build and tests.

## Safety Notes

- Critical update actions still require backend confirmation.
- Operator App only sends metadata and rollout commands; signed artifacts and
  signing keys remain outside the desktop app.
- Package and rollout operations use existing backend permission checks and
  audit behavior.
