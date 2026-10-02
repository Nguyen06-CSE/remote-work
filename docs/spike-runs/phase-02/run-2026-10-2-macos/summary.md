# Phase 02 - Run Summary

**Thời gian chạy:** Session `7beb3351-f1bd-4030-8048-2f3ee600a8d2` (Kéo dài ~54.7s)
**Thiết bị:** MacBook-Pro-2 (macOS 26.5.1)
**Agent Version:** 1.0.0-dev

## 🆕 Những điểm mới so với Phase 01

1. **Tập trung vào Keyboard Tracking:**
   - Phase 1: Chủ yếu ghi nhận `MouseActivity` (chuột).
   - Phase 2: Xuất hiện dày đặc các `KeyboardActivity`. Các batch ghi nhận số lượng phím cực lớn.
   - *Minh chứng log mới:*
     - `Keyboard=84, Mouse=2`
     - `Keyboard=146, Mouse=0`
     - `Keyboard=132, Mouse=0`
     - `Keyboard=178, Mouse=1`

2. **Cơ chế Batching ổn định:**
   - Các batch được tạo đều đặn với chu kỳ ~10-12 giây.
   - Thời gian Active (Active time) trong mỗi batch được tính toán chính xác (ví dụ: `00:00:11.9996310`).

3. **Vòng đời Session (Session Lifecycle) rõ ràng hơn:**
   - Log ghi nhận đầy đủ từ lúc bắt đầu (`Session started`), chuyển trạng thái (`Agent status: Running`), cho đến khi kết thúc (`Ending session`, `Session ended`).
   - Thời gian session kéo dài hơn so với Phase 1 (54.7s so với 26.3s).

## 📋 Kết quả chạy (Log tóm tắt)

```text
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Input hooks installed.
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Activity: KeyboardActivity | Session: 7beb3351... | Count: 41 | Active: (null)
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Activity batch created. BatchId=83766f15..., Keyboard=84, Mouse=2, Active=00:00:11.9962530, Idle=00:00:00
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Activity batch created. BatchId=55a3e172..., Keyboard=146, Mouse=0, Active=00:00:09.9999440, Idle=00:00:00
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Activity batch created. BatchId=5cceff9e..., Keyboard=132, Mouse=0, Active=00:00:09.9999700, Idle=00:00:00
info: RemoteWork.Desktop.Application.Monitoring.MonitoringService[0]
      Activity batch created. BatchId=e21d6c12..., Keyboard=178, Mouse=1, Active=00:00:11.9992620, Idle=00:00:00
info: RemoteWork.Desktop.Application.Collectors.SessionCollector[0]
      Session ended. SessionId: 7beb3351..., Duration: 00:00:54.7128590