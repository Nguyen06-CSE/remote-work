# TÀI LIỆU KIẾN TRÚC KỸ THUẬT GAUZY DESKTOP

## 1. TỔNG QUAN & Ý TƯỞNG THIẾT KẾ

### 1.1. Ý tưởng cốt lõi
Ever Gauzy là nền tảng quản lý doanh nghiệp (HR, dự án, timesheet…). Phần Desktop App ra đời để đưa time tracking “xuống máy tính người dùng” — nơi có thể:
- Theo dõi thời gian làm việc theo project/task theo thời gian thực.
- Thu thập bằng chứng làm việc (screenshot, activity %, cửa sổ đang mở).
- Hoạt động khi mất mạng rồi đồng bộ lại khi có internet.
- Tích hợp sâu với OS (tray, idle, sleep/lock, quyền chụp màn hình).

Hai sản phẩm Electron phục vụ hai mức độ nhu cầu:

| Sản phẩm | AppId / Protocol | Vai trò |
| :--- | :--- | :--- |
| **Gauzy Desktop** | `com.ever.gauzydesktop` / `gauzy-desktop://` | Client đầy đủ: timer + tùy chọn UI platform + API local tích hợp |
| **Gauzy Desktop Timer** | `com.ever.gauzydesktoptimer` / `gauzy-timer://` | Client mỏng: chỉ time tracking, kết nối API remote |

---

### 1.2. Bài toán giải quyết

| Đối tượng | Vấn đề | Cách Desktop App giải quyết |
| :--- | :--- | :--- |
| **Doanh nghiệp** | Không kiểm soát được thời gian làm việc remote | Timesheet tự động + screenshot + activity |
| **Nhân viên** | Ghi giờ thủ công dễ quên/sai | Start/Stop một lần, đếm wall-clock chính xác |
| **Hệ thống** | Web không truy cập được OS | Electron main process: capture, idle, tray, SQLite offline |
| **IT / DevOps** | Cần WakaTime / IDE heartbeat | Sidecar NestJS `desktop-api` trên cổng 5622 |

---

### 1.3. Vai trò 3 thư mục và mối quan hệ

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Business Backend                                 │
│                  (Remote hoặc api/main.js [chỉ Full Desktop])                │
└───────────────────────▲─────────────────────────────▲───────────────────────┘
                        │ HTTP                        │ HTTP
                        │                             │
┌───────────────────────┴─────────────┐       ┌───────┴───────────────────────┐
│            apps/desktop             │       │      apps/desktop-timer       │
│            (Full Desktop)           │       │         (Timer Only)          │
└───────────┬───────────────────┬─────┘       └─────┬─────────────────────────┘
            │ fork              │ IPC               │ IPC
            │                   │                   │
            ▼                   │                   │
┌───────────────────────┐       │                   │
│   apps/desktop-api    │       │                   │
│   (NestJS :5622)      │       │                   │
└───────────────────────┘       ▼                   ▼
                     ┌─────────────────────────────────┐
                     │         Shared Packages         │
                     │  • desktop-lib (IPC, Timer)     │
                     │  • desktop-window               │
                     │  • desktop-ui-lib (Angular UI)  │
                     │  • desktop-activity             │
                     │  • desktop-core                 │
                     └─────────────────────────────────┘
