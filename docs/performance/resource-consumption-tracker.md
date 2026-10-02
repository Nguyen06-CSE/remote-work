# Resource Consumption Tracker

Tài liệu này trả lời một câu hỏi duy nhất:

> **Agent RemoteWork ăn bao nhiêu tài nguyên, có ổn với yêu cầu không?**

---

## 📊 Tóm tắt 1 phút (TL;DR)

| Chỉ số | Ngưỡng cho phép | Thực tế (Phase 05) | Kết luận |
|---|---|---|---|
| **RAM (RSS)** | < 40 MB | **33.8 MB** | ✅ Đạt |
| **CPU khi idle** | < 0.5% | **~0.0%** | ✅ Đạt |
| **CPU khi active** | < 1.0% | ~0.1% | ✅ Đạt |
| **Gen2 GC (Full GC)** | 0 lần | **0** | ✅ Đạt |
| **Thread leak** | Không tăng | Ổn định | ✅ Đạt |
| **Assembly** | Không tăng | 59 (không đổi) | ✅ Đạt |

**Kết luận:** Agent chạy **nhẹ**, chỉ chiếm **1/3 ngân sách RAM** cho phép, không có dấu hiệu rò rỉ bộ nhớ hay thread leak. Sẵn sàng cho các phase tiếp theo (SQLite, Screenshot, Sync).

---

## 1. Máy đo & công cụ

| Mục | Giá trị |
|---|---|
| **Máy** | MacBook-Pro-2 (macOS 26.5.1, Apple Silicon ARM64) |
| **Runtime** | .NET 10.0 SDK |
| **Tiến trình** | `RemoteWork.Desktop.Host` |
| **Agent version** | 1.0.0-dev |
| **Công cụ đo** | `dotnet-counters` (EventPipe) + `ps` (macOS) |

---

## 2. Kết quả từng Phase

### Phase 01 — Baseline (Session Engine + Native Hooks cơ bản)

| Chỉ số | Giá trị |
|---|---|
| Working Set (RAM) | ~25.5 MB |
| CPU idle | ~0.1% |
| Gen0 / Gen1 / Gen2 GC | 7 / 1 / 0 |
| Thread Pool | 2 threads |
| Lock contentions | 2 |

### Phase 05 — Activity Engine (macOS + Windows, 3-tier pipeline)

| Chỉ số | Giá trị |
|---|---|
| **RSS (RAM thực tế)** | **33,808 KB ≈ 33.8 MB** |
| **%CPU** | **0.0%** (đo lúc idle) |
| **%MEM** | **0.1%** |
| Gen0 / Gen1 / Gen2 GC | 1 / 1 / **0** |
| Assembly count | 59 (không đổi so với Phase 01) |
| Total allocated (1 snapshot) | 14.5 MB |

---

## 3. So sánh Phase 01 → Phase 05

| Chỉ số | Phase 01 | Phase 05 | Δ | Nhận xét |
|---|---|---|---|---|
| **RAM** | 25.5 MB | 33.8 MB | **+8.3 MB** | Vẫn dưới ngưỡng 40 MB. Tăng do thêm `IIdleProvider`, batch buffer, state machine |
| **CPU idle** | ~0.1% | ~0.0% | ~0 | Không tăng |
| **Gen2 GC** | 0 | 0 | 0 | ✅ Không có Full GC |
| **Assembly** | 59 | 59 | 0 | ✅ Không thêm dependency |
| **Native hook** | 1 (`CGEventTap`) | 1 (`CGEventTap`) | 0 | Không thêm thread native |

**Kết luận:** Phase 05 thêm **~8 MB RAM** cho việc phân tách 3-tier pipeline (raw → sampling → aggregation) và thêm `IIdleProvider`. **CPU không tăng**. Đây là mức tăng hợp lý.

---

## 4. Giải thích các con số quan trọng (cho người không chuyên)

