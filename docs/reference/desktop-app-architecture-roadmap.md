# RemoteWork Desktop App — Architecture & Roadmap

## Mục tiêu cuối cùng

Xây dựng **Desktop App đa nền tảng** (Electron + TypeScript) thay thế C# Worker hiện tại, với các đặc tính cốt lõi:

- Electron
- TypeScript
- SQLite
- IPC
- Multi-platform
- Offline-first
- Policy-driven

---

## Cấu trúc tài liệu đề xuất

```
docs/
├── architecture/
│   ├── desktop-overview.md
│   ├── platform-architecture.md
│   └── development-environment.md
│
├── monitoring/
│   ├── monitoring-scope.md
│   └── monitoring-policy.md
│
└── api/
    └── desktop-api-contract.md
```

---

## PHASE 0 — RE-ARCHITECTURE

**Mục tiêu:** Chốt kiến trúc Desktop App đa nền tảng.

**Chốt công nghệ:**

| Thành phần       | Quyết định          |
|------------------|---------------------|
| Framework        | Electron            |
| Language         | TypeScript          |
| Local Database   | SQLite              |
| Communication    | IPC                 |
| Platform         | Multi-platform      |
| Data strategy    | Offline-first       |
| Control model    | Policy-driven       |

---

## PHASE 1 — DESKTOP APP FOUNDATION

Thay thế C# Worker hiện tại.

**Tạo:**

```
apps/desktop
```

**Cấu trúc tối thiểu:**

- Electron Main
- Renderer
- Preload
- Configuration
- Logger
- Lifecycle

**Mục tiêu đạt được:**

- App starts
- App runs
- App quits
- Tray works
- Logging works

> Chưa có monitoring.

---

## PHASE 2 — CORE DOMAIN

Tạo các model **platform-independent**:

- `Device`
- `Session`
- `ActivityEvent`
- `ActivityBatch`
- `MonitoringPolicy`

**Interface:**

```ts
IActivityProvider
IIdleProvider
IApplicationProvider
IScreenshotProvider
IStorage
ISyncService
```

> Phase này **không** gọi OS API.

---

## PHASE 3 — DEVICE IDENTITY & LOCAL CONFIG

Desktop App tạo identity:

- Device ID
- Installation ID
- App Version
- OS

**Local Config:**

```
Local Config
├── deviceId
├── backendUrl
├── policyVersion
└── appVersion
```

---

## PHASE 4 — SESSION ENGINE

Làm lại Session theo architecture mới:

```
Login
  ↓
Tracking Start
  ↓
Session
  ↓
Tracking Stop
```

**Hỗ trợ các trạng thái sau này:**

- Active
- Paused
- Disconnected
- Ended

---

## PHASE 5 — CROSS-PLATFORM ACTIVITY ENGINE

Đây là phase lớn.

**Không làm:**

```
Windows-only Activity
```

**Mà làm:**

```
Activity Engine
        │
        ├── macOS Provider
        ├── Windows Provider
        └── Linux Provider
```

**Thu thập:**

- Keyboard count
- Mouse count
- Active / Idle

### Quyết định quan trọng: Tự viết hay tận dụng ActivityWatch?

Đây nên là một task nghiên cứu trong Phase 5:

```
Activity Architecture Study
        │
        ├── Native implementation
        │
        ├── ActivityWatch
        │
        └── Hybrid
```

Gauzy cũng dùng ActivityWatch như một lớp bổ sung cho app/window/browser activity, trong khi vẫn có tracking built-in của riêng họ.

Có thể kiến trúc cuối sẽ là:

```
Our Desktop App
       │
       ├── Built-in Activity
       │
       └── Optional ActivityWatch Integration
```

Thay vì tự xây mọi native collector từ con số 0.

---

## PHASE 6 — ACTIVITY AGGREGATION

Tạo:

- `ActivityBucket`
- `ActivityBatch`
- `ActivityTimeline`

**Ví dụ:**

```
10:00–10:10

Keyboard: 531
Mouse: 120
Active: 8m42s
Idle: 1m18s
```

> Gauzy cũng tổ chức tracking thành time slots và gắn activity + screenshot + app usage vào từng khoảng thời gian. Nên học mô hình này.

---

## PHASE 7 — LOCAL SQLITE

Đây là phase rất lớn.

Desktop App có SQLite với các bảng:

