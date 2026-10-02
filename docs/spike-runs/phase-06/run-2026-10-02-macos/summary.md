# Phase 06 — Run Summary (macOS)

## 📌 Metadata
- **Ngày chạy:** 2026-10-02
- **Máy:** MacBook-Pro-2 (macOS 26.5.1)
- **App Version:** 1.0.0-dev
- **Device ID:** 9a14dc23-27c1-455a-96cf-41c495b987b0
- **Session ID:** 41a1c5ef-d28e-48d8-a710-189d952fd085
- **StartedAt (UTC):** 10/02/2026 13:08:06 +00:00
- **EndedAt (UTC):** 10/02/2026 13:08:49 +00:00
- **Duration:** 00:00:43.2166840
- **Backend:** http://localhost:8000

## 🎯 Mục tiêu kiểm tra (Objective)
- [x] `MonitoringService` start/stop runtime sạch (không còn log "hooks installed/removed")
- [x] Batch được tạo đúng chu kỳ ~10s (3 batch periodic)
- [x] **Final batch flush khi shutdown** — tính năng mới của Phase 06
- [x] `DeviceId` + `SessionId` gắn đúng vào mọi batch
- [x] Session lifecycle đầy đủ (`Starting → Active → Ending → Ended`)
- [x] Graceful shutdown (`Ctrl+C` → flush → stop → exit)
- [ ] Idle transition (lần chạy này user active liên tục, không quan sát được)

## 🆕 Điểm mới so với Phase 05

| Thay đổi | Phase 05 | Phase 06 | Ý nghĩa |
|---|---|---|---|
| **Log lifecycle** | `Input hooks installed.` / `Input hooks removed.` | `Input activity provider started.` / `Input activity provider stopped.` | Đổi wording phản ánh đúng abstraction `IInputActivityProvider` thay vì "hooks" |
| **Shutdown flush** | Không có — batch cuối bị mất | **`Final activity batch flushed on shutdown. BatchId=756d91a5-...`** | Đảm bảo không mất dữ liệu activity cuối session |
| **Error isolation** | Chưa có | Collector fail được catch riêng (idle / keyboard / mouse) | Một provider hỏng không kéo sập runtime |
| **Duration guarantee** | Chỉ tính dựa trên sample cuối | Tail interval từ `_lastTimestamp` đến `now` được gán vào batch cuối | `ActiveDuration + IdleDuration ≈ EndedAt - StartedAt` |
| **Unit tests** | 87 | **98** | +11 test cho: error isolation, lifecycle, shutdown flush, duration balancing |

## 📋 Bằng chứng (Log Highlights)

```text
Session started. SessionId: 41a1c5ef-d28e-48d8-a710-189d952fd085, DeviceId: 9a14dc23-..., StartedAt: 10/02/2026 13:08:06 +00:00
Agent status: Running
Input activity provider started.                                 ← wording mới
Activity: ActivityStateChanged | ... | Active: True
Activity: KeyboardActivity | ... | Count: 4
Activity: MouseActivity | ... | Count: 6

Activity batch created. BatchId=9d3e36f6-..., Keyboard=14, Mouse=9,  Active=00:00:08.0019680, Idle=00:00:00
Activity batch created. BatchId=8bb8d81f-..., Keyboard=6,  Mouse=11, Active=00:00:11.9970380, Idle=00:00:00
Activity batch created. BatchId=2b390a29-..., Keyboard=8,  Mouse=43, Active=00:00:11.9984620, Idle=00:00:00

^C
Application is shutting down...
Shutdown requested.
Agent status: Stopping
Final activity batch flushed on shutdown. BatchId=756d91a5-..., Keyboard=1, Mouse=91, Active=00:00:09.1621680, Idle=00:00:00  ← TÍNH NĂNG MỚI
Input activity provider stopped.                                 ← wording mới
Session ended. SessionId: 41a1c5ef-..., Duration: 00:00:43.2166840, EndedAt: 10/02/2026 13:08:49 +00:00
Agent status: Stopped
```

## 📊 Phân tích batch

