# Mouse Bot Detection — Policy & Algorithm Specification

## 1. Mục đích và Phạm vi
Tài liệu này quy định chính sách, nguyên lý toán học và cấu hình cho hệ thống phát hiện hành vi gian lận giả lập thao tác chuột (Bot/Auto-Clicker Detection) trên Desktop Agent của RemoteWork.

Tính năng này được thiết kế để phát hiện:
- Phần mềm auto-clicker chạy nền (click theo chu kỳ đều đặn).
- Macro script tự động click ngẫu nhiên trong một vùng cố định nhằm giữ máy tính không bị chuyển sang trạng thái Idle.
- Kịch bản tự động hóa click lặp đi lặp lại qua các điểm cố định trên màn hình.

---

## 2. Ràng buộc Bảo mật & Pháp lý (Privacy Compliance)

Theo nguyên tắc **Privacy by Design**:
1. **Tính chất dữ liệu**: Tọa độ chuột $(X, Y)$ được xem là dữ liệu nhạy cảm gián tiếp (có thể suy đoán hành vi người dùng). Do đó:
   - Dữ liệu tọa độ **tuyệt đối không được ghi vào file log**.
   - Dữ liệu tọa độ **không bao giờ được ghi xuống database SQLite cục bộ**.
   - Dữ liệu tọa độ **không bao giờ được gửi qua mạng lên Backend**.
2. **Vòng đời dữ liệu**: Tọa độ chỉ tồn tại trong bộ nhớ RAM tạm thời (Ring Buffer kích thước tối đa 200 mẫu) và bị thu hồi ngay lập tức sau khi hoàn tất chu kỳ phân tích (mỗi chu kỳ sampling 10 giây).
3. **Thông báo nhân viên**: Tính năng này là cơ chế chống gian lận trong thời gian làm việc đã được thỏa thuận trong hợp đồng lao động. Doanh nghiệp cần công bố chính sách trong tài liệu giám sát nội bộ.

---

## 3. Đặc tả 3 Thuật toán Nhận diện (Heuristics)

Thuật toán phân tích trên tập mẫu $\mathcal{S} = \{ s_1, s_2, \dots, s_n \}$ thu thập được từ hàm `DrainMouseSamples()`, với mỗi mẫu $s_i = (t_i, x_i, y_i)$:
- $t_i$: Timestamp tính bằng đơn vị milliseconds.
- $(x_i, y_i)$: Tọa độ pixel trên màn hình tại thời điểm click.

Kết quả chung cuộc:
$$\text{IsSuspicious} = \text{Algorithm\_A} \lor \text{Algorithm\_B} \lor \text{Algorithm\_C}$$

---

### 3.1. Thuật toán A: Phân tích Chu kỳ Thời gian (Temporal Periodicity)

#### Ý tưởng
Con người click chuột luôn có sự biến thiên tự nhiên về mặt thời gian phản xạ (Reaction Time Variance). Ngược lại, bot auto-clicker thường kích hoạt theo một timer có khoảng cách cố định (hoặc độ lệch rất nhỏ do trễ luồng OS).

#### Công thức toán học
1. Tính mảng khoảng cách thời gian giữa các lần click liên tiếp:
   $$\Delta t_i = t_{i+1} - t_i \quad (\forall i \in [1, n-1])$$
2. Tính giá trị trung bình $\mu$ (Mean) và độ lệch chuẩn $\sigma$ (Standard Deviation):
   $$\mu = \frac{1}{n-1} \sum_{i=1}^{n-1} \Delta t_i, \quad \sigma = \sqrt{\frac{1}{n-1} \sum_{i=1}^{n-1} (\Delta t_i - \mu)^2}$$
3. Tính hệ số biến thiên (Coefficient of Variation - $CV$):
   $$CV = \frac{\sigma}{\mu}$$

#### Điều kiện kích hoạt cờ Nghi ngờ
- Số lượng mẫu $n \ge \text{PeriodicityMinSamples}$ (mặc định: $30$).
- Giá trị trung bình $\mu$ nằm trong khoảng thời gian bot thường dùng: $5{,}000\text{ ms} \le \mu \le 300{,}000\text{ ms}$.
- Hệ số biến thiên $CV < \text{PeriodicityThresholdCv}$ (mặc định: $0.15$ tức độ lệch thời gian dưới 15%).

---

### 3.2. Thuật toán B: Phân tích Vùng Tọa độ Cố định (Spatial Bounding Box)

#### Ý tưởng
Một người làm việc bình thường sẽ di chuyển chuột khắp các vùng của màn hình (chuyển tab, bấm nút, chọn văn bản). Các auto-clicker đơn giản thường được đặt cố định tại một vị trí hoặc random xung quanh một vùng pixel nhỏ để bấm liên tục mà không gây ảnh hưởng đến phần tử khác.

