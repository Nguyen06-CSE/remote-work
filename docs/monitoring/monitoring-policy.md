Đây là phần mình muốn bạn thiết kế ngay từ đầu.

Agent không tự quyết định:

"Screenshot = ON"

mà nhận configuration từ Backend.

Ví dụ Policy:

{
  "version": 1,

  "activity": {
    "enabled": true
  },

  "applications": {
    "enabled": true
  },

  "screenshots": {
    "enabled": true,
    "interval_seconds": 600
  },

  "device": {
    "enabled": true
  },

  "heartbeat": {
    "enabled": true,
    "interval_seconds": 60
  }
}