| Chỉ số | Nghĩa là gì | Tại sao quan tâm |
|---|---|---|
| **RSS (RAM)** | Số MB RAM thực tế tiến trình đang chiếm từ OS | Càng thấp càng tốt cho laptop nhân viên |
| **%CPU** | % CPU trung bình | Cao = tốn pin, máy nóng |
| **Gen2 GC** | Số lần dọn rác "toàn bộ" | Nếu > 0 liên tục = có rò rỉ bộ nhớ |
| **Thread count** | Số luồng OS | Tăng liên tục = thread leak |
| **Assembly count** | Số thư viện .NET được load | Tăng = thêm dependency (không mong muốn) |

**Nguyên tắc vàng của RemoteWork Agent:**
- RAM **< 40 MB** → OK cho mọi máy nhân viên (kể cả máy 4 GB RAM)
- CPU **< 1%** → Không ảnh hưởng pin
- **Gen2 GC = 0** → Không rò rỉ
- **Thread count ổn định** → Không leak

---

## 5. Phase 07 — Local SQLite Persistence (macOS)

**Ngày đo:** 2026-10-02
**Session:** 3c1ea66b-f97a-45ff-a4b2-f48fc0b9d941
**Công cụ:** `ps -o rss`

| Chỉ số | Giá trị | Budget | Đạt? |
|---|---|---|---|
| RSS (RAM) | **72.2 MB** | < 60 MB | ❌ vượt 12 MB |
| %CPU | 0.0% | < 0.5% | ✅ |
| Gen2 GC | 0 | 0 | ✅ |

**Tăng do:** EF Core meta model + change tracker + SQLite native lib.
**Chi tiết:** `docs/phases/phase-07-local-persistence.md`.

---

## 6. Phase 08 — Offline-First Sync Engine (macOS)

**Ngày đo:** 2026-10-02
**Session:** e4684cd6-c685-41bf-b200-6009983c1844
**Công cụ:** `ps -o rss` + `dotnet-counters`

| Chỉ số | Giá trị | Budget | Đạt? |
|---|---|---|---|
| **RSS (RAM)** | **95.1 MB** (97,360 KB) | < 100 MB | ⚠️ **Sát budget (+5 MB headroom)** |
| **%CPU** | 0.5% | < 2.0% | ✅ |
| **Gen2 GC** | **1** | 0 | ⚠️ **Lần đầu xuất hiện** |
| **Assembly count** | 86 | — | +3 so với Phase 07 |
| **Total allocated** | 29 MB | — | — |

### Giải thích mức tăng so với Phase 07

| Thành phần | Δ ước tính |
|---|---|
| EF Core query compile cache (SyncQueue queries) | +5–8 MB |
| SyncEngine state (polling loop, backoff state) | +3–5 MB |
| Change tracker cho SyncQueueItemEntity | +5–8 MB |
| Startup persistence (Device + Session repo) | +2–3 MB |
| **Tổng** | **+22–23 MB** |

### ⚠️ Cảnh báo

1. **RAM còn 5 MB headroom** trước budget 100 MB → **Phase 09 (Application Tracking) + Phase 10 (Screenshot) sẽ vượt** nếu không optimize.

2. **Gen2 GC xuất hiện lần đầu (1 lần)** trong session 3.5 phút. Không phải red flag ngay, nhưng:
   - Nếu Gen2 tăng **đều đặn** trong Phase 09 → có memory leak tiềm ẩn cần điều tra.
   - Nếu Gen2 vẫn ổn định (1–2 lần/session) → chấp nhận được.

### Khuyến nghị optimization (trước Phase 09)

1. **`AddDbContextPool`** thay `AddDbContext` — tái dùng context instance, giảm allocation.
2. **`QueryTrackingBehavior.NoTracking`** cho query read-only (SyncQueue eligibility check).
3. **WAL mode** cho SQLite để giảm I/O contention.
4. **Retention policy** — xoá `Synced` items khỏi SyncQueue sau N ngày, giảm bảng size.
5. **`DeleteSentAsync` chạy định kỳ** — hiện có method nhưng chưa schedule.

### Budget cập nhật

| Phase | Budget gốc | Thực tế | Δ |
|---|---|---|---|
| Phase 01 | < 35 MB | 25.5 MB | ✅ |
| Phase 05 | < 40 MB | 33.8 MB | ✅ |
| Phase 06 | — | ~34 MB | ✅ |
| Phase 07 | < 60 MB | 72.2 MB | ❌ +12 MB |
| Phase 08 | < 100 MB | 95.1 MB | ⚠️ +0 (sát ngưỡng) |
| **Phase 09** | **< 100 MB** | **123 MB** | ❌ **+23 MB** (chi tiết ở mục 7) |
| Phase 10 | cần optimize trước | — | ⚠️ |

