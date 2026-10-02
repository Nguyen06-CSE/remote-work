# Phase 04 - Run Summary (macOS)

## 📌 Thông tin môi trường (Metadata)
- **Ngày chạy:** 2026-10-02
- **Máy:** MacBook-Pro-2 (macOS 26.5.1)
- **App Version:** 1.0.0-dev
- **Device ID:** 9a14dc23-27c1-455a-96cf-41c495b987b0
- **Session ID:** fb1c3ac1-ab96-470a-9ae9-a3d35ae4ddcb
- **StartedAt (UTC):** 10/02/2026 03:53:51 +00:00
- **EndedAt (UTC):** 10/02/2026 03:54:24 +00:00
- **Duration:** 00:00:33.3771370

## 🎯 Mục tiêu kiểm tra (Objective)
- [x] Xác minh `SessionEngine` hoạt động như một domain layer độc lập với OS.
- [x] Xác minh `StartSession` sinh `SessionId` mới và ghi `StartedAt` theo UTC.
- [x] Xác minh `DeviceId` ổn định qua các session (không đổi từ Phase 01 → Phase 04).
- [x] Xác minh `GetCurrentSession` được Host truy vấn và phản ánh đúng trạng thái.
- [x] Xác minh `EndSession` ghi `EndedAt` và tính `Duration` dương.
- [x] Xác minh shutdown thoát an toàn khi session đang `Active`.

## 🆕 Điểm mới so với Phase 03 (Architecture Changes)
*Lưu ý: Logic tracking (Mouse/Keyboard) không thay đổi. Sự thay đổi nằm ở tầng Session.*

- **Đổi tên & tách tầng:** `SessionCollector` → `SessionEngine`. Không còn là collector log đơn thuần, giờ là domain service quản lý vòng đời session.
- **State machine tối thiểu:** hỗ trợ `Starting → Active → Ending → Ended` (và `Error`), không đụng tới Pause/Disconnect.
- **Timestamp UTC:** log `Session started` bổ sung trường `StartedAt`; log `Session ended` bổ sung `EndedAt` — cả hai đều định dạng ISO 8601 với offset `+00:00`.
- **API mới:** `GetCurrentSession` được expose ra ngoài; `Worker` gọi để log `Current session: <id>` và suy ra `Agent status: Running`.
- **Duration calculation:** `SessionEngine` tự tính `Duration` từ `EndedAt - StartedAt` (`00:00:33.3771370`).
- **OS-independence:** `SessionEngine` không gọi Windows API, macOS API, SQLite hay Backend — chỉ quản lý state nội bộ.
- **SessionId mới:** `fb1c3ac1-...` (khác Phase 03) chứng minh uniqueness per session.
- **DeviceId ổn định:** `9a14dc23-...` giữ nguyên qua 4 phase.
- **Shutdown-safe lifecycle:** `Ctrl+C` → `Worker` gọi `EndSession` → chuyển `Agent status: Stopping → Stopped`, session chuyển `Ended`, app không crash.

## 📋 Bằng chứng (Log Highlights)
*Chỉ trích xuất những dòng log quan trọng nhất, không copy toàn bộ.*

```text
info: RemoteWork.Desktop.Host.Worker[0]
      Agent version: 1.0.0-dev
info: RemoteWork.Desktop.Application.Collectors.DeviceCollector[0]
      Device detected: 9a14dc23-27c1-455a-96cf-41c495b987b0, Hostname: MacBook-Pro-2, OS: macOS
info: RemoteWork.Desktop.Host.Worker[0]
      OS Version: 26.5.1
info: RemoteWork.Desktop.Application.Collectors.SessionEngine[0]
      Session started. SessionId: fb1c3ac1-ab96-470a-9ae9-a3d35ae4ddcb, DeviceId: 9a14dc23-27c1-455a-96cf-41c495b987b0, StartedAt: 10/02/2026 03:53:51 +00:00
info: RemoteWork.Desktop.Host.Worker[0]
      Current session: fb1c3ac1-ab96-470a-9ae9-a3d35ae4ddcb
info: RemoteWork.Desktop.Host.Worker[0]
      Agent status: Running
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Input hooks installed.
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Activity: ActivityStateChanged | Session: fb1c3ac1-ab96-470a-9ae9-a3d35ae4ddcb | Count: (null) | Active: True
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Activity batch created. BatchId=14604f8f-9933-4d55-9657-4b9cb0a91efc, Keyboard=1, Mouse=31, Active=00:00:10.0003130, Idle=00:00:00
...
info: RemoteWork.Desktop.Host.Worker[0]
      Shutdown requested.
info: RemoteWork.Desktop.Host.Worker[0]
      Agent status: Stopping
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Input hooks removed.
info: RemoteWork.Desktop.Application.Collectors.SessionEngine[0]
      Ending session fb1c3ac1-ab96-470a-9ae9-a3d35ae4ddcb.
info: RemoteWork.Desktop.Application.Collectors.SessionEngine[0]
      Session ended. SessionId: fb1c3ac1-ab96-470a-9ae9-a3d35ae4ddcb, Duration: 00:00:33.3771370, EndedAt: 10/02/2026 03:54:24 +00:00
info: RemoteWork.Desktop.Host.Worker[0]
      Agent status: Stopped
info: RemoteWork.Desktop.Host.Worker[0]
      RemoteWork Desktop Host stopped.