```

| Thư mục | Vai trò thực tế |
| :--- | :--- |
| `apps/desktop` | Entry Electron full. Main: `src/index.ts`. Renderer Angular. Có thể `createGauzyWindow()` + fork API đầy đủ khi `isLocalServer`. |
| `apps/desktop-timer` | Entry Electron timer-only. Cùng stack tracking/IPC/tray; không nhúng full platform UI/API. |
| `apps/desktop-api` | Không phải Electron app. NestJS sidecar được `fork('./desktop-api/main.js')` sau `server_is_ready`, phục vụ WakaTime local. |

> **Lưu ý quan trọng:** Ba thư mục `apps/*` chủ yếu là shell + bootstrap. Logic nghiệp vụ nằm trong `packages/desktop-*`. Hiểu đúng kiến trúc cần đọc cả lớp package.

---

## 2. KIẾN TRÚC KỸ THUẬT & CÔNG NGHỆ

### 2.1. Tech stack

| Lớp | Công nghệ | Bằng chứng |
| :--- | :--- | :--- |
| **Shell** | Electron ^38.1.2 | `apps/desktop/package.json`, `apps/desktop-timer/package.json` |
| **Main process** | TypeScript, Node APIs, electron-store, electron-log, electron-updater | `apps/*/src/index.ts` |
| **Renderer** | Angular 21, Nebular, Akita, RxJS | `apps/*/src/main.ts`, deps |
| **Local sidecar** | NestJS 11, TypeORM + MikroORM, better-sqlite3 | `apps/desktop-api` |
| **Offline DB (main)** | Knex + SQLite (`gauzy.sqlite3` trong userData) | `ProviderFactory`, `GAUZY_USER_PATH` |
| **Capture** | `desktopCapturer` / `MediaStream`, `screenshot-desktop` | `ScreenCaptureWebRTCService`, `desktop-screenshot.ts` |
| **Activity** | ActivityWatch (localhost:5600), `powerMonitor`, `uiohook-napi` | `desktop-activity`, AW services |

---

### 2.2. Main Process vs Renderer Process

```
┌──────────────────────────────────────────────────────────────┐
│ MAIN PROCESS (Node/Electron) — apps/*/src/index.ts           │
│  • Window lifecycle (desktop-window)                         │
│  • Tray, auto-updater, permissions                           │
│  • TimerHandler, screenshots (native), idle/power            │
│  • Knex SQLite offline                                       │
│  • IPC hub: ipcMainHandler + ipcTimer (desktop-ipc.ts)       │
│  • fork(desktop-api) [+ DesktopServer API nếu full Desktop]  │
├──────────────────────────────────────────────────────────────┤
│ PRELOAD — titlebar / theme helpers (không phải contextBridge │
│           API đầy đủ; nodeIntegration: true)                 │
├──────────────────────────────────────────────────────────────┤
│ RENDERER (Chromium + Angular) — desktop-ui-lib                │
│  • TimeTrackerComponent, Auth, Setup, Settings               │
│  • ElectronService → ipcRenderer.invoke / send               │
│  • HTTP tới Gauzy API (timesheet, screenshot, auth)          │
│  • ScreenCaptureWebRTCService (grab frame rồi stop tracks)   │
└──────────────────────────────────────────────────────────────┘
```

**Luồng khởi động điển hình** (`apps/desktop/src/index.ts` / `apps/desktop-timer/src/index.ts`):
1. `setupAkitaStorageHandler()` — đồng bộ Akita ↔ electron-store
2. `ProviderFactory` → tạo/migrate DB
3. Splash → `createTimeTrackerWindow(...)`
4. Setup hoặc `startServer(configs)`
5. `ipcMainHandler(...)`
6. Khi nhận `server_is_ready` → `fork(desktop-api/main.js)` + `ipcTimer(...)`

Cửa sổ đăng ký (`RegisteredWindow` trong `desktop-core`): `TIMER`, `MAIN`, `SETUP`, `SETTINGS`, `SPLASH`, `CAPTURE`, `WIDGET` (always-on), `AUTH`, …

---

### 2.3. Cơ chế IPC

**Trung tâm:** `packages/desktop-lib/src/lib/desktop-ipc.ts`

| Nhóm | Hàm | Ví dụ channel |
| :--- | :--- | :--- |
| **Setup / sync / OS** | `ipcMainHandler()` | `START_SERVER`, `DESKTOP_CAPTURER_GET_SOURCES`, `UPDATE_SYNCED`, `COLLECT_ACTIVITIES`, `SET_OFFLINE_MODE` |
| **Timer lifecycle** | `ipcTimer()` | `START_TIMER`, `STOP_TIMER`, `CURRENT_TIMER`, `logout_desktop`, `show_ao` / `hide_ao` |

**Mẫu giao tiếp:**

```
TimeTrackerComponent ─── invoke('START_TIMER') ───► ipcMain (desktop-ipc) ───► TimerHandler.startTimer()
                     ◄─── send('timer_push') ───────
                                │
                                ▼
                           POST /timesheet/timer/start (Gauzy API)