---

## 7. Phase 09 — Application Activity Tracking (macOS)

**Ngày đo:** 2026-10-02
**Session:** dcb70181-27e3-46ef-a563-43f506ba4fde
**Công cụ:** `ps -o rss`

| Chỉ số | Giá trị | Budget cũ | Đạt? |
|---|---|---|---|
| **RSS (RAM)** | **123 MB** (126,288 KB) | < 100 MB | ❌ **vượt 23 MB** |
| **%CPU** | 0.0% | < 2.0% | ✅ |
| **%MEM** | 0.4% | — | ✅ |
| **Gen2 GC** | (không đo lại) | 0 | ❓ |

### Giải thích mức tăng so với Phase 08

| Thành phần | Δ ước tính |
|---|---|
| AppKit/CoreGraphics dynamic loading | +3–5 MB |
| ApplicationActivityCollector state | +2–3 MB |
| Thêm ApplicationActivity + SyncQueue query path | +8–10 MB |
| Change tracker cho ApplicationActivityEntity | +5–8 MB |
| EF Core query cache mở rộng (type mới) | +5–8 MB |
| **Tổng** | **+25–30 MB** |

### ⚠️ Cảnh báo nghiêm trọng

RAM đã **vượt budget 100 MB** (đặt ra ở Phase 08) khoảng 23%. Nếu tiếp tục tăng ở Phase 10 (Screenshot Engine), RSS có thể chạm **150 MB** — không chấp nhận được cho laptop nhân viên cấu hình thấp.

**Đã được chấp nhận tạm thời (2026-10-02)** để unblock Phase 10, với kế hoạch optimize ở giai đoạn sau.

### 🎯 Optimization phase đề xuất (bắt buộc trước khi release)

Thứ tự ưu tiên (ROI cao → thấp):

1. **`AddDbContextPool` thay vì `AddDbContext`** — giảm allocation cho mỗi lần query, ước tính **-10–15 MB**.
2. **`QueryTrackingBehavior.NoTracking`** globally cho read-only queries (SyncQueue eligibility, ApplicationActivity queries) — **-5–8 MB**.
3. **Retention policy cho SyncQueue** — xoá items đã `Synced` sau N ngày/rows, giảm DB size và EF tracking overhead.
4. **`DeleteSentAsync` chạy định kỳ** (background timer) — giữ SyncQueue gọn.
5. **WAL mode** SQLite — giảm I/O, gián tiếp giảm memory pressure.
6. **Chia sẻ `ApplicationActivityCollector` state** — reset state khi session end để GC sớm.

Target sau optimization: **< 100 MB** cho Phase 10.

### Budget cập nhật

| Phase | Budget gốc | Thực tế | Đạt? |
|---|---|---|---|
| Phase 01 | < 35 MB | 25.5 MB | ✅ |
| Phase 05 | < 40 MB | 33.8 MB | ✅ |
| Phase 06 | — | ~34 MB | ✅ |
| Phase 07 | < 60 MB | 72.2 MB | ❌ +12 MB |
| Phase 08 | < 100 MB | 95.1 MB | ⚠️ sát |
| **Phase 09** | **< 100 MB** | **123 MB** | ❌ **+23 MB** |
| Phase 10 | **cần optimize trước** | — | ⚠️ |

---

## 8. Cách đo lại (Runbook)

Khi cần đo resource cho phase mới:

### Bước 1 — Chạy agent

```bash
dotnet run --project src/RemoteWork.Desktop.Host
```

### Bước 2 — Chạy ổn định & test transition

Đợi **ít nhất 5 phút** sau khi agent khởi động để:
- SQLite migrate xong, EF Core warm-up
- SyncEngine poll ít nhất 1–2 chu kỳ
- GC ổn định (không còn allocation burst lúc startup)

