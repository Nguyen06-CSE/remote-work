# 📚 Tài liệu dự án — Điểm vào nhanh

> README này giúp bạn tìm đúng tài liệu cần đọc trong vài giây. Click vào liên kết để mở trực tiếp.

---

## 🚀 Bắt đầu từ đâu?

| Bạn muốn... | Đọc file |
|---|---|
| Hiểu bức tranh tổng thể & lộ trình | [reference/desktop-app-architecture-roadmap.md](reference/desktop-app-architecture-roadmap.md) |
| Xem kiến trúc đích của desktop app | [architecture/target-desktop-architecture.md](architecture/target-desktop-architecture.md) |
| Biết quyết định kiến trúc đã chốt | [adr/](#-architecture-decision-records-adr) |
| Đọc tài liệu tham khảo từ Gauzy | [reference/gauzy/](#-tham-khảo-gauzy) |
| Xem kết quả các spike run | [spike-runs/](#-spike-runs-kết-quả-thực-nghiệm) |
| Gỡ lỗi khi gặp sự cố | [troubleshooting/](#-troubleshooting-xử-lý-sự-cố) |
| Làm việc với AI agent | [ai-development/master-agent-instruction.md](ai-development/master-agent-instruction.md) |

---

## 🏛️ Architecture Decision Records (ADR)

Các quyết định kiến trúc quan trọng — đọc trước khi thay đổi thiết kế:

- [ADR-001 — Cross-platform desktop architecture](adr/ADR-001-cross-platform-desktop-architecture.md)
- [ADR-002 — Native activity providers](adr/ADR-002-native-activity-providers.md)
- [ADR-003 — Mouse bot detection](adr/ADR-003-mouse-bot-detection.md)
- [ADR-004 — Dynamic library loading & activity sampling lifecycle](adr/ADR-004-dynamic-library-loading-and-activity-sampling-lifecycle.md)

---

## 🏗️ Kiến trúc hệ thống

- [Core Domain](architecture/core-domain.md) — mô hình domain cốt lõi
- [Application Tracking](architecture/application-tracking.md) — theo dõi ứng dụng
- [Local Storage](architecture/local-storage.md) — lưu trữ cục bộ
- [Offline Sync](architecture/offline-sync.md) — đồng bộ khi offline
- [Platform Abstraction](architecture/platform-abstraction.md) — lớp trừu tượng đa nền tảng
- [Platform Activity](architecture/platform-activity.md) — hoạt động theo nền tảng
- [Project Boundaries](architecture/project-boundaries.md) — ranh giới module
- [Target Desktop Architecture](architecture/target-desktop-architecture.md) — kiến trúc đích
- [Desktop App Platform Decision](architecture/DESKTOP_APP_PLATFORM_DECISION.md) — quyết định nền tảng

---

## 📦 Phases (lộ trình triển khai)

- [Phase 01 — Rearchitecture](phases/phase-01-rearchitecture.md)
- [Phase 02 — Core Domain](phases/phase-02-core-domain.md)
- [Phase 03 — Device Identity](phases/phase-03-device-identity.md)
- [Phase 04 — Session Engine](phases/phase-04-session-engine.md)
- [Phase 05 — Activity Engine](phases/phase-05-activity-engine.md)
- [Phase 06 — Activity Monitoring Runtime](phases/phase-06-activity-monitoring-runtime.md)
- [Phase 07 — Local Persistence](phases/phase-07-local-persistence.md)
- [Phase 08 — Offline Sync](phases/phase-08-offline-sync.md)
- [Phase 09 — Application Tracking](phases/phase-09-application-tracking.md)
- [Phase 09 — Diagnostic](phases/phase-09-diagnostic.md)

---

## 🧪 Spike runs (kết quả thực nghiệm)

Mỗi thư mục chứa `summary.md` của lần chạy tương ứng:

- [Phase 01 — macOS (2026-09-20)](spike-runs/phase-01-rearchitecture/run-2026-09-20-1430-macos/summary.md)
- [Phase 02 — macOS (2026-10-02)](spike-runs/phase-02/run-2026-10-2-macos/summary.md)
- [Phase 03 — macOS (2026-10-02)](spike-runs/phase-03/run-2026-10-02-macos/summary.md)
- [Phase 04 — macOS (2026-10-02)](spike-runs/phase-04/run-2026-10-02-macos/summary.md)
- [Phase 05 — macOS (2026-10-02)](spike-runs/phase-05/run-2026-10-02-macos/summary.md)
- [Phase 05 — Windows (2026-10-02)](spike-runs/phase-05/run-2026-10-02-windows/summary.md)
- [Phase 06 — macOS (2026-10-02)](spike-runs/phase-06/run-2026-10-02-macos/summary.md)
- [Phase 07 — macOS (2026-10-02)](spike-runs/phase-07/run-2026-10-02-macos/summary.md)
- [Phase 08 — macOS (2026-10-02)](spike-runs/phase-08/run-2026-10-02-macos/summary.md)
- [Phase 09 — macOS (2026-10-02)](spike-runs/phase-09/run-2026-10-02-macos/summary.md)

---

## 🔍 Monitoring

- [Monitoring Policy](monitoring/monitoring-policy.md) — chính sách giám sát
- [Monitoring Scope](monitoring/monitoring-scope.md) — phạm vi giám sát
- [Mouse Bot Detection Design](monitoring/mouse-bot-detection-design.md) — thiết kế phát hiện bot chuột

---

## ⚡ Performance

- [How to Measure Resource Consumption](performance/how-to-measure-resource-consumption.md) — cách đo tài nguyên
- [Resource Consumption Tracker](performance/resource-consumption-tracker.md) — theo dõi tiêu thụ tài nguyên

---

## 🔌 API & AI Development

- [Agent API Contract](api/agent-api-contract.md) — hợp đồng API cho agent
- [Master Agent Instruction](ai-development/master-agent-instruction.md) — hướng dẫn cho AI agent

---

## 📖 Tham khảo Gauzy

- [Gauzy Desktop App Architecture](reference/gauzy/gauzy-desktop-app-architecture.md)
- [Gauzy Desktop Architecture Detail](reference/gauzy/gauzy-desktop-architecture-detail.md)
- [Desktop App Architecture Roadmap](reference/desktop-app-architecture-roadmap.md)

---

## 🛠️ Troubleshooting (xử lý sự cố)

- [Collector Error Isolation](troubleshooting/collector-error-isolation.md)
- [Cross-platform Build](troubleshooting/cross-platform-build.md)
- [SQLite FK Constraint Failed](troubleshooting/sqlite-fk-constraint-failed.md)
- [SQLite Vulnerability Warning](troubleshooting/sqlite-vulnerability-warning.md)
- [Sync Failures](troubleshooting/sync-failures.md)

---

## 📄 Khác

- [Native Capability Spike](native-capability-spike.md) — khảo sát năng lực native

---

> 💡 **Gợi ý:** Nếu bạn mới tham gia dự án, hãy đọc theo thứ tự: **Roadmap → ADR → Target Architecture → Phase hiện tại → Spike run gần nhất**.