#!/usr/bin/env python3
"""End-to-end staging smoke for AFK4 SaaS Control Plane Slices 1-6,
onboarding Slice 1.4, and hardening A-E.

Usage:
    AFK4_PLATFORM_API=https://afk4.staging.mubi.dev \
    AFK4_PLATFORM_ADMIN_USERNAME=mubi@afk4.staging.mubi.dev \
    AFK4_PLATFORM_ADMIN_PASSWORD=... \
        python3 scripts/staging-smoke.py

Creates one fresh tenant + branch + invite per run (slug is stamped with
the current Unix time so reruns are idempotent at the tenant level).
The smoke leaves the created tenant in place; clean up via the Slice 3
status PATCH (deletion_pending) or directly in Postgres.
"""
import json
import os
import sys
import time
import urllib.request
import urllib.error

API = os.environ.get("AFK4_PLATFORM_API", "https://afk4.staging.mubi.dev").rstrip("/")
USERNAME = os.environ.get("AFK4_PLATFORM_ADMIN_USERNAME", "")
PW = os.environ.get("AFK4_PLATFORM_ADMIN_PASSWORD", "")
if not USERNAME or not PW:
    print("AFK4_PLATFORM_ADMIN_USERNAME and AFK4_PLATFORM_ADMIN_PASSWORD env vars are required.",
          file=sys.stderr)
    sys.exit(2)
STAMP = int(time.time())
ORG_SLUG = f"demo-club-{STAMP}"
BRANCH_SLUG = "dushanbe-main"

passed: list[str] = []
failed: list[str] = []
token: str | None = None
staff_token: str | None = None


def step(title: str) -> None:
    print(f"\n=== {title} ===", flush=True)


def ok(msg: str) -> None:
    print(f"OK  {msg}", flush=True)
    passed.append(msg)


def fail(msg: str) -> None:
    print(f"FAIL {msg}", flush=True)
    failed.append(msg)


def request(method, path, *, body=None, auth=None, extra_headers=None):
    url = path if path.startswith("http") else API + path
    headers = {"Content-Type": "application/json"}
    if auth:
        headers["Authorization"] = f"Bearer {auth}"
    if extra_headers:
        headers.update(extra_headers)
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        resp = urllib.request.urlopen(req, timeout=15)
        raw = resp.read().decode()
        status = resp.status
        out_headers = dict(resp.headers.items())
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        status = e.code
        out_headers = dict(e.headers.items()) if e.headers else {}
    try:
        return status, json.loads(raw) if raw else {}, out_headers
    except json.JSONDecodeError:
        return status, {"_text": raw}, out_headers


def header(headers, name):
    for key, value in headers.items():
        if key.lower() == name.lower():
            return value
    return None


step("1. Platform admin sign-in")
status, body, _ = request(
    "POST", "/api/platform/auth/sign-in",
    body={"userName": USERNAME, "password": PW},
)
if status == 200 and "accessToken" in body:
    token = body["accessToken"]
    ok(f"sign-in OK, token len={len(token)}, roles={body['roles']}")
else:
    fail(f"sign-in failed: {status} {body}")
    sys.exit(1)


step("2. List tenants (initial)")
status, body, _ = request("GET", "/api/platform/tenants", auth=token)
initial_count = len(body) if isinstance(body, list) else -1
ok(f"initial tenant count: {initial_count}")


step("3. Create tenant WITH Idempotency-Key (Slice E)")
idem_key = f"smoke-{STAMP}"
create_body = {
    "organizationSlug": ORG_SLUG,
    "organizationName": "Demo Club",
    "branchSlug": BRANCH_SLUG,
    "branchName": "Dushanbe Main",
    "branchCity": "Dushanbe",
    "planCode": "starter",
    "subscriptionStatus": "trial",
    "limits": {
        "maxBranches": 3,
        "maxDevicesPerBranch": 60,
        "maxConcurrentSessions": 80,
        "maxStaffUsersPerBranch": 20,
    },
    "ownerUserName": f"owner@{ORG_SLUG}.test",
    "ownerDisplayName": "Demo Owner",
    "ownerInviteLifetime": "7.00:00:00",
}
status, body, headers = request(
    "POST", "/api/platform/tenants",
    body=create_body, auth=token,
    extra_headers={"Idempotency-Key": idem_key},
)
if status == 200 and "tenant" in body:
    org_id = body["tenant"]["organizationId"]
    branch_id = body["tenant"]["branches"][0]["branchId"]
    invite_code = body["ownerInvite"]["code"]
    invite_id = body["ownerInvite"]["ownerInviteId"]
    ok(f"tenant created org={org_id} branch={branch_id} invite={invite_id} code={invite_code[:8]}...")