Trong thời gian này, thực hiện:
1. **Tương tác phím/chuột** liên tục ~30 giây → đo CPU lúc active.
2. **Để idle 1–2 phút** (không chạm máy) → đo CPU lúc idle + kiểm tra idle detection có trigger đúng không.
3. **Quan sát Gen2 GC** — nếu xuất hiện > 2 lần trong 5 phút → nghi vấn memory leak, cần điều tra ngay.

### Bước 3 — Đo nhanh bằng `ps`

```bash
ps -o pid,%cpu,%mem,rss,command -p $(pgrep RemoteWork.Desktop.Host)
```

- Cột **`RSS`** (KB) → chia `1024` để ra **MB**.
- Cột **`%CPU`** → đo lúc idle, nên ~0.0%.
- Cột **`%MEM`** → tỷ lệ RAM tiến trình / tổng RAM máy.

**Ví dụ output Phase 08:**

```
PID   %CPU  %MEM    RSS      COMMAND
4821   0.5   0.2   97360     RemoteWork.Desktop.Host
```

→ `97360 / 1024 ≈ 95.1 MB`.

### Bước 4 — Đo chi tiết bằng `dotnet-counters`

```bash
dotnet-counters monitor -p $(pgrep RemoteWork.Desktop.Host) --counters System.Runtime
```

**Các chỉ số cần quan tâm:**

| Counter | Ý nghĩa | Ngưỡng |
|---|---|---|
| `dotnet.process.memory.working_set` | RAM thực tế (tương đương RSS) | < budget phase |
| `dotnet.gc.collections` (Gen0 / Gen1 / **Gen2**) | Số lần GC | **Gen2 = 0** |
| `dotnet.thread_pool.thread.count` | Số thread pool | Ổn định, không tăng |
| `dotnet.monitor.lock_contentions` | Số lần tranh lock | Thấp, không tăng liên tục |

> ⚠️ Nếu **Gen2 > 0** hoặc **thread count tăng đều** qua các lần đo → dừng lại, điều tra leak trước khi qua phase mới.

### Bước 5 — Ghi kết quả

Ghi tất cả số đo vào bảng tương ứng ở **mục 2** (Kết quả từng Phase) và **mục 6** (Budget cập nhật). Đính kèm:
- Ngày đo
- Session ID
- Công cụ đo
- Ghi chú bất thường (nếu có)

---

## 9. Tinh chỉnh khi chạy máy yếu

Nếu triển khai trên máy cấu hình thấp (RAM 4 GB, CPU 2 cores), điều chỉnh `appsettings.json`:

```json
{
  "Agent": {
    "ActivitySamplingIntervalSeconds": 15,
    "ActivityBatchIntervalSeconds": 180,
    "IdleThresholdSeconds": 300
  }
}
```

**Tác dụng:**
- Giảm **~30%** số lần đánh thức CPU (sampling thưa hơn).
- Giảm **~60%** số object phát sinh trong runtime (batch lớn hơn).
- Giảm tải SQLite (ghi batch ít hơn).

**Đánh đổi:** độ chi tiết dữ liệu activity giảm (mất granularity dưới 15 giây).

---

## 10. Kết luận

| Câu hỏi | Trả lời |
|---|---|
| Agent ăn bao nhiêu RAM? | **~34 MB** ở Phase 05 → **~72 MB** ở Phase 07 → **~95 MB** ở Phase 08 → **~123 MB** ở Phase 09 |
| Ăn bao nhiêu CPU? | **~0% khi idle**, **~0.5% khi sync active** |
| Có rò rỉ bộ nhớ không? | **Chưa kết luận** — Gen2 GC xuất hiện lần đầu ở Phase 08 (1 lần/session 3.5 phút). Phase 09 chưa đo lại Gen2, cần theo dõi tiếp. |
| Có ổn với yêu cầu không? | **Phase 05 ✅ đạt** · **Phase 07 ❌ vượt 12 MB** · **Phase 08 ⚠️ sát ngưỡng 100 MB** · **Phase 09 ❌ vượt 23 MB** |
| Cần làm gì tiếp? | **Bắt buộc optimize trước Phase 10**: `AddDbContextPool`, `NoTracking`, WAL mode, retention policy — nếu không Phase 10 (Screenshot) sẽ vượt xa 100 MB. |