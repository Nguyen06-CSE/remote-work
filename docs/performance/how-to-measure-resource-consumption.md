# Hướng dẫn Đo đạc & Giám sát Mức sử dụng Tài nguyên (Hardware Resource Measurement Guide)

Tài liệu này là **Runbook / Cheat-sheet** chuẩn lưu lại tất cả các câu lệnh và quy trình đo đạc mức tiêu hao phần cứng (CPU, RAM, Garbage Collection, Thread) của RemoteWork Desktop Agent.

Bao gồm 2 cách tiếp cận:
- **Cách 1: Sử dụng công cụ bên ngoài (CLI / OS Tools)** — Không cần đụng vào code.
- **Cách 2: Ghi log tự động từ bên trong code (In-Process Diagnostics)** — Đọc trực tiếp từ file log.

---

## CÁCH 1: SỬ DỤNG CÔNG CỤ BÊN NGOÀI (KHÔNG SỬA CODE)

Đây là cách đo chính xác và khách quan nhất theo chuẩn của Microsoft Runtime.

### 1.1. Dùng công cụ chính hãng `dotnet-counters` (Khuyên dùng nhất)

`dotnet-counters` là công cụ phân tích hiệu năng thời gian thực của .NET runtime, đo chính xác đến từng byte RAM và chu kỳ CPU mà không gây sai lệch dữ liệu.

#### Bước 1: Cài đặt công cụ (Chỉ cần chạy 1 lần duy nhất)

```bash
dotnet tool install --global dotnet-counters
```

*(Nếu đã cài từ trước, có thể update: `dotnet tool update --global dotnet-counters`)*

#### Bước 2: Chạy Desktop Agent

Mở Terminal 1 và khởi động ứng dụng:

```bash
dotnet run --project src/RemoteWork.Desktop.Host
```

#### Bước 3: Đo trực tiếp qua Terminal (Live Monitoring)

Mở Terminal 2 và chạy lệnh sau:

```bash
dotnet-counters monitor -n RemoteWork.Desktop.Host --counters System.Runtime
```

**Màn hình Terminal sẽ hiển thị trực tiếp bảng thông số (nhảy số liên tục):**

```text
[System.Runtime]
    % Time in GC since last GC (%)                                 0
    CPU Usage (%)                                                0.1
    Working Set (MB)                                            35.4
    GC Heap Size (MB)                                            4.8
    Gen 0 GC Count / 1 min                                         2
    Gen 1 GC Count / 1 min                                         0
    Gen 2 GC Count / 1 min                                         0
    Thread Pool Thread Count                                       4
    Lock Contention Count / 1 sec                                  0
```

#### Bước 4: Xuất kết quả đo ra file CSV / JSON (Lưu bằng chứng benchmark)

Nếu muốn đo trong vòng 5 phút rồi xuất file báo cáo để lưu vào `docs/`:

```bash
# Xuất ra file CSV
dotnet-counters monitor -n RemoteWork.Desktop.Host --counters System.Runtime --format csv -o benchmark-result.csv

# Hoặc xuất ra file JSON
dotnet-counters monitor -n RemoteWork.Desktop.Host --counters System.Runtime --format json -o benchmark-result.json
```

*(Nhấn `Ctrl + C` để dừng đo và lưu file).*

---

### 1.2. Đo trên hệ điều hành macOS

#### Lệnh Terminal (CLI):

```bash
# Xem CPU, RAM (RSS), Threads của tiến trình Host
top -pid $(pgrep RemoteWork.Desktop.Host)

# Hoặc xem snapshot 1 dòng thông số nhanh gọn:
ps -o pid,%cpu,%mem,rss,command -p $(pgrep RemoteWork.Desktop.Host)
```

> *Lưu ý:* Giá trị `RSS` chia cho 1024 sẽ ra số MB RAM thực tế tiến trình đang chiếm (Working Set).

#### Giao diện đồ họa (GUI):

1. Mở ứng dụng **Activity Monitor** (nhấn `Cmd + Space` gõ Activity Monitor).
2. Tại ô tìm kiếm góc phải, gõ `RemoteWork.Desktop.Host`.
3. Quan sát các cột: `% CPU`, `Memory`, `Threads`.

---

### 1.3. Đo trên hệ điều hành Windows

#### Lệnh PowerShell (CLI):

Mở PowerShell và chạy lệnh sau (lấy thông số tự động mỗi 3 giây):

```powershell
while ($true) {
    Get-Process -Name "RemoteWork.Desktop.Host" -ErrorAction SilentlyContinue | 
    Select-Object Id, 
                  ProcessName, 
                  @{Name="RAM (MB)"; Expression={[math]::Round($_.WorkingSet64 / 1MB, 2)}}, 
                  CPU, 
                  @{Name="Threads"; Expression={$_.Threads.Count}} | 
    Format-Table -AutoSize
    Start-Sleep -Seconds 3
}
```

#### Giao diện đồ họa (GUI):

1. Nhấn `Ctrl + Shift + Esc` mở **Task Manager**.
2. Tìm tiến trình `RemoteWork.Desktop.Host`.
3. Để xem chi tiết: Chuyển sang tab **Details**, click chuột phải vào tiêu đề cột chọn **Select columns** $\rightarrow$ Tích chọn thêm **Memory (active private working set)** và **Threads**.

---

## CÁCH 2: TỰ ĐỘNG GHI LOG TÀI NGUYÊN TỪ BÊN TRONG CODE (IN-PROCESS DIAGNOSTICS)

Cách này giúp Desktop Agent tự động in số liệu RAM/Thread vào file log mỗi khi gom mẻ (Batch Flush) mà không cần mở thêm terminal ngoài.

