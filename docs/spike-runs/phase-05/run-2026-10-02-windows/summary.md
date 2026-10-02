
---

## File 4: `spike-runs/phase-05/run-2026-10-02-windows/summary.md`

```markdown
# Phase 05 — Run Summary (Windows)

## 📌 Metadata
- **Ngày chạy:** 2026-10-02
- **Máy:** DESKTOP-J3HEKF5 (Windows 10.0.26100.0)
- **App Version:** 1.0.0-dev
- **Device ID:** 86c16fc9-25cc-4c9a-9f24-c7ea71ea7f62
- **Session ID:** 247a9109-d312-480e-a412-7e47f52032ae
- **StartedAt:** 10/02/2026 04:16:00 +00:00
- **EndedAt:**   10/02/2026 04:17:22 +00:00
- **Duration:**  00:01:22.5445412

## 🎯 Objective
- [x] Idle transition: `Active: True → False → True`
- [x] Batch có `Idle > 00:00:00`
- [x] Keyboard count > 0 (`Keyboard=6`) — Phase 04 luôn = 0
- [x] Mouse count > 0
- [x] Session lifecycle đầy đủ
- [x] Shutdown sạch (`Input hooks removed.`)

## 📋 Log Highlights

```text
Session started. SessionId: 247a9109-..., StartedAt: 10/02/2026 04:16:00 +00:00
Input hooks installed.
Activity: ActivityStateChanged | ... | Active: True
Activity: ActivityStateChanged | ... | Active: False       ← idle transition
Activity batch created. BatchId=8424361a-..., Keyboard=0, Mouse=0, Active=00:00:00, Idle=00:00:10.0045558  ← Idle > 0
Activity: ActivityStateChanged | ... | Active: True
Activity: MouseActivity | ... | Count: 2/10/3/4
Activity batch created. BatchId=83dc6129-..., Keyboard=0, Mouse=19, Active=00:00:08.0033672, Idle=00:00:03.9882809
Activity: KeyboardActivity | ... | Count: 3
Activity batch created. BatchId=5c80be8d-..., Keyboard=6, Mouse=20, Active=00:00:11.9984561, Idle=00:00:00  ← Keyboard > 0
Input hooks removed.
Session ended. SessionId: 247a9109-..., Duration: 00:01:22.5445412, EndedAt: 10/02/2026 04:17:22 +00:00
Agent status: Stopped