else:
    fail(f"tenant create failed: {status} {body}")
    sys.exit(1)


step("4. Idempotency replay: same key + body -> 200 + Idempotency-Replayed:true")
status, body, headers = request(
    "POST", "/api/platform/tenants",
    body=create_body, auth=token,
    extra_headers={"Idempotency-Key": idem_key},
)
if status == 200 and headers.get("Idempotency-Replayed") == "true":
    ok("replay returned Idempotency-Replayed: true")
else:
    fail(f"replay: status={status} hdr={headers.get('Idempotency-Replayed')}")


step("5. Idempotency mismatch: same key + DIFFERENT body -> 422")
diff_body = dict(create_body, organizationName="Mutated")
status, body, _ = request(
    "POST", "/api/platform/tenants",
    body=diff_body, auth=token,
    extra_headers={"Idempotency-Key": idem_key},
)
if status == 422:
    ok(f"got 422 on key-reuse-different-body, error={body.get('error')}")
else:
    fail(f"expected 422, got {status} {body}")


step("6. List tenants after create")
status, body, _ = request("GET", "/api/platform/tenants", auth=token)
after_count = len(body) if isinstance(body, list) else -1
ok(f"tenant count now: {after_count}")


step("7. Get tenant detail")
status, body, _ = request("GET", f"/api/platform/tenants/{org_id}", auth=token)
if status == 200:
    ok(f"detail: slug={body['slug']} status={body['status']} branches={len(body['branches'])}")
else:
    fail(f"get tenant: {status} {body}")


step("8. List owner invites (Slice C -- CodeSuffix masking)")
status, body, _ = request("GET", f"/api/platform/tenants/{org_id}/owner-invites", auth=token)
if status == 200 and isinstance(body, list):
    for i in body:
        print(f"  invite={i['ownerInviteId'][:8]}... status={i['status']} codeSuffix={i['codeSuffix']}")
    suffix = body[0]["codeSuffix"]
    if len(suffix) == 4 and invite_code.endswith(suffix):
        ok(f"list returned 4-char suffix matching real code (...{suffix})")
    else:
        fail(f"suffix mismatch: list='{suffix}' code ends '{invite_code[-4:]}'")
    raw_text = json.dumps(body)
    if invite_code not in raw_text:
        ok("full invite code NOT in list response")
    else:
        fail("list leaked full invite code")
else:
    fail(f"list invites: {status} {body}")


step("9. Resolve operator connection via slug pair (Slice 6)")
status, body, _ = request(
    "POST", "/api/operator-connections/resolve",
    body={"organizationSlug": ORG_SLUG, "branchSlug": BRANCH_SLUG},
)
if status == 200 and body.get("organizationId") == org_id and body.get("organizationStatus") == "active":
    ok(f"slug-pair resolve -> org={body['organizationId']} status=active source={body['source']}")
else:
    fail(f"slug-pair resolve: {status} {body}")


step("10. Resolve operator connection via setup code")
status, body, _ = request(
    "POST", "/api/operator-connections/resolve",
    body={"setupCode": invite_code},
)
if status == 200 and body.get("organizationId") == org_id and body.get("source") == "setup_code":
    ok(f"setup-code resolve -> source=setup_code branch={body.get('branchSlug')}")
else:
    fail(f"setup-code resolve: {status} {body}")