```
devices
sessions
activity_events
activity_batches
app_usage
screenshots
sync_queue
settings
```

**Vị trí lưu trữ:**

```
OS user-data directory
```

(giống cách Gauzy đặt SQLite trong `userData`)

---

## PHASE 8 — OFFLINE-FIRST SYNC ENGINE

Đây là phần cần đầu tư nhiều.

```
Collector
    ↓
SQLite
    ↓
Sync Queue
    ↓
Backend
```

**Trạng thái:**

- `PENDING`
- `SYNCING`
- `SYNCED`
- `FAILED`

**Có:**

- retry
- backoff
- batch upload
- idempotency

> Mô hình queue/state machine của Gauzy là thứ rất đáng học ở đây.

---

## PHASE 9 — APPLICATION TRACKING

Đây sẽ là phase tiếp theo sau Activity.

**Thu thập:**

```
Application
├── name
├── process
├── started
├── ended
├── duration
└── active window
```

Gauzy built-in tracking có App Name và Window Title; ActivityWatch có thể bổ sung browser URL nếu được bật.

**MVP:**

- App Name
- Duration
- Active Window

> Browser URL để sau.

---

## PHASE 10 — SCREENSHOT ENGINE

**MVP:**

```
Screenshot
├── Enabled
├── Interval
├── Timestamp
├── Screen
└── Local file
```

Không phải livestream liên tục.

> Gauzy cũng không dùng WebSocket video stream liên tục; screenshot được chụp theo chu kỳ, sau đó stop capture.

---

## PHASE 11 — MONITORING POLICY

Bây giờ mới đưa ý tưởng Adaptive Monitoring vào Desktop App.

```
Policy
├── Activity
├── Application
├── Screenshot
└── ...
```

**Ví dụ policy:**

- Intern High
- Intern Normal
- Employee Normal
- Custom

Agent/App nhận:

```
GET /desktop/policy
```

và áp dụng.

---

## PHASE 12 — DESKTOP → BACKEND API

Chỉ sau khi Desktop App đã:

- Collect
- Store
- Queue
- Sync

ổn định mới xây API chính thức.

**API:**

```
POST /api/v1/desktop/register
POST /api/v1/desktop/heartbeat
POST /api/v1/desktop/sessions
POST /api/v1/desktop/activity/batch
POST /api/v1/desktop/applications
POST /api/v1/desktop/screenshots

GET  /api/v1/desktop/config
GET  /api/v1/desktop/policy
```

---

## PHASE 13 — BACKEND INGESTION

**FastAPI modules:**

```
agent/
desktop/
tracking/
```

**Backend chịu trách nhiệm:**

- Authentication
- Validation
- Persistence
- Aggregation
- Analytics
- Policy

> Desktop App **không** tính Performance Score.

---

## PHASE 14 — SECURITY

Làm:

- Device Authentication
- Token Rotation
- Policy Authorization
- TLS
- Audit
- Screenshot Permission
- Data Retention
- Rate Limit

---

## PHASE 15 — CROSS-PLATFORM TESTING

Test thực tế:

```
macOS
├── Activity
├── App
├── Screenshot
└── Permission

Windows
├── Activity
├── App
├── Screenshot
└── Permission

Linux
├── Activity
├── App
├── Screenshot
└── Permission
```

> Linux đặc biệt cần test môi trường display server/Wayland vì screen capture có cách hoạt động khác. Tài liệu Gauzy cũng mô tả một đường WebRTC capture cho Linux Wayland.

---

## PHASE 16 — DESKTOP PACKAGING

**Build:**

- `RemoteWork.dmg`
- `RemoteWork.exe`
- `RemoteWork.AppImage` / `.deb`

hoặc packaging Linux phù hợp.

**Có:**

- Installer
- Auto Start
- Tray
- Auto Update
- Uninstall

---

## PHASE 17 — FRONTEND

Lúc này mới chuyển sang FE.

Backend đã có dữ liệu thật:

```
Employee
   ↓
Session
   ↓
Activity
   ↓
Application
   ↓
Screenshot
```

**FE modules:**

- Dashboard
- Employee
- Activity
- Application
- Screenshot
- Sessions
- Policies
- Reports

---

## Kiến trúc cuối cùng của Desktop App