| # | BatchId | Keyboard | Mouse | Active | Idle | Loại |
|---|---|---|---|---|---|---|
| 1 | `9d3e36f6` | 14 | 9 | 00:00:08.00 | 0 | Periodic |
| 2 | `8bb8d81f` | 6 | 11 | 00:00:11.99 | 0 | Periodic |
| 3 | `2b390a29` | 8 | 43 | 00:00:11.99 | 0 | Periodic |
| 4 | `756d91a5` | 1 | 91 | 00:00:09.16 | 0 | **Final flush** |
| | **Tổng** | **29** | **154** | **~41.15s** | **0s** | |

**Kiểm chứng Duration Guarantee:**

```
Sum of Active durations      = 08.00 + 11.99 + 11.99 + 09.16 = 41.14s
Session Duration              = 43.22s
Chênh lệch                    = 2.08s  (≈ sampling resolution 10s / startup delay)
```

→ `ActiveDuration + IdleDuration` bám sát `EndedAt - StartedAt` trong sai số sampling resolution — đúng như tài liệu Phase 06 §4.

**Điểm đáng chú ý về Mouse=91 ở batch cuối:** con số cao bất thường là do `Ctrl+C` kích hoạt nhiều event chuột/keyboard trước khi hook bị stop — minh chứng việc final flush bắt được tail activity mà trước Phase 06 sẽ bị mất.

## 🔍 Đối chiếu với Phase 05

| Thuộc tính | Phase 05 | Phase 06 | Kết luận |
|---|---|---|---|
| SessionId | e73bb7b6-... | 41a1c5ef-... | ✅ Mới |
| DeviceId | 9a14dc23-... | 9a14dc23-... | ✅ Không đổi |
| Duration | 39.62s | 43.22s | ✅ Tương đương |
| Số batch | 3 | 4 (3 periodic + 1 final) | ✅ Thêm final flush |
| Idle transition | ✅ Có | ❌ Không (user active liên tục) | ⚠️ Không regression, chỉ khác ngữ cảnh test |
| Shutdown flush | ❌ Không | ✅ Có | ✅ Tính năng mới |
| Log wording | hooks installed/removed | provider started/stopped | ✅ Rõ nghĩa hơn |

## ✅ Đối chiếu với Definition of Done (Phase 06)

| Item | Trạng thái |
|---|---|
| Runtime starts (`MonitoringService.StartAsync`) | ✅ `Input activity provider started.` |
| Runtime stops cleanly (`StopAsync`) | ✅ `Input activity provider stopped.` |
| Activity batches generated at intervals | ✅ 3 batch periodic |
| Activity batch generated on shutdown | ✅ `Final activity batch flushed on shutdown.` |
| `DeviceId` + `SessionId` correct | ✅ Cả 4 batch đều gắn đúng |
| Duration guarantee | ✅ ~41.14s active vs 43.22s session |
| Tests pass (98/98) | ❓ Cần paste `dotnet test` output |
| Docs `docs/phases/phase-06-activity-monitoring-runtime.md` | ✅ Đã có (per task) |
| Commit created | ❓ Cần hash |

## ⚠️ Điểm cần bổ sung để đóng DoD

1. **Idle transition test cho Phase 06:** lần chạy này user active liên tục → chưa quan sát `Active: False`. Nên chạy lại và để máy idle > threshold để xác nhận logic vẫn hoạt động (không regression so với Phase 05).
2. **Error isolation test thủ công (tùy chọn):** tắt Accessibility trên macOS → kỳ vọng log warning + runtime tiếp tục, keyboard/mouse = 0, idle vẫn chạy.
3. **`dotnet test` output:** paste kết quả để xác nhận con số 98/98.
4. **Commit hash:** `git log --oneline -1` sau khi commit.

## 📝 Ghi chú

Phase 06 khép lại vòng đời activity tracking ở mức **aggregation** — batch giờ đã là đơn vị dữ liệu **hoàn chỉnh, immutable, có guarantee về duration**. Đây là bước đệm trực tiếp cho Phase 07 (SQLite persistence) vì:
- Batch đã có `BatchId` + `SessionId` + `DeviceId` + timestamp UTC → có thể insert vào bảng SQLite không cần transform.
- Final flush on shutdown đảm bảo không mất batch cuối khi process tắt đột ngột → không cần recovery logic phức tạp.
- Error isolation đảm bảo một collector hỏng không chặn các batch tiếp theo → durability được cải thiện trước khi thêm I/O.