# Troubleshooting: Collector Error Isolation (Phase 06)

## Behavior

Trong `ActivityCollector.Collect()`, mỗi collector được gọi độc lập trong try-catch riêng:

| Collector fail | Hành vi | Fallback |
|---|---|---|
| `IIdleActivityCollector.IsUserActive()` | Log warning, tiếp tục | `Active = true` (không phạt nhân viên oan) |
| `IKeyboardActivityCollector.Collect()` | Log warning, tiếp tục | `KeyboardCount = 0` |
| `IMouseActivityCollector.Collect()` | Log warning, tiếp tục | `MouseCount = 0` |
| Exception trong tick `MonitoringService.RunAsync` | Log warning | Bỏ qua tick đó, không dừng loop |

## Nguyên tắc

- **Một collector hỏng KHÔNG kéo sập runtime.**
- **Idle fail → fallback Active** (không phạt user vì lỗi sensor).
- **Keyboard/Mouse fail → fallback 0** (không làm sai lệch count).

## Khi nào cần kiểm tra

Nếu log xuất hiện warning `Collector X failed`:

1. Kiểm tra Accessibility permission (macOS) — `System Settings → Privacy & Security → Accessibility`.
2. Kiểm tra hook đã cài đặt chưa — log `Input activity provider started.` phải có.
3. Nếu chỉ 1 collector fail còn 2 cái kia chạy → runtime OK, chỉ cần fix provider đó.

## Test

`tests/RemoteWork.Desktop.UnitTests/Application/ActivityCollectorTests.cs`:
- `Collect_WhenKeyboardCollectorFails_IsolatesErrorAndCollectsMouse`
- `Collect_WhenMouseCollectorFails_IsolatesErrorAndCollectsKeyboard`
- `Collect_WhenIdleCollectorFails_IsolatesErrorAndCollectsInput`