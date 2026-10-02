
---

## File 3: `spike-runs/phase-05/run-2026-10-02-macos/summary.md`

```markdown
# Phase 05 — Run Summary (macOS)

## 📌 Metadata
- **Ngày chạy:** 2026-10-02
- **Máy:** MacBook-Pro-2 (macOS 26.5.1)
- **App Version:** 1.0.0-dev
- **Device ID:** 9a14dc23-27c1-455a-96cf-41c495b987b0
- **Session ID:** e73bb7b6-56b0-4260-844c-c35b9279eea3
- **StartedAt:** 10/02/2026 04:13:53 +00:00
- **EndedAt:**   10/02/2026 04:14:33 +00:00
- **Duration:**  00:00:39.6214810

## 🎯 Objective
- [x] Idle transition: `Active: True → False → True`
- [x] Batch có `Idle > 00:00:00` khi user ngừng thao tác
- [x] Không crash khi end session
- [x] Keyboard/Mouse counter batching đúng
- [ ] Permission test (revoke Accessibility) — pending

## 📋 Log Highlights

```text
Session started. SessionId: e73bb7b6-..., StartedAt: 10/02/2026 04:13:53 +00:00
Input hooks installed.
Activity: ActivityStateChanged | ... | Active: True
Activity batch created. BatchId=7704f129-..., Keyboard=0, Mouse=0, Active=00:00:09.9873850, Idle=00:00:00
Activity batch created. BatchId=f2bae9ac-..., Keyboard=0, Mouse=0, Active=00:00:11.9914310, Idle=00:00:00
Activity: ActivityStateChanged | ... | Active: False      ← idle transition
Activity: ActivityStateChanged | ... | Active: True        ← resume
Activity: MouseActivity | ... | Count: 11
Activity: MouseActivity | ... | Count: 5
Activity batch created. BatchId=4dfd7c35-..., Keyboard=0, Mouse=16, Active=00:00:05.9986780, Idle=00:00:05.9978600  ← Idle > 0
Input hooks removed.
Session ended. SessionId: e73bb7b6-..., Duration: 00:00:39.6214810, EndedAt: 10/02/2026 04:14:33 +00:00
Agent status: Stopped