# Desktop App Platform Decision

## 1. Mục đích

Tài liệu này ghi lại quyết định kỹ thuật cho Desktop App/Desktop Agent của dự án Remote Employee Performance Platform.

Phạm vi hiện tại:

- Desktop application chạy trực tiếp trên máy Employee, không còn phụ thuộc Remote PC.
- Ưu tiên macOS và Windows.
- Linux được giữ trong kiến trúc để mở rộng sau.
- Giai đoạn hiện tại tập trung vào Activity Collection, local persistence, offline handling và API synchronization.
- UI sẽ được triển khai sau khi phần core/agent hoàn thành.

---

## 2. Nền tảng được chọn

### Core Desktop Application

**C# / .NET**

C#/.NET được chọn làm nền tảng chính cho Desktop Application/Agent.

Các thành phần dự kiến:

- C# / .NET
- Background Service / Worker cho các tác vụ thu thập dữ liệu
- SQLite cho local persistence
- ORM cho dữ liệu local khi phù hợp
- HTTP/REST client để giao tiếp với Backend API
- Platform Abstraction để tách logic chung khỏi API riêng của từng OS
- Native interop/P/Invoke hoặc native helper khi cần tương tác sâu với OS

### UI trong giai đoạn sau

UI chưa triển khai trong giai đoạn Agent hiện tại.

Khi bắt đầu UI cross-platform, **Avalonia UI** là hướng dự kiến phù hợp để đánh giá/triển khai vì dự án cần macOS + Windows và có khả năng mở rộng Linux.

UI không được phép trở thành dependency bắt buộc của Activity Engine.

---

## 3. Kiến trúc nền tảng

Kiến trúc định hướng:

```text
RemoteWork Desktop
│
├── Core
│   └── Domain / Models / Contracts
│
├── Application
│   └── Activity orchestration / Sync / Use Cases
│
├── Infrastructure
│   └── HTTP / SQLite / Logging / Configuration
│
├── Platform.Abstractions
│   ├── IIdleProvider
│   ├── IActiveApplicationProvider
│   ├── IInputActivityProvider
│   ├── IScreenshotProvider
│   └── IPlatformPermissionService
│
├── Platform.Windows
│   └── Windows native implementations
│
└── Platform.MacOS
    └── macOS native implementations
```

Nguyên tắc:

> Business logic không được biết implementation cụ thể của Windows/macOS.

Ví dụ:

```csharp
public interface IIdleProvider
{
    TimeSpan GetIdleTime();
}
```

Windows và macOS có implementation riêng, nhưng Application layer chỉ gọi interface.

---

## 4. Vì sao C#/.NET phù hợp với dự án này?

### 4.1. Đây trước hết là một Desktop Agent, không phải Web UI

Trong giai đoạn hiện tại, chương trình cần:

- Chạy nền.
- Thu thập OS activity.
- Theo dõi active application.
- Theo dõi idle/active state.
- Có khả năng làm việc offline.
- Ghi SQLite.
- Đồng bộ dữ liệu lên Backend.
- Quản lý lifecycle.
- Chạy ổn định trong thời gian dài.

C#/.NET phù hợp với mô hình background application/service và có hệ sinh thái native interop tốt.

### 4.2. Không phụ thuộc Chromium

Electron mang theo Chromium + Node.js runtime.

Trong khi Agent hiện tại chưa cần một browser engine để thực hiện công việc cốt lõi.

C#/.NET giúp tách rõ:

```text
OS
↓
Native Activity Layer
↓
C# Application
↓
SQLite / Sync
↓
Backend API
```

### 4.3. Có thể sử dụng native OS API

C# không có một API duy nhất để lấy toàn bộ activity.

Thay vào đó:

```text
C#
 ↓
Platform abstraction
 ↓
Windows native APIs / macOS native APIs
```

Điều này cho phép kiểm soát rõ implementation theo từng OS.

### 4.4. Phù hợp với kiến trúc Agent-first

Project hiện tại chủ động làm Agent trước UI.

C# cho phép xây dựng core độc lập:

```text
Activity Engine
    ↓
Local Storage
    ↓
Sync Engine
    ↓
Backend
```

Sau này UI chỉ là một lớp phía trên.

---

# 5. So sánh với Electron/Gauzy

Gauzy là nguồn tham khảo kiến trúc quan trọng của dự án.

Theo tài liệu đã phân tích, Gauzy sử dụng Electron Main Process để quản lý lifecycle, IPC, timer, database và OS-related operations. Activity/native hooks sử dụng các thành phần như `uiohook-napi`; idle có thể lấy qua Electron `powerMonitor`; screenshot sử dụng Electron desktop capture hoặc thư viện native; dữ liệu offline được lưu bằng SQLite và đồng bộ qua queue.

Điểm quan trọng:

> Electron không tự thực hiện toàn bộ OS monitoring bằng JavaScript thuần. Nó vẫn dựa vào native modules và OS APIs.

---

## 6. So sánh kiến trúc

