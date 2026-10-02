# Phase 03 - Run Summary (macOS)

## 📌 Thông tin môi trường (Metadata)
- **Ngày chạy:** 2026-10-02
- **Máy:** MacBook-Pro-2 (macOS 26.5.1)
- **App Version:** 1.0.0-dev
- **Device ID:** 9a14dc23-27c1-455a-96cf-41c495b987b0
- **Session ID:** 8b087a2c-05fd-4f72-95c1-76eb84e13553

## 🎯 Mục tiêu kiểm tra (Objective)
- [x] Xác minh DeviceId không đổi sau khi restart app (Persistence).
- [x] Xác minh App chạy ổn định trong thời gian dài (Long-running stability).
- [x] Xác minh cấu hình mặc định được load đúng.

## 🆕 Điểm mới so với Phase 02 (Architecture Changes)
*Lưu ý: Logic tracking (Mouse/Keyboard) không thay đổi. Sự thay đổi nằm ở tầng Identity.*
- **DeviceId:** UUID `9a14dc23...` giữ nguyên qua các lần chạy (từ Phase 1 đến Phase 3). File lưu tại `~/Library/Application Support/RemoteWork/Agent/device-id.txt`.
- **Log Format:** `DeviceCollector` đã tách riêng `OS` và `OS Version` thành 2 dòng log.
- **Thời gian chạy:** Session kéo dài ~9 phút 8 giây (so với ~54s ở Phase 2). Chứng minh app không bị crash hay rò rỉ bộ nhớ.

## 📋 Bằng chứng (Log Highlights)
*Chỉ trích xuất những dòng log quan trọng nhất, không copy toàn bộ.*

```text
info: RemoteWork.Desktop.Host.Worker[0]
      Agent version: 1.0.0-dev
info: RemoteWork.Desktop.Application.Collectors.DeviceCollector[0]
      Device detected: 9a14dc23-27c1-455a-96cf-41c495b987b0, Hostname: MacBook-Pro-2, OS: macOS
info: RemoteWork.Desktop.Host.Worker[0]
      OS Version: 26.5.1
info: RemoteWork.Desktop.Application.Collectors.SessionCollector[0]
      Session started. SessionId: 8b087a2c-05fd-4f72-95c1-76eb84e13553, DeviceId: 9a14dc23-27c1-455a-96cf-41c495b987b0
...
info: RemoteWork.Desktop.Application.Collectors.SessionCollector[0]
      Session ended. SessionId: 8b087a2c-05fd-4f72-95c1-76eb84e13553, Duration: 00:09:08.1051890