### 2.1. Thêm cấu hình bật/tắt vào `appsettings.Development.json`

Thêm cờ `EnableResourceDiagnostics` vào cấu hình để chỉ bật khi dev kiểm thử:

```json
{
  "Agent": {
    "EnableResourceDiagnostics": true,
    "ActivitySamplingIntervalSeconds": 10,
    "ActivityBatchIntervalSeconds": 60
  }
}
```

Cập nhật `src/RemoteWork.Desktop.Infrastructure/Configuration/AgentOptions.cs` (hoặc `TrackingOptions.cs`):

```csharp
public bool EnableResourceDiagnostics { get; set; } = false;
```

---

### 2.2. Đoạn code C# thu thập tài nguyên

Đoạn code sau sử dụng `System.Diagnostics.Process` và `GC` để đọc chính xác thông số của chính tiến trình đang chạy:

```csharp
private void LogSystemResourceUsage()
{
    using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
    
    // 1. RAM thực tế chiếm dụng từ OS (Working Set)
    var workingSetMb = currentProcess.WorkingSet64 / (1024.0 * 1024.0);

    // 2. RAM do .NET Garbage Collector đang quản lý (Managed Heap)
    var gcHeapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0);

    // 3. Số luồng OS đang cấp phát cho tiến trình
    var threadCount = currentProcess.Threads.Count;

    // 4. Thời gian CPU tích lũy
    var cpuTime = currentProcess.TotalProcessorTime.TotalSeconds;

    _logger.LogInformation(
        "[PERF-MONITOR] RAM WorkingSet: {WorkingSet:F2} MB | GC Heap: {GC:F2} MB | Threads: {Threads} | Total CPU Time: {CpuTime:F2}s",
        workingSetMb,
        gcHeapMb,
        threadCount,
        cpuTime);
}
```

---

### 2.3. Vị trí gắn code trong dự án

Nơi hợp lý nhất là đặt trong **`ActivityCollector.FlushBatch()`** (hoặc trong vòng lặp sampling của **`MonitoringService.cs`**):

Mở `src/RemoteWork.Desktop.Application/Collectors/ActivityCollector.cs`:

```csharp
public ActivityBatch FlushBatch()
{
    var session = _sessionCollector.GetCurrentSession()
        ?? throw new InvalidOperationException("No active session.");

    var now = DateTimeOffset.UtcNow;
    var startedAt = _batchStartedAt ?? now;

    var batch = new ActivityBatch
    {
        BatchId = Guid.NewGuid().ToString(),
        DeviceId = session.DeviceId,
        SessionId = session.SessionId,
        StartedAt = startedAt,
        EndedAt = now,
        KeyboardCount = _accumulator.KeyboardCount,
        MouseCount = _accumulator.MouseCount,
        ActiveDuration = _accumulator.ActiveDuration,
        IdleDuration = _accumulator.IdleDuration,
        HasSuspiciousMouseActivity = _accumulator.HasSuspiciousMouseActivity
    };

    // --- BẮT ĐẦU ĐO ĐẠC TÀI NGUYÊN (IN-PROCESS) ---
    try
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);
        var gcMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        
        _logger.LogInformation(
            "[RESOURCE-METRICS] BatchId={BatchId} | WorkingSet={Ram:F2} MB | GC Heap={GC:F2} MB | Threads={Threads}",
            batch.BatchId,
            workingSetMb,
            gcMb,
            process.Threads.Count);
    }
    catch
    {
        // Graceful degradation: Không để việc đọc telemetry ảnh hưởng đến luồng chính
    }
    // --- KẾT THÚC ĐO ĐẠC ---

    _accumulator.Reset();
    _batchStartedAt = now;
    _lastTimestamp = now;

    return batch;
}
```

#### Kết quả hiển thị trong Console/Log file:

```text
info: RemoteWork.Desktop.Application.Collectors.ActivityCollector[0]
      [RESOURCE-METRICS] BatchId=91c665c2-4423-49dd-8281-0a367c27f603 | WorkingSet=35.80 MB | GC Heap=5.12 MB | Threads=5
```

---

## BẢNG GIẢI THÍCH Ý NGHĨA CÁC CHỈ SỐ QUAN TRỌNG

Khi đọc kết quả, cần phân biệt rõ các khái niệm sau để không hiểu nhầm:

| Chỉ số | Ý nghĩa kỹ thuật | Ngưỡng an toàn của RemoteWork Agent |
| --- | --- | --- |
| **Working Set (MB)** | Tổng lượng RAM vật lý thực tế mà tiến trình đang chiếm từ hệ điều hành. Đây là con số quan trọng nhất phản ánh ứng dụng có tốn RAM hay không. | Phải luôn **< 45 MB** (Phase 1) và **< 65 MB** (khi có SQLite/Bot Detection). |
| **GC Heap Size (MB)** | Lượng bộ nhớ cấp phát cho các đối tượng C# do .NET runtime quản lý. Con số này luôn nhỏ hơn Working Set. | Thường duy trì quanh **4 – 8 MB**. |
| **CPU Usage (%)** | Phần trăm CPU trung bình sử dụng. | Khi nghỉ: **< 0.1%**.<br>Khi gõ phím/click liên tục: **< 0.5%**. |
| **Thread Count** | Số lượng luồng (threads) đang chạy. | Thông thường dao động từ **4 đến 8 luồng** (Main thread, Background Worker, Win32/macOS Hook thread, ThreadPool workers). Nếu số luồng tăng liên tục không giảm $\rightarrow$ Rò rỉ luồng (Thread leak). |