| Tiêu chí | C#/.NET | Electron/Gauzy |
|---|---|---|
| Desktop Agent | Rất phù hợp | Phù hợp |
| Background processing | Native với .NET | Electron Main Process |
| OS integration | Native interop / PInvoke / helper | Electron APIs + native modules |
| Windows | Mạnh | Mạnh |
| macOS | Có thể dùng native interop | Có Electron APIs/native modules |
| Linux | Có thể mở rộng | Có hỗ trợ trong hệ sinh thái Electron |
| CPU/RAM | Thường nhẹ hơn nếu không cần UI web engine | Cao hơn do Chromium/runtime |
| SQLite | Có thư viện .NET trưởng thành | Gauzy dùng better-sqlite3/Knex |
| Offline sync | Có thể thiết kế riêng | Gauzy có nhiều queue/state |
| Web UI | Không phải thế mạnh | Rất mạnh |
| Native desktop UI | Cần framework riêng | Có sẵn Chromium UI |
| Native dependency | Có thể cần | Vẫn cần cho một số activity |
| Architecture Agent-first | Rất phù hợp | Phù hợp |
| Tách UI khỏi Agent | Tự nhiên | Có thể nhưng cần Main/Renderer/IPC |
| Kiểm soát lifecycle | Tốt | Tốt |
| Cross-platform abstraction | Cần tự thiết kế | Một phần do Electron cung cấp |

---

# 7. Những điểm học hỏi từ Gauzy

Dự án không sao chép implementation của Gauzy. Chỉ sử dụng các ý tưởng kiến trúc phù hợp.

Các ý tưởng đáng học:

### Activity abstraction

Activity collection phải tách khỏi UI.

### Local-first

Dữ liệu nên được lưu local trước khi upload.

```text
Collector
   ↓
Local SQLite
   ↓
Sync Queue
   ↓
Backend
```

### Offline-first

Nếu mất mạng:

```text
Collect
  ↓
Store locally
  ↓
Retry later
```

### OS permission

Các chức năng nhạy cảm như screenshot hoặc accessibility phải có permission handling riêng theo OS.

### Native layer

Không giả định JavaScript/.NET tự có tất cả khả năng OS-level.

### Queue/state management

Dữ liệu chưa đồng bộ cần trạng thái rõ ràng:

```text
PENDING
SYNCING
SYNCED
FAILED
```

### Không trộn UI với Activity Engine

Gauzy có Main/Renderer/IPC vì Electron cần bridge giữa UI và native process. Dự án này sẽ tránh tạo dependency tương tự nếu chưa cần UI.

---

# 8. Vì sao chưa chọn Electron dù Gauzy dùng Electron?

Không phải vì Electron không phù hợp.

Electron là một lựa chọn hợp lý nếu mục tiêu chính là:

- UI phong phú bằng React/Angular/Vue.
- Một codebase JavaScript/TypeScript.
- Web technology là kỹ năng chính của team.
- Cần tận dụng Electron APIs.

Tuy nhiên, mục tiêu hiện tại của dự án là:

```text
Desktop Agent first
        ↓
OS Activity
        ↓
Local Storage
        ↓
Offline Sync
        ↓
Backend API
        ↓
UI later
```

Do đó, việc đưa Chromium/Renderer/IPC vào từ đầu chưa tạo ra lợi ích tương ứng với phần core hiện tại.

Nếu sau này UI yêu cầu rất lớn, kiến trúc vẫn có thể được đánh giá lại dựa trên PoC và requirement thực tế.

---

# 9. Nguyên tắc không khóa công nghệ tuyệt đối

Quyết định dùng C#/.NET không có nghĩa:

> "Mọi thứ bắt buộc phải viết bằng C#."

Nếu một OS capability không thể thực hiện tốt bằng .NET abstraction, cho phép sử dụng:

```text
C#
 ↓
Native helper
 ↓
C/C++ / Swift / Objective-C / OS API
```

Miễn là native component được đóng gói sau một interface ổn định.

Mục tiêu là:

> Chọn công nghệ phù hợp với capability cần triển khai, không chọn công nghệ chỉ để đồng nhất ngôn ngữ.

---

# 10. Quyết định hiện tại

**Desktop Core:** C# / .NET

**Local Database:** SQLite

**ORM/Data Access:** ORM phù hợp với .NET architecture của project

**Communication:** HTTP/REST API với Backend

**OS integration:** Platform-specific implementation sau abstraction

**Target OS hiện tại:** Windows + macOS

**Linux:** chuẩn bị abstraction, triển khai ở phase sau

**UI:** chưa triển khai

**UI direction:** đánh giá Avalonia khi bước vào UI phase

**Architecture:** Agent-first, modular, platform-independent core

---

# 11. Điều kiện để xem xét thay đổi nền tảng

Không chuyển sang Electron chỉ vì Gauzy dùng Electron.

Việc thay đổi nền tảng chỉ nên được xem xét nếu PoC thực tế cho thấy một hoặc nhiều vấn đề:

- Native capability quan trọng không hoạt động ổn định.
- Permission model không thể xử lý phù hợp.
- Cross-platform implementation trở nên quá phức tạp.
- Performance/resource usage không đạt requirement.
- Packaging/deployment gặp trở ngại nghiêm trọng.
- UI requirement sau này khiến kiến trúc hiện tại không còn phù hợp.

Do đó, trước khi xây toàn bộ Activity Engine, dự án cần thực hiện một **Native Capability Spike** trên Windows và macOS.

---

# 12. Kết luận

Kiến trúc hiện tại chọn:

```text
C# / .NET
     │
     ├── Activity Engine
     ├── Platform Abstraction
     ├── SQLite
     ├── Offline Queue
     └── API Sync
              │
              ▼
          Backend API
```

Sau khi core ổn định:

```text
              Backend
                 ▲
                 │
          API Sync Layer
                 ▲
                 │
        C# Desktop Core
          /                Windows          macOS
          \           /
           Native APIs
                 │
                 ▼
            OS Activity
```

UI được bổ sung sau và không làm thay đổi nguyên tắc rằng Activity Engine phải hoạt động độc lập với UI.

**Quyết định này là quyết định kỹ thuật hiện tại dựa trên scope MVP và có thể được đánh giá lại bằng dữ liệu từ các PoC/benchmark thực tế.**
