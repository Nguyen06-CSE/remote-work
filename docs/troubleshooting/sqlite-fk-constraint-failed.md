# Troubleshooting: SQLite FOREIGN KEY constraint failed

## Hiện tượng

Khi Phase 08 chạy production runtime, mọi batch insert đều fail:

```text
Microsoft.Data.Sqlite.SqliteException (0x80004005):
SQLite Error 19: 'FOREIGN KEY constraint failed'

INSERT INTO "ActivityBatches" (...) VALUES (...); ← fail
```

Log xác nhận ở `Worker.cs:82`:

```text
fail: RemoteWork.Desktop.Host.Worker[0]
Failed to persist and queue ActivityBatch <id>.
```

## Nguyên nhân gốc rễ

Bảng `ActivityBatches` có hai FK:
- `SessionId → Sessions.SessionId`
- `DeviceId  → Devices.DeviceId`

Pipeline Phase 08 lần đầu wire `TrackingPersistenceCoordinator` vào `MonitoringService.OnBatchGenerated`, nhưng **chưa wire Device + Session persistence vào Worker startup**.

Kết quả: `SessionEngine.StartSession()` chỉ tạo Session trong bộ nhớ; `DeviceCollector` chỉ trả về DeviceId mà không ghi DB. Khi batch insert chạy, FK target (Session + Device) không tồn tại trong SQLite → FK fail.

```text
Memory only:                        SQLite DB:
┌──────────────────┐               ┌──────────────────┐
│ Session e4684cd6 │               │ Devices: 0 rows  │
│ Device 9a14dc23  │               │ Sessions: 0 rows │
└────────┬─────────┘               └──────────────────┘
         │
         ▼
ActivityBatch insert ─── FK fail ──▶ ✗
```

## Giải pháp

Wire Device + Session persistence vào Worker **trước** khi MonitoringService bắt đầu emit batch. Cụ thể trong `Worker.ExecuteAsync`:

```csharp
// Sau SessionEngine.StartSession(), trước MonitoringService.StartAsync()
using var scope = _scopeFactory.CreateScope();
var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

await deviceRepo.SaveAsync(device, ct);   // upsert (LastSeenAt)
await sessionRepo.SaveAsync(session, ct); // insert

// Session có EndedAt set trên shutdown → update
// (hook vào StopAsync / finally block)
```

Kết quả log sau fix:

```text
UPDATE "Devices" SET "AgentVersion"=..., "LastSeenAt"=... WHERE "DeviceId"=...
INSERT INTO "Sessions" ("SessionId", "DeviceId", "EndedAt", "StartedAt", "Status") VALUES (...)
Persisted initial Device 9a14dc23-... and Session e4684cd6-... to SQLite.
...
INSERT INTO "ActivityBatches" (...) VALUES (...);  ← PASS
```

## Cách verify

Sau khi fix, chạy agent ~60s, rồi query:

```bash
sqlite3 ~/Library/Application\ Support/RemoteWork/Agent/remotework.db <<EOF
SELECT 'Devices',         COUNT(*) FROM Devices;
SELECT 'Sessions',        COUNT(*) FROM Sessions;
SELECT 'ActivityBatches', COUNT(*) FROM ActivityBatches;
SELECT 'SyncQueue',       COUNT(*) FROM SyncQueue;
EOF
```

**Kỳ vọng:** tất cả > 0.

Log runtime phải có (theo đúng thứ tự):
1. `Persisted initial Device ... and Session ...`
2. `Saved ActivityBatch ... to local SQLite.`
3. `Enqueued ActivityBatch ... to sync queue.`

Và **KHÔNG** có:
- `FOREIGN KEY constraint failed`
- `Failed to persist and queue ActivityBatch`

## Phòng ngừa

*   **Tuân thủ thứ tự insert:** parent (Device, Session) → child (ActivityBatch, SyncQueue).
*   **Không disable FK trong SQLite:** FK là safety net cho dữ liệu.
*   **Test startup wiring:** bất kỳ khi nào thêm bảng mới có FK trỏ tới Sessions hoặc Devices, viết integration test chạy đủ flow Worker.Start → batch → DB để bắt lỗi sớm.
*   **Kiểm tra dependencies:** Đọc `docs/phases/phase-07-local-persistence.md` mục "Next Phase Dependencies" — Phase 07 đã ghi rõ "Wire repositories into MonitoringService". Phase 08 cần đọc section đó trước khi implement.