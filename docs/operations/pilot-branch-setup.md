# Pilot Branch Setup

This runbook creates the minimum operational configuration for a controlled
pilot branch without direct database edits.

It covers:

- branch staff users using predefined MVP roles;
- floor layout zones and seats;
- a basic tariff and active tariff version;
- a basic POS category and product;
- device-to-seat assignment through the existing device assignment API.

It does not create a web admin panel or custom role editor. Custom roles remain
outside the current MVP implementation surface.

## Prerequisites

- Platform API deployed and healthy.
- Organization and branch already exist.
- An existing owner or branch manager staff account for the branch. The account
  must have `identity.branch_staff.manage`, `layout.manage`, `tariffs.manage`,
  and `pos.catalog.manage`.
- PowerShell on a trusted operator or release workstation.

## Configure Branch

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/configure-pilot-branch.ps1 `
  -BaseUrl https://afk4.staging.mubi.dev `
  -OrganizationId '<organization-id>' `
  -BranchId '<branch-id>' `
  -AdminUserName '<owner-or-manager-user>' `
  -AdminPassword '<password>' `
  -SeatCount 20 `
  -TariffName 'Standard' `
  -PricePerMinuteMinorUnits 100 `
  -ProductCategoryName 'Drinks' `
  -ProductName 'Water 0.5' `
  -ProductSku 'WATER-05' `
  -ProductPriceMinorUnits 500
```

The script is intentionally idempotent for normal pilot reruns:

- staff users are matched by username and missing branch roles are added;
- zones are matched by branch/name;
- seats are matched by branch/zone/name;
- tariffs and POS setup use stable idempotency keys.

## Enroll And Assign Devices

For a clean gaming PC, use the staging Gaming PC setup executable. It creates
an enrollment code, enrolls the device, and assigns it to the configured smoke
seat through the Platform API.

For an already enrolled device, assign it through:

```powershell
$headers = @{ Authorization = "Bearer <staff-access-token>" }

Invoke-RestMethod `
  -Method Post `
  -Uri "https://afk4.staging.mubi.dev/api/branches/<branch-id>/devices/<device-id>/seat-assignment" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body (@{
    organizationId = "<organization-id>"
    seatId = "<seat-id>"
  } | ConvertTo-Json)
```

Do not use direct PostgreSQL edits for pilot setup unless there is no supported
API path and the gap is explicitly recorded in the progress snapshot.

## Verify

After setup:

1. Sign in with the created cashier user.
2. Load the Operator App floor map.
3. Confirm the configured seats are visible.
4. Open a shift.
5. Start and end one guest session on an assigned device.
6. Create one POS sale for the configured product and take a manual payment.
7. Confirm audit records exist for setup and operational actions.

Record the exact branch, device, session, sale, and verification timestamps in
`docs/progress/2026-05-12-vertical-slice-progress.md` when this is executed
against staging or a pilot branch.