```

```
TimerHandler ─── prepare_activities_screenshot ───► TimeTrackerComponent ───► takeScreenshot + pushToTimeSlot
(mỗi updatePeriod)                                                                    │
                                                                                      ▼
                                                                        POST /timesheet/time-slot + /screenshot
```

Renderer gọi qua `ElectronService` (`desktop-ui-lib`), dùng `window.require('electron')` vì `nodeIntegration: true` (preload chủ yếu cho titlebar: `apps/desktop/src/preload/preload.ts`, `apps/desktop-timer/src/preload.ts`).

---

## 3. BẢO MẬT & XÁC THỰC

### 3.1. Luồng đăng nhập
1. **UI:** `AuthStrategy` / `AuthService.login` → `POST /api/auth/login`
2. Kiểm tra `user.employee` (desktop yêu cầu employee)
3. Lưu vào Akita PersistStore: token, refreshToken, tokenExpiresAt, org/tenant/user
4. `AuthService.electronAuthentication` → IPC `auth_success`
5. **Main:** handler lưu user SQLite + electron-store key `auth`
6. **Offline fallback:** nếu API lỗi, có thể tái dùng session cache đã lưu.

---

### 3.2. Quản lý JWT & đồng bộ API

| Kho lưu trữ | Nội dung | Ai dùng |
| :--- | :--- | :--- |
| **Akita + AkitaStorageEngine** → electron-store | Token renderer, expiry | HTTP interceptors |
| **electron-store auth** | token, refreshToken, employeeId, organizationId, tenantId, userId, isLogout | Main / LocalStore.beforeRequestParams |

**Làm mới token:**
* **Proactive:** `TokenRefreshService` — kiểm tra ~60s; refresh nếu sắp hết hạn (buffer ~5 phút); circuit breaker sau nhiều lần fail.
* **Reactive:** `RefreshTokenInterceptor` bắt HTTP 401 → `TokenRefreshHandler` → retry (bỏ qua endpoint auth / offline).
* **Attach:** `TokenInterceptor` gắn `Authorization: Bearer …`.
* **Logout:** `AuthStrategy.logout` → `POST /auth/logout` + IPC `FINAL_LOGOUT` → dừng timer nếu đang chạy, `isLogout: true`.

---

## 4. CÁC CHỨC NĂNG CỐT LÕI VÀ CÁCH THỰC HIỆN

### 4.1. Time Tracking

**Engine chính:** `TimerHandler` — `packages/desktop-lib/src/lib/desktop-timer.ts`

* **Start:** IPC `START_TIMER` → `TimerHandler.startTimer` → tạo bản ghi local Timer (`startSyncState: PENDING`) → bật `DesktopEventCounter` + `DesktopActiveWindow` → interval 1000 ms thu activity.
* **Period slot:** `startTimerIntervalPeriod`: mỗi `60 * 1000 * updatePeriod` phút (`updatePeriod` $\in \{1, 5, 10\}$, mặc định 10) gửi `prepare_activities_screenshot`.
* **Stop:** `STOP_TIMER` → stamp `stoppedAt` / `stopSyncState: PENDING` → UI gọi `toggleApiStop`.
* **Đồng hồ UI:** `calculateTimeRecord()` dùng wall-clock `moment().diff(timeStart)` — không cộng tick (tránh drift).

```typescript
// desktop-timer.ts (Ln 206–211)
calculateTimeRecord() {
  const now = moment();
  this.timeRecordSecond = now.diff(moment(this.timeStart), 'seconds');
  this.timeRecordHours = now.diff(moment(this.timeStart), 'hours');
  this.timeRecordMinute = now.diff(moment(this.timeStart), 'minutes');
}
```

* **API:** `TimeTrackerService.toggleApiStart` / `toggleApiStop` → `POST …/timesheet/timer/start|stop` (mutex `_timerMutex` tránh race khi sync offline).
* **Pause:** Không có nút Pause riêng trong UI timer. Pause thực tế = sleep/lock (`DesktopPowerManager`) hoặc thất bại inactivity proof → chiến lược sleep/pause tracking.

---

### 4.2. Screen Capture & “Live Stream”

Không có WebSocket live stream liên tục. “Stream” ở đây là MediaStream ngắn hạn để lấy một khung PNG, rồi `stop()` tracks.

| Engine | Class / file | Cách lấy khung |
| :--- | :--- | :--- |
| **ElectronDesktopCapturer** (mặc định) | `ScreenCaptureWebRTCService.takeScreenshot` | `getUserMedia({ chromeMediaSource: 'desktop' })` → canvas PNG + thumb 320×240; maxFrameRate: 5 |
| **ScreenshotDesktopLib** | `desktop-screenshot.ts` → `getScreenshot()` | npm `screenshot-desktop` |

* **Tần suất:** Theo `updatePeriod` (1/5/10 phút) hoặc chế độ random (`nextTickScreenshot` / `maxMinAdditionalTime`).
* **Phạm vi:** `monitor.captured`: `'all'` | `'active-only'`.
* **Gửi lên server:** HTTP FormData — `TimeTrackerService.uploadImages` → `POST /api/timesheet/screenshot`. Offline fail → SQLite (Screenshot / Interval, `synced: false`).
* **Gate quyền:** `allowScreenshotCapture` từ employee/auth.

---

### 4.3. Activity & Idle Detection

Ba lớp bổ trợ:

| Lớp | Thành phần | Cơ chế |
| :--- | :--- | :--- |
| **A. ActivityWatch** (ưu tiên) | `ActivityWatchService`, buckets `localhost:5600` | Mỗi giây collect window/AFK/browser; % = window − AFK |
| **B. DesktopEventCounter** | `packages/desktop-activity/.../desktop-event-counter.ts` | Fallback: mỗi giây, `powerMonitor.getSystemIdleTime() === 0` → system active. Flag kb/mouse gần như không được set ở path UI chính |
| **C. Idle proof** | `PowerManagerDetectInactivity` + `DesktopOsInactivityHandler` | Idle > `inactivityTimeLimit` (mặc định 10 phút) → dialog proof trong `activityProofDuration`; reject → cắt idle time / pause |

* **Active window:** `DesktopActiveWindow` poll ~10s (bỏ qua nếu AW đã cover).
* Đường `uiohook` (KeyboardMouse, AFK ~30s) chủ yếu dùng trong agent app, không phải vòng lặp UI desktop-timer chính.

---

### 4.4. Offline Mode & Data Sync

* **Phát hiện mạng:** `DesktopOfflineModeHandler` — ping API mỗi 10 giây qua `ApiServerConnectivity`; emit offline / connection-restored.
* **Lưu local:**
  * Config/auth/settings: `electron-store`
  * Timer / interval / screenshot / AW events: Knex + SQLite `{userData}/gauzy.sqlite3` (`TimerService`, `IntervalService`, `ScreenshotService`)
* **Sync khi online lại:**

```
               [Mạng phục hồi]
                      │
                      ▼
            Unsynced timers + intervals
                      │
                      ▼
                SequenceQueue
                      │
            ┌─────────┴─────────┐
       Offline start?        Online normal
            │                       │
            ▼                       ▼
    Silent toggleApiStart    TimeSlotQueue
             /               (POST time-slot)
        addTimeLog                  │
            │                       ▼
            │               Upload screenshots
            │                       │
            └─────────┬─────────────┘
                      ▼
         UPDATE_SYNCED / UPDATE_SYNCED_TIMER
                      │
         (Silent stop nếu isStoppedOffline)
