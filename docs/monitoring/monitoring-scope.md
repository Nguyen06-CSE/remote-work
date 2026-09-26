Monitoring Scope — MVP
1. Session

Agent thu thập:

session_id
device_id
started_at
ended_at
duration
status

Mục đích:

Xác định phiên làm việc của Employee trên Remote PC.

2. Activity

Agent thu thập:

active_duration
idle_duration

Có thể tính:

activity_percentage

nhưng mình khuyên Backend mới là nơi tính các metric tổng hợp.

Agent chủ yếu gửi raw/near-raw data.

3. Mouse

Agent chỉ thu thập:

mouse_click_count
mouse_movement_count

Không thu thập:

cursor_coordinates
mouse_path

trong MVP.

4. Keyboard

Agent chỉ thu thập:

keyboard_press_count

Không thu thập:

key_value
typed_text
password
keyboard_content

Đây là nguyên tắc cần ghi rõ trong specification.

5. Applications

Agent thu thập:

application_name
process_name
started_at
ended_at
duration

MVP chưa thu thập:

window content
file content
application screenshot riêng
6. Screenshot

Agent hỗ trợ:

enabled
interval_seconds
captured_at
file
file_size
checksum

Default:

interval_seconds = 600

tức 10 phút.

Nhưng:

600 giây là default configuration, không hard-code logic.

7. Device

Agent thu thập:

device_id
hostname
operating_system
os_version
agent_version
8. Heartbeat

Agent gửi:

device_id
timestamp
agent_version
status

để Backend biết Agent còn hoạt động.