step("11. Accept owner invite -> staff session")
status, body, _ = request(
    "POST", "/api/platform/owner-invites/accept",
    body={
        "code": invite_code,
        "userName": f"owner@{ORG_SLUG}.test",
        "displayName": "Demo Owner",
        "password": "OwnerP@ss-Demo24!",
    },
)
if status == 200 and body.get("organizationId") == org_id and "accessToken" in body:
    staff_token = body["accessToken"]
    ok(f"invite accepted -> staff token len={len(staff_token)} branches={len(body['branchIds'])}")
else:
    fail(f"invite accept: {status} {body}")


step("12. Re-list invites: accepted one should now show status=accepted")
status, body, _ = request("GET", f"/api/platform/tenants/{org_id}/owner-invites", auth=token)
target = next((i for i in body if i["ownerInviteId"] == invite_id), None)
if target and target["status"] == "accepted":
    ok(f"invite status flipped to 'accepted' (acceptedAtUtc={target['acceptedAtUtc']})")
else:
    fail(f"invite status: {target}")


step("13. Suspend tenant (Slice 3 PATCH)")
status, body, _ = request(
    "PATCH", f"/api/platform/tenants/{org_id}/status",
    body={"status": "suspended", "reason": "Smoke test pause"},
    auth=token,
)
if status == 200 and body.get("status") == "suspended":
    ok(f"suspend -> status=suspended reason={body.get('statusReason')}")
else:
    fail(f"suspend: {status} {body}")


step("14. Staff mutation on suspended tenant -> 403 TenantSuspended")
status, body, _ = request(
    "POST", f"/api/branches/{branch_id}/device-enrollment-codes",
    body={"organizationId": org_id, "expiresInSeconds": 3600},
    auth=staff_token,
)
if status == 403 and body.get("error") == "TenantSuspended":
    ok(f"blocked -> 403 TenantSuspended (status={body.get('status')} reason={body.get('reason')})")
else:
    fail(f"expected 403 TenantSuspended, got {status} {body}")


step("15. Resolve on suspended tenant -> status=suspended (operator app shows blocked-state)")
status, body, _ = request(
    "POST", "/api/operator-connections/resolve",
    body={"organizationSlug": ORG_SLUG, "branchSlug": BRANCH_SLUG},
)
if status == 200 and body.get("organizationStatus") == "suspended" and body.get("organizationStatusReason") == "Smoke test pause":
    ok(f"resolve -> status=suspended reason='{body['organizationStatusReason']}'")
else:
    fail(f"resolve on suspended: {status} {body}")


step("16. Reactivate")
status, body, _ = request(
    "PATCH", f"/api/platform/tenants/{org_id}/status",
    body={"status": "active", "reason": ""},
    auth=token,
)
if status == 200 and body.get("status") == "active":
    ok("reactivate -> status=active")
else:
    fail(f"reactivate: {status} {body}")


step("17. Staff mutation after reactivation -> 200")
status, body, _ = request(
    "POST", f"/api/branches/{branch_id}/device-enrollment-codes",
    body={"organizationId": org_id, "expiresInSeconds": 3600},
    auth=staff_token,
)
if status == 200 and "code" in body:
    enrollment_code = body["code"]
    ok(f"staff mutation succeeded, enrollment code={enrollment_code[:6]}...")
else:
    fail(f"expected 200, got {status} {body}")


step("18. Slice 1.4 device admin path")
status, body, headers = request("GET", f"/api/branches/{branch_id}/floor-map", auth=staff_token)
etag = header(headers, "ETag")
if status == 200 and etag:
    ok(f"floor-map loaded for Slice 1.4 setup, etag={etag}")
else:
    fail(f"floor-map load: {status} {body} etag={etag}")

