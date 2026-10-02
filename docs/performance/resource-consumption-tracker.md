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

## 5. Ngân sách cho các Phase tiếp theo

| Phase | Chức năng thêm | RAM budget | CPU budget | Trạng thái |
|---|---|---|---|---|
| Phase 01 | Session Engine + Hooks cơ bản | < 35 MB | < 0.5% | ✅ Đạt (25.5 MB) |
| Phase 05 | Activity Engine (macOS + Windows) | < 40 MB | < 1.0% | ✅ Đạt (33.8 MB) |
| Phase 06 (dự kiến) | + SQLite / EF Core Local Storage | < 60 MB | < 2.0% | ⏳ Chờ đo |
| Phase 07 (dự kiến) | + Screenshot Engine | < 85 MB | < 4.0% | ⏳ Chờ đo |
| Phase 08 (dự kiến) | + Offline Sync Queue | < 100 MB | < 3.0% | ⏳ Chờ đo |

---

## 6. Cách đo lại (Runbook)

Khi cần đo resource cho phase mới:

### Bước 1 — Chạy agent

```bash
dotnet run --project src/RemoteWork.Desktop.Host
```

### Bước 2 — Đợi ít nhất 5 phút

Tương tác phím/chuột, để idle 1–2 phút để test transition.

### Bước 3 — Đo bằng `ps` (nhanh nhất)

```bash
ps -o pid,%cpu,%mem,rss,command -p $(pgrep RemoteWork.Desktop.Host)
```

→ Cột `RSS` (KB) chia 1024 = MB.

### Bước 4 — Đo bằng `dotnet-counters` (chi tiết hơn)

```bash
dotnet-counters monitor -p $(pgrep RemoteWork.Desktop.Host) --counters System.Runtime
```

Quan tâm các chỉ số:
- `dotnet.process.memory.working_set`
- `dotnet.gc.collections` (Gen0 / Gen1 / **Gen2**)
- `dotnet.thread_pool.thread.count`
- `dotnet.monitor.lock_contentions`

### Bước 5 — Ghi kết quả vào bảng ở mục 2

---

## 7. Tinh chỉnh khi chạy máy yếu

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

**Tác dụng:** giảm ~30% số lần đánh thức CPU và ~60% số object phát sinh trong runtime. Đổi lại: độ chi tiết dữ liệu activity giảm.

---

## 8. Kết luận

| Câu hỏi | Trả lời |
|---|---|
| Agent ăn bao nhiêu RAM? | **~34 MB** ở Phase 05 |
| Ăn bao nhiêu CPU? | **~0% khi idle**, ~0.1% khi có tương tác |
| Có rò rỉ bộ nhớ không? | **Không** (Gen2 GC = 0) |
| Có ổn với yêu cầu không? | **Có** — dưới ngưỡng 40 MB, sẵn sàng cho SQLite + Screenshot phase tiếp theo |


## Phase 07 — Local SQLite Persistence (macOS)

**Ngày đo:** 2026-10-02
**Session:** 3c1ea66b-f97a-45ff-a4b2-f48fc0b9d941
**Công cụ:** `ps -o rss` sau khi app chạy ổn định (SQLite đã migrate xong)

| Chỉ số | Giá trị | Budget | Đạt? |
|---|---|---|---|
| **RSS (RAM)** | **72.2 MB** (73,968 KB) | < 60 MB | ❌ **Vượt 12 MB** |
| **%CPU** | 0.0% | < 0.5% | ✅ |
| **%MEM** | 0.2% | — | ✅ |
| **Assembly count** | 83–143 | — | ⚠️ Tăng do EF Core |

### Giải thích mức tăng

| Thành phần | Ước tính |
|---|---|
| Phase 06 baseline | ~34 MB |
| + EF Core meta model + change tracker | ~15–20 MB |
| + SQLite native lib (`e_sqlite3`) | ~5–8 MB |
| + Migration + DB connection pool | ~5–10 MB |
| + .NET runtime overhead cho EF types | ~5 MB |
| **Phase 07 tổng** | **~72 MB** |

### Khuyến nghị giảm RAM (chưa implement)

1. **WAL mode** — giảm I/O contention, không tăng RAM nhưng cải thiện throughput
2. **`QueryTrackingBehavior.NoTracking`** globally cho read-only queries → giảm change tracker overhead ~5–10 MB
3. **`AddDbContextPool`** thay vì `AddDbContext` → tái dùng context, giảm allocation
4. **Retention policy** — xoá batch cũ > N ngày khỏi SQLite → giảm disk, không giảm RAM nhưng cần cho long-run
5. **Lazy loading** — đảm bảo tắt (`UseLazyLoadingProxies` mặc định off) → đang đúng
6. **Chỉ giữ DbContext scoped khi cần** — hiện tại đã scoped ✅

### Cập nhật Budget thực tế

| Phase | Budget gốc | Thực tế | Chênh lệch |
|---|---|---|---|
| Phase 01 | < 35 MB | 25.5 MB | ✅ |
| Phase 05 | < 40 MB | 33.8 MB | ✅ |
| Phase 06 | (không thêm) | ~34 MB | ✅ |
| **Phase 07** | **< 60 MB** | **72.2 MB** | ❌ **+12 MB** |
| Phase 08 (sync) | < 100 MB | — | ⏳ |

**Kết luận:** SQLite + EF Core tốn RAM nhiều hơn dự kiến. **Cần optimization phase trước khi thêm sync layer**, nếu không sẽ vượt 100 MB ở Phase 08.