#### Công thức toán học
1. Xác định Bounding Box bao quanh toàn bộ các điểm click trong buffer:
   $$\text{Width} = \max_{i}(x_i) - \min_{i}(x_i)$$
   $$\text{Height} = \max_{i}(y_i) - \min_{i}(y_i)$$
2. Diện tích bao phủ:
   $$\text{Area} = \text{Width} \times \text{Height} \quad (\text{pixel}^2)$$
3. Tổng thời gian quan sát:
   $$T_{\text{span}} = t_n - t_1$$

#### Điều kiện kích hoạt cờ Nghi ngờ
- Số lượng mẫu $n \ge 30$.
- Thời gian trải dài $T_{\text{span}} \ge 60{,}000\text{ ms}$ (ít nhất 1 phút click liên tục trong vùng đó).
- Diện tích $\text{Area} \le \text{FixedAreaMaxPixels}$ (mặc định: $10{,}000\text{ px}^2$, tương đương hình vuông $100 \times 100\text{ px}$).

---

### 3.3. Thuật toán C: Phân tích Chuỗi Lặp Đa Vùng (Cyclic Multi-Zone Sequence)

#### Ý tưởng
Các bot tinh vi hơn click xoay vòng giữa 2 đến 5 vị trí trên màn hình (ví dụ: click Tab A, sau đó click Tab B, rồi lặp lại) nhằm tránh thuật toán phát hiện vùng cố định.

#### Quy trình xử lý
1. **Lượng tử hóa không gian (Grid Clustering):**
   - Chia màn hình thành các ô lưới kích thước $100 \times 100\text{ px}$.
   - Gán mỗi điểm $(x_i, y_i)$ vào một Cluster ID $C(x_i, y_i) = (\lfloor x_i / 100 \rfloor, \lfloor y_i / 100 \rfloor)$.
2. **Tạo chuỗi ký hiệu:** Chuyển tập click thành chuỗi định danh:
   $$\mathcal{Q} = [c_1, c_2, \dots, c_n]$$
3. **Phát hiện chu kỳ lặp lại (Cycle Detection):**
   - Đếm số cluster duy nhất $k = \vert{}\text{Unique}(\mathcal{Q})\vert{}$. Nếu $k \notin [2, 5]$: bỏ qua.
   - Quét độ dài chu kỳ khả dĩ $p$ từ $2$ đến $\lfloor n / 3 \rfloor$.
   - Kiểm tra xem chuỗi có lặp lại ít nhất 3 chu kỳ liên tiếp hay không:
     $$c_i == c_{i+p} \quad (\forall i \in [0, 2p])$$

#### Điều kiện kích hoạt cờ Nghi ngờ
- Số cluster duy nhất $k \in [2, 5]$.
- Tồn tại chu kỳ lặp lại với số lần lặp $\ge 3$.
- Thời gian trải dài $T_{\text{span}} \ge 120{,}000\text{ ms}$ (2 phút).

---

## 4. Bảng Cấu hình Tham số (`TrackingOptions`)

Toàn bộ các ngưỡng được cấu hình tập trung trong `TrackingOptions` và có thể ghi đè qua `appsettings.json`:

| Tên cấu hình | Kiểu | Mặc định | Ý nghĩa |
|---|---|---|---|
| `MouseBotDetectionEnabled` | `bool` | `true` | Bật/tắt toàn bộ module phát hiện bot |
| `MouseSampleBufferSize` | `int` | `200` | Số lượng mẫu click tối đa giữ trong RAM ring buffer |
| `PeriodicityThresholdCv` | `double` | `0.15` | Ngưỡng hệ số biến thiên thời gian (CV) |
| `PeriodicityMinSamples` | `int` | `30` | Số lượng click tối thiểu để phân tích chu kỳ |
| `PeriodicityMinIntervalMs` | `int` | `5000` | Khoảng thời gian click tối thiểu bị coi là suspicious (5s) |
| `PeriodicityMaxIntervalMs` | `int` | `300000` | Khoảng thời gian click tối đa bị coi là suspicious (5 phút) |
| `FixedAreaMaxPixels` | `int` | `10000` | Diện tích bounding box tối đa bị coi là click cục bộ (px²) |
| `FixedAreaMinDurationMs` | `int` | `60000` | Thời lượng tối thiểu click trong vùng cố định (ms) |
| `MultiAreaMinClusters` | `int` | `2` | Số cụm tối thiểu trong phát hiện đa vùng |
| `MultiAreaMaxClusters` | `int` | `5` | Số cụm tối đa trong phát hiện đa vùng |

Ví dụ trong `appsettings.json`:
```json
{
  "Agent": {
    "MouseBotDetectionEnabled": true,
    "MouseSampleBufferSize": 200,
    "PeriodicityThresholdCv": 0.15,
    "FixedAreaMaxPixels": 10000
  }
}