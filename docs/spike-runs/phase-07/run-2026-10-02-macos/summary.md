# Phase 07 — Run Summary (macOS)

## 📌 Metadata
- **Ngày chạy:** 2026-10-02
- **Máy:** MacBook-Pro-2 (macOS 26.5.1)
- **App Version:** 1.0.0-dev
- **Device ID:** 9a14dc23-27c1-455a-96cf-41c495b987b0
- **Session ID:** 3c1ea66b-f97a-45ff-a4b2-f48fc0b9d941
- **StartedAt (UTC):** 10/02/2026 13:49:59 +00:00
- **EndedAt (UTC):**   10/02/2026 13:53:32 +00:00
- **Duration:** 00:03:33.3526730
- **Database path:** `/Users/caotiendattx/Library/Application Support/RemoteWork/Agent/remotework.db`
- **Backend:** http://localhost:8000

## 🎯 Mục tiêu kiểm tra (Objective)
- [x] SQLite DB khởi tạo + migrate thành công khi start (`Applying database migrations...` → `Database ready.`)
- [x] Migrate idempotent — lần chạy sau `No migrations were applied. The database is already up to date.`
- [x] Idle transition ghi nhận đúng (`Active: True → False → True`)
- [x] Batch có `Idle > 0` khi user ngừng tương tác (`Idle=00:00:11.99`)
- [x] Final batch flush on shutdown
- [x] Session lifecycle đầy đủ, shutdown sạch
- [ ] Data thực tế đã ghi vào SQLite hay chưa (cần query `sqlite3` để verify)

## 🆕 Điểm mới so với Phase 06

| Thay đổi | Phase 06 | Phase 07 | Ý nghĩa |
|---|---|---|---|
| **SQLite init** | Không có | `DatabaseInitializer` chạy `MigrateAsync()` trước `host.RunAsync()` | DB ready trước khi tracking bắt đầu |
| **EF Core startup log** | Không có | Debug log chi tiết: `Migrating using database 'main' on server '...remotework.db'` | Có thể trace đường dẫn DB |
| **Migration lock** | Không có | `Acquiring an exclusive lock for migration application` | Bảo vệ khi nhiều instance cùng khởi động |
| **DB idempotency** | N/A | Lần 2 chạy: `No migrations were applied. The database is already up to date.` | An toàn restart |
| **Index optimization** | N/A | EF Core bỏ redundant index (`The index {'DeviceId'} was not created...already covered by {DeviceId, StartedAt}`) | Schema được tối ưu |
| **Resource** | ~34 MB | **~72 MB** | ⚠️ **+38 MB do SQLite + EF Core** |
| **Warning** | Không | `NU1903: SQLitePCLRaw.lib.e_sqlite3 2.1.10 has a known high severity vulnerability` | ⚠️ Cần theo dõi |

## 📋 Log Highlights

```text
info: RemoteWork.Desktop.Persistence.Data.DatabaseInitializer[0]
      Applying database migrations...
dbug: Microsoft.EntityFrameworkCore.Migrations[20400]
      Migrating using database 'main' on server '/Users/caotiendattx/Library/Application Support/RemoteWork/Agent/remotework.db'.
info: Microsoft.EntityFrameworkCore.Migrations[20411]
      Acquiring an exclusive lock for migration application.
info: Microsoft.EntityFrameworkCore.Migrations[20405]
      No migrations were applied. The database is already up to date.
info: RemoteWork.Desktop.Persistence.Data.DatabaseInitializer[0]
      Database ready.

info: RemoteWork.Desktop.Application.Collectors.SessionEngine[0]
      Session started. SessionId: 3c1ea66b-f97a-45ff-a4b2-f48fc0b9d941, ..., StartedAt: 10/02/2026 13:49:59 +00:00
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Input activity provider started.

Activity batch created. BatchId=f16c3c07-..., Keyboard=8,   Mouse=2,   Active=00:00:08.00, Idle=00:00:00
Activity batch created. BatchId=0ed2a1c9-..., Keyboard=15,  Mouse=63,  Active=00:00:11.99, Idle=00:00:00
Activity batch created. BatchId=46dd110a-..., Keyboard=22,  Mouse=457, Active=00:00:12.00, Idle=00:00:00
Activity batch created. BatchId=2817975a-..., Keyboard=0,   Mouse=0,   Active=00:00:00,    Idle=00:00:11.99  ← idle full batch
Activity batch created. BatchId=bb02abe6-..., Keyboard=2,   Mouse=1,   Active=00:00:02.00, Idle=00:00:09.99

^C
Final activity batch flushed on shutdown. BatchId=4a23ce2e-..., Keyboard=13, Mouse=3, Active=00:00:05.30, Idle=00:00:00
Input activity provider stopped.
Session ended. SessionId: 3c1ea66b-..., Duration: 00:03:33.3526730, EndedAt: 10/02/2026 13:53:32 +00:00
Agent status: Stopped