floor_map = {
    "organizationId": org_id,
    "zones": [
        {"zoneId": None, "clientId": "z-main", "name": "Main Hall", "sortOrder": 1},
    ],
    "seats": [
        {"seatId": None, "clientId": "s-smoke-1", "zoneClientId": "z-main", "name": "Smoke PC 01", "sortOrder": 1},
        {"seatId": None, "clientId": "s-smoke-2", "zoneClientId": "z-main", "name": "Smoke PC 02", "sortOrder": 2},
    ],
}
status, body, headers = request(
    "PUT", f"/api/branches/{branch_id}/floor-map",
    body=floor_map,
    auth=staff_token,
    extra_headers={"If-Match": etag or ""},
)
if status == 200:
    seat_ids = {s["clientId"]: s["seatId"] for s in body.get("seats", [])}
    seat_1 = seat_ids.get("s-smoke-1")
    seat_2 = seat_ids.get("s-smoke-2")
    if seat_1 and seat_2:
        ok(f"floor-map seeded seats {seat_1[:8]}... and {seat_2[:8]}...")
    else:
        fail(f"floor-map response missing seeded seats: {body}")
else:
    fail(f"floor-map seed: {status} {body}")
    seat_1 = None
    seat_2 = None

status, body, _ = request(
    "PUT", f"/api/branches/{branch_id}/settings",
    body={"organizationId": org_id, "requireManualDeviceApproval": True},
    auth=staff_token,
)
if status == 200 and body.get("requireManualDeviceApproval") is True:
    ok("manual device approval enabled")
else:
    fail(f"manual approval enable: {status} {body}")

status, body, _ = request("POST", "/api/staff/me/owner-code/generate", auth=staff_token)
owner_code = body.get("ownerCode") if status == 200 else None
if owner_code and len(owner_code) == 8:
    ok(f"owner code generated suffix={body.get('codeSuffix')}")
else:
    fail(f"owner code generate: {status} {body}")

status, body, _ = request("POST", "/api/install/discover", body={"ownerCode": owner_code or ""})
branches = body.get("branches", []) if status == 200 else []
target_branch = next((b for b in branches if b.get("branchId") == branch_id), None)
if target_branch and seat_1 in target_branch.get("freeSeatIds", []) and seat_2 in target_branch.get("freeSeatIds", []):
    ok(f"install discover returned free seats for branch={branch_id}")
else:
    fail(f"install discover: {status} {body}")

status, body, _ = request(
    "POST", "/api/install/enroll",
    body={
        "ownerCode": owner_code or "",
        "branchId": branch_id,
        "seatId": seat_1 or "",
        "role": "gaming_pc",
        "displayName": "Smoke PC 01",
        "machineName": f"SMOKE-{STAMP}",
        "devicePublicKey": "smoke-public-key",
    },
)
smoke_device_id = body.get("deviceId") if status == 200 else None
if status == 200 and body.get("enrollmentState") == "pending" and smoke_device_id:
    ok(f"install enroll created pending device={smoke_device_id}")
else:
    fail(f"install enroll pending: {status} {body}")

status, body, _ = request("GET", f"/api/branches/{branch_id}/devices/pending", auth=staff_token)
pending_device = next((d for d in body if d.get("deviceId") == smoke_device_id), None) if isinstance(body, list) else None
if status == 200 and pending_device and pending_device.get("enrollmentState") == "pending":
    ok("pending device queue contains enrolled smoke device")
else:
    fail(f"pending queue: {status} {body}")

status, body, _ = request(
    "POST", f"/api/devices/{smoke_device_id}/approve",
    body={"organizationId": org_id, "reason": "Staging smoke approval"},
    auth=staff_token,
)
if status == 200 and body.get("enrollmentState") == "approved":
    ok("pending device approved")
else:
    fail(f"device approve: {status} {body}")

status, body, _ = request(
    "POST", f"/api/devices/{smoke_device_id}/rename",
    body={"organizationId": org_id, "displayName": "Smoke PC 01 renamed"},
    auth=staff_token,
)
if status == 200 and body.get("displayName") == "Smoke PC 01 renamed":
    ok("device renamed")
else:
    fail(f"device rename: {status} {body}")

status, body, _ = request(
    "POST", f"/api/devices/{smoke_device_id}/move-seat",
    body={"organizationId": org_id, "seatId": seat_2 or ""},
    auth=staff_token,
)
if status == 200 and body.get("seatId") == seat_2:
    ok("device moved to second smoke seat")
else:
    fail(f"device move-seat: {status} {body}")

