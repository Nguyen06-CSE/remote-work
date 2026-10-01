# Monitoring Scope — MVP

## 1. Session

Agent thu thập:

- `session_id`
- `device_id`
- `started_at`
- `ended_at`
- `duration`
- `status`

Mục đích:

Xác định phiên làm việc của Employee trên Remote PC.

---

## 2. Activity

Agent thu thập:

- `active_duration`
- `idle_duration`

Có thể tính:

- `activity_percentage`

nhưng Backend mới là nơi tính các metric tổng hợp.

Agent chủ yếu gửi raw/near-raw data.

---

## 3. Mouse

Agent thu thập:

- `mouse_click_count`
- `mouse_movement_count`
- `has_suspicious_mouse_activity` (kết quả boolean từ module Bot Detection)

Nguyên tắc về tọa độ chuột:

- Tọa độ chuột (X, Y) và timestamp của các lượt click **CHỈ** được lưu tạm thời trên bộ nhớ RAM
  trong một Ring Buffer ngắn hạn (tối đa 200 samples) phục vụ thuật toán nhận diện gian lận (Bot Detection).
- Tọa độ thô **NGHIÊM CẤM**:
  - Không lưu xuống đĩa (disk/SQLite).
  - Không ghi vào log file.
  - Không serialize gửi lên Backend.
- Ngay sau khi chu kỳ phân tích hoàn tất, toàn bộ tọa độ trong buffer bị xóa sạch (Drain & Purge).
- Backend **CHỈ** nhận duy nhất cờ boolean `hasSuspiciousMouseActivity`.

---

## 4. Keyboard

Agent chỉ thu thập:

- `keyboard_press_count`

Không thu thập:

- `key_value`
- `typed_text`
- `password`
- `keyboard_content`

Đây là nguyên tắc cần ghi rõ trong specification.

---

## 5. Applications

Agent thu thập:

- `application_name`
- `process_name`
- `started_at`
- `ended_at`
- `duration`

MVP chưa thu thập:

- window content
- file content
- application screenshot riêng

---

## 6. Screenshot

Agent hỗ trợ:

- `enabled`
- `interval_seconds`
- `captured_at`
- `file`
- `file_size`
- `checksum`

Default:

- `interval_seconds = 600` (tức 10 phút)

Nhưng:

- 600 giây là default configuration, **không hard-code logic**.

---

## 7. Device

Agent thu thập:

- `device_id`
- `hostname`
- `operating_system`
- `os_version`
- `agent_version`

---

## 8. Heartbeat

Agent gửi:

- `device_id`
- `timestamp`
- `agent_version`
- `status`

để Backend biết Agent còn hoạt động.