```
apps/
└── desktop/
    ├── main/
    ├── renderer/
    └── preload/

packages/
├── core/
│
├── desktop-shell/
│
├── tracking/
│   ├── session/
│   ├── activity/
│   ├── application/
│   └── screenshot/
│
├── platform/
│   ├── macos/
│   ├── windows/
│   └── linux/
│
├── storage/
│   └── sqlite/
│
├── sync/
│
├── network/
│
├── policy/
│
├── auth/
│
└── shared/
```

> So với Gauzy, không cần tạo quá nhiều package ngay lập tức. Chỉ tách package khi logic thực sự đủ lớn.

---

## Luồng dữ liệu mục tiêu

```
              EMPLOYEE COMPUTER
                     │
                     ▼
             ┌───────────────┐
             │ Desktop App   │
             └───────┬───────┘
                     │
              Monitoring Policy
                     │
                     ▼
          ┌─────────────────────┐
          │ Tracking Engine     │
          └──────────┬──────────┘
                     │
       ┌─────────────┼──────────────┐
       ▼             ▼              ▼
    Activity     Application     Screenshot
       │             │              │
       └─────────────┼──────────────┘
                     ▼
                Normalized Event
                     │
                     ▼
                  SQLite
                     │
                     ▼
                Sync Queue
                     │
                Internet?
                /         \
              NO           YES
              │             │
              ▼             ▼
          Keep Local      Upload
                            │
                            ▼
                        FastAPI
                            │
                            ▼
                       PostgreSQL
```

---

## Điều cần học từ Gauzy

Có 6 phần của Gauzy nên nghiên cứu song song trong quá trình xây:

### ① desktop-activity

Cách họ abstract:

- Keyboard
- Mouse
- AFK
- Active Window

Tài liệu mô tả `uiohook-napi`, `powerMonitor` và ActivityWatch là ba lớp khác nhau.

### ② Local SQLite

Đặc biệt cách họ lưu:

- timer
- interval
- screenshot
- activity

local trước khi sync.

### ③ Sync Queue

Đây có lẽ là phần đáng học nhất về backend integration.

Gauzy dùng các queue/state để xử lý offline sequence và sync lại khi có mạng.

### ④ IPC

Electron:

```
Renderer
   ↕
IPC
   ↕
Main
   ↕
OS
```

Gauzy tập trung IPC ở một layer riêng.

### ⑤ Screenshot

Học cách họ tách:

- Capture
- Processing
- Storage
- Upload

thay vì viết một hàm `takeScreenshot()` khổng lồ.

### ⑥ ActivityWatch

Đây là hướng cần nghiên cứu kỹ trước khi tự viết quá nhiều Activity Collector.

ActivityWatch được thiết kế cross-platform và theo dõi active application/window, browser tab, keyboard/mouse để xác định AFK.

---

## Mối quan hệ Desktop App và Web App

Từ bây giờ nên nhìn:

```
                    RemoteWork Platform

             ┌────────────┴────────────┐
             ▼                         ▼
       Desktop Client              Web Client
             │                         │
             └───────────┬─────────────┘
                         ▼
                     FastAPI
                         │
                     PostgreSQL
```

| Client          | Vai trò                              |
|-----------------|--------------------------------------|
| Desktop App     | Data Collector + Local Tracking Client |
| Web App         | Management + Analytics + Configuration |

Đây là ranh giới rất sạch.

---

## Roadmap thực tế

Do đã hoàn thành C# Agent prototype, không cần cố kéo prototype đó tiếp.

**Bắt đầu lại implementation ở:**

```
NEW PHASE 0
Architecture migration → Electron/TypeScript
```

**Thứ tự thực hiện:**

```
Phase 0   Architecture
    ↓
Phase 1   Electron Foundation
    ↓
Phase 2   Core Domain
    ↓
Phase 3   Device Identity
    ↓
Phase 4   Session Engine
    ↓
Phase 5   Activity Engine
    ↓
Phase 6   Activity Aggregation
    ↓
Phase 7   SQLite
    ↓
Phase 8   Offline Sync
    ↓
Phase 9   Application Tracking
    ↓
Phase 10  Screenshot
    ↓
Phase 11  Monitoring Policy
    ↓
Phase 12  Backend API
    ↓
Phase 13  Backend Processing
    ↓
Phase 14  Security
    ↓
Phase 15  Cross-platform QA
    ↓
Phase 16  Packaging / Distribution
    ↓
Phase 17  Frontend
```