status, body, _ = request(
    "POST", f"/api/devices/{smoke_device_id}/remove",
    body={"organizationId": org_id, "reason": "Staging smoke cleanup"},
    auth=staff_token,
)
if status == 200 and body.get("enrollmentState") == "removed" and body.get("seatId") is None:
    ok("device removed, seat detached")
else:
    fail(f"device remove: {status} {body}")

status, body, _ = request("GET", f"/api/branches/{branch_id}/devices", auth=staff_token)
listed = [d.get("deviceId") for d in body] if isinstance(body, list) else []
if status == 200 and smoke_device_id not in listed:
    ok("removed device is hidden from active branch device list")
else:
    fail(f"device list after remove: {status} {body}")


step("19. Tenant health (Slice 4)")
status, body, _ = request("GET", f"/api/platform/tenants/{org_id}/health", auth=token)
if status == 200:
    ok(f"health: branches={body.get('branchCount')} devices={body.get('deviceCount')} staff={body.get('activeStaffUserCount')} recentErrors={body.get('recentErrorCount')}")
else:
    fail(f"health: {status} {body}")


step("20. Create support note (Slice 4)")
status, body, _ = request(
    "POST", f"/api/platform/tenants/{org_id}/support-notes",
    body={"body": "Smoke test note: tenant lifecycle verified end-to-end."},
    auth=token,
)
if status == 200 and "tenantSupportNoteId" in body:
    ok(f"note created id={body['tenantSupportNoteId']} author={body.get('authorDisplayName')}")
else:
    fail(f"note: {status} {body}")


step("21. Update plan + limits")
status, body, _ = request(
    "PATCH", f"/api/platform/tenants/{org_id}/subscription",
    body={"planCode": "growth", "status": "active"},
    auth=token,
)
plan_code = body.get("planCode") if status == 200 else "?"
status2, body2, _ = request(
    "PATCH", f"/api/platform/tenants/{org_id}/limits",
    body={"limits": {
        "maxBranches": 5,
        "maxDevicesPerBranch": 80,
        "maxConcurrentSessions": 100,
        "maxStaffUsersPerBranch": 25,
    }},
    auth=token,
)
limit_branches = body2.get("limits", {}).get("maxBranches") if status2 == 200 else "?"
if status == 200 and status2 == 200:
    ok(f"plan->{plan_code} limits.maxBranches->{limit_branches}")
else:
    fail(f"plan={status} limits={status2}")


step("22. Slice D: rate-limit on /api/operator-connections/resolve (25 requests)")
codes = []
for _ in range(25):
    s, _, _ = request("POST", "/api/operator-connections/resolve", body={})
    codes.append(s)
n429 = codes.count(429)
n400 = codes.count(400)
seq = " ".join(str(c) for c in codes)
if n429 > 0:
    ok(f"rate-limit fired: {n400} x 400 + {n429} x 429 out of 25")
    print(f"  sequence: {seq}")
else:
    fail(f"no 429 in: {seq}")


step("23. Slice C revoke flow: create + revoke a fresh invite")
status, body, _ = request(
    "POST", f"/api/platform/tenants/{org_id}/owner-invites",
    body={"branchId": branch_id, "ownerUserName": "owner-2@demo.test", "ownerDisplayName": "Owner Two", "lifetime": "7.00:00:00"},
    auth=token,
)
if status == 200:
    new_invite_id = body["ownerInviteId"]
    ok(f"second invite created id={new_invite_id}")
    status, body, _ = request(
        "POST", f"/api/platform/owner-invites/{new_invite_id}/revoke",
        body={"reason": "Smoke test: revoking second invite"},
        auth=token,
    )
    if status == 200 and body.get("status") == "revoked":
        ok(f"invite revoked reason='{body.get('revokedReason')}'")
    else:
        fail(f"revoke: {status} {body}")
else:
    fail(f"second invite: {status} {body}")


print("\n================ SUMMARY ================")
print(f"PASSED: {len(passed)}")
print(f"FAILED: {len(failed)}")
for f in failed:
    print(f"  FAIL {f}")
sys.exit(len(failed))