```

`SequenceQueue` / `InterruptedSequenceQueue` / `TimeSlotQueue` / `ScreenshotQueue` trong `desktop-ui-lib/src/lib/offline-sync/`:
* Poll nền khoảng 5 giây (`BACKGROUND_SYNC_OFFLINE_INTERVAL`) đến khi hết queue.
* Trong lúc tracking: embedded-queue trong `TimerHandler.processWithQueue` cho job nội bộ (AW, duration).

---

## 5. TÁC DỤNG & GIÁ TRỊ MANG LẠI

### 5.1. Hiệu năng (CPU / Memory)

| Thiết kế | Tác động |
| :--- | :--- |
| **Tách Main vs Renderer** | UI Angular không block OS hooks; capture/idle chạy main |
| **Capture theo chu kỳ, stop MediaStream ngay** | Tránh giữ camera/desktop stream liên tục → tiết kiệm CPU/RAM |
| **`desktop-api` là child process riêng** | Crash/load WakaTime không kéo sập UI; share `GAUZY_USER_PATH` |
| **Desktop Timer không nhúng full API/@gauzy/core** | Binary nhẹ hơn, ít memory hơn full Desktop |
| **Interval 1s chỉ cho clock/activity nhẹ** | Screenshot theo phút → trade-off hợp lý giữa độ mịn dữ liệu và tải máy |
| **SQLite local + queue sync** | UI vẫn start/stop khi mất mạng; tránh spam API |

---

### 5.2. Giá trị quản lý năng suất & minh bạch
* Timesheet gắn project/task, nguồn `DESKTOP` rõ ràng.
* Time-slot + screenshot + activity % tạo chuỗi bằng chứng có thể audit.
* Idle detection giảm “đếm giờ khi rời máy”.
* Offline-first giảm mất dữ liệu khi VPN/mạng kém.
* WakaTime sidecar mở rộng sang IDE/editor heartbeat cùng hệ sinh thái.

---

## 6. BẢO TRÌ VÀ MỞ RỘNG

### 6.1. Ưu / nhược điểm tổ chức mã

| Ưu điểm | Nhược điểm / rủi ro |
| :--- | :--- |
| Hai product share `desktop-lib` / `desktop-ui-lib` → ít nhân đôi logic | `apps/*` mỏng dễ gây hiểu nhầm “toàn bộ code nằm trong 3 folder” |
| IPC tập trung `desktop-ipc.ts` | File IPC rất lớn → khó review, dễ regression |
| Offline queue tách class rõ (`SequenceQueue`, …) | Nhiều trạng thái sync (PENDING, interrupted, failed upload) cần nắm kỹ trước khi sửa |
| `nodeIntegration: true`, `contextIsolation: false` đơn giản hóa bridge | Giảm bề mặt bảo mật renderer; cân nhắc migrate `contextBridge` khi hardening |
| Desktop vs Timer phân tách sản phẩm rõ | Phải kiểm thử cả hai shell khi đổi shared package |

---

### 6.2. Lời khuyên cho lập trình viên mới
1. **Đổi UI timer** → `packages/desktop-ui-lib` (`TimeTrackerComponent`, services), không chỉ `apps/desktop-timer`.
2. **Đổi Start/Stop / interval / offline DB** → `TimerHandler` + `desktop-ipc.ts` + `packages/desktop-lib/src/lib/offline/`.
3. **Thêm IPC channel** → đăng ký trong `ipcMainHandler` hoặc `ipcTimer`; gọi từ renderer qua `ElectronService`; nhớ `removeTimerListener` / cleanup khi quit.
4. **Screenshot** → ưu tiên `ScreenCaptureWebRTCService`; tôn trọng `SCREENSHOTS_ENGINE_METHOD` và quyền OS (macOS Screen Recording).
5. **Activity** → ưu tiên tích hợp ActivityWatch; đừng giả định `DesktopEventCounter` đã đếm đủ kb/mouse.
6. **Auth/token** → sửa cả Akita store + electron-store auth + interceptors refresh.
7. **Chỉ ảnh hưởng full Desktop** (local API, Gauzy window) → `apps/desktop/src/index.ts` + `DesktopServer` / `createGauzyWindow`.
8. **WakaTime local** → `apps/desktop-api` + plugin `@gauzy/plugin-integration-wakatime`; port 5622 đã được settings UI coi là reserved.
9. **Test offline:** Tắt mạng → start/stop → bật mạng → theo dõi `SequenceQueue` và channel `UPDATE_SYNCED*`.
10. **Build:** Angular UI và Electron main compile tách (`tsconfig.electron.json`); sidecar `desktop-api` được đóng gói vào `dist/apps/desktop(-timer)/desktop-api`.

---

## PHỤ LỤC A — BẢN ĐỒ FILE THEN CHỐT

| Chủ đề | File / Symbol |
| :--- | :--- |
| **Electron main (full)** | `apps/desktop/src/index.ts` — `startServer`, `ipcMainHandler`, `fork(desktop-api)` |
| **Electron main (timer)** | `apps/desktop-timer/src/index.ts` |
| **Nest sidecar** | `apps/desktop-api/src/main.ts` → port `DESKTOP_API_DEFAULT_PORT` (5622); `AppModule` + `WakatimeModule` |
| **IPC hub** | `packages/desktop-lib/src/lib/desktop-ipc.ts` — `ipcMainHandler`, `ipcTimer` |
| **Timer engine** | `packages/desktop-lib/src/lib/desktop-timer.ts` — `TimerHandler` |
| **Offline mode** | `packages/desktop-lib/src/lib/offline/desktop-offline-mode-handler.ts` |
| **Sync queues** | `packages/desktop-ui-lib/src/lib/offline-sync/concretes/sequence-queue.ts` |
| **Screenshot** | `packages/desktop-ui-lib/.../screen-capture.service.ts` — `ScreenCaptureWebRTCService` |
| **API timer/screenshot** | `packages/desktop-ui-lib/.../time-tracker.service.ts` — `toggleApiStart`, `uploadImages` |
| **Auth IPC** | `packages/desktop-ui-lib/.../auth.service.ts` — `auth_success` |
| **Token refresh** | `packages/desktop-ui-lib/.../token-refresh.service.ts` |
| **Event counter** | `packages/desktop-activity/src/lib/desktop-event-counter.ts` |
| **Idle** | `packages/desktop-lib/.../power-manager-detect-inactivity.ts` |

---

## PHỤ LỤC B — SO SÁNH NHANH DESKTOP VS DESKTOP TIMER

| Tiêu chí | Desktop | Desktop Timer |
| :--- | :--- | :--- |
| **Full Gauzy UI window** | Có (`createGauzyWindow`) | Không |
| **Fork full Gauzy API (`api/main.js`)** | Có khi `isLocalServer` | Không |
| **Fork `desktop-api` (WakaTime)** | Có | Có |
| **Tracking / screenshot / offline** | Chung `desktop-lib` + `desktop-ui-lib` | Giống |
| **Protocol** | `gauzy-desktop://` | `gauzy-timer://` |
| **Mục tiêu đóng gói** | All-in-one / self-host nhẹ | Client tracking mỏng |

---

## KẾT LUẬN NGẮN

Desktop App của Gauzy là kiến trúc Electron hai tiến trình + sidecar NestJS, chia sẻ lõi tracking qua packages. Ba thư mục `apps/desktop`, `apps/desktop-timer`, và `apps/desktop-api` lần lượt là shell đầy đủ, shell timer, và API WakaTime local — còn “bộ não” nằm ở `desktop-lib` / `desktop-ui-lib`. Hiểu rõ ranh giới đó giúp mở rộng tính năng đúng chỗ, tránh sửa nhầm shell hoặc nhân đôi logic giữa hai sản phẩm.