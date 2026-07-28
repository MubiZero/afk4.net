# Organization Admin Latest Installer Design

## Goal

Keep the Organization Admin upgrade URL current after every successful staging
package build without restarting the Platform API or interrupting club traffic.

## Decision

The update store keeps two representations of each Organization Admin package:

- an immutable, versioned MSI used by signed update metadata, rollout records,
  audit evidence, and rollback;
- one stable `latest` MSI object used only as the download target returned by
  the Organization Admin compatibility gate.

The staging compatibility URL is configured once and does not contain a build
version:

```text
https://updates.afk4.staging.mubi.dev/afk4-updates-staging/organization-admin/internal/latest/afk4-organization-admin-internal.msi
```

Publishing a new package must not change API configuration, restart the API,
or deploy application code.

## Publishing Flow

After `Package Smoke` builds the role-specific MSIs, the existing publisher
uploads the Organization Admin MSI to its immutable versioned key and produces
the signed request JSON. A focused publishing step then:

1. reads the Organization Admin request JSON rather than reconstructing the
   versioned URI or hash;
2. verifies `component=organization-admin`, `channel=internal`, an HTTPS
   artifact URI under the configured staging public base URI, and a local MSI
   whose size and SHA-256 match the request;
3. uploads the same verified bytes to a temporary object under the stable
   `latest` prefix;
4. promotes those bytes to the final stable object only after upload
   verification succeeds;
5. downloads the public stable object and verifies its size and SHA-256 before
   the workflow can pass.

The versioned object and signed request remain authoritative for update
rollouts. The stable object is a convenience download pointer and must never be
used as immutable release evidence.

## Failure And Concurrency Behavior

The final stable object is replaced only after the new object is completely
available. A failed build, upload, verification, or promotion leaves the
previous stable MSI usable and fails the workflow.

Concurrent publications for the same environment and channel are serialized by
GitHub Actions concurrency. The stable object must receive cache headers that
prevent clients and intermediary caches from serving an older installer after
promotion. No signing key, object-store credential, or raw token is written to
logs or artifacts.

Rolling back the convenience download means promoting a previously verified
immutable MSI to the same stable object. Update rollouts continue to use their
recorded immutable artifact URI and are unaffected.

## Platform API Configuration

`OrganizationAdminCompatibility__DownloadUrl` is set once to the stable public
URI. The compatibility response continues to require epoch 2 and returns this
URL for missing or obsolete client headers.

Changing the stable MSI does not require configuration reload, container
restart, or API deployment. A deliberate compatibility-epoch or download-host
change remains a coordinated release operation.

## Production Boundary

Staging may promote the unsigned internal package produced by `Package Smoke`.
Production promotion is a separate release action and is allowed only after
Authenticode signing, release approval, immutable artifact verification, and
the production rollback point are recorded. Staging automation must not have
credentials or configuration capable of writing the production stable object.

## Verification

Automated tests cover request validation, host/base-URI enforcement, hash and
size mismatch rejection, stable-key construction, failure-before-promotion,
workflow wiring, and the no-API-restart invariant.

The staging workflow proves that:

- the immutable and stable URLs both return the expected bytes;
- the compatibility response returns the stable URL;
- no Coolify restart or deploy is triggered by package publication;
- existing package registration and rollout creation still use immutable URLs.

The publishing and rollback procedure is documented in the update-package and
big-bang cutover runbooks.
