# Offline-First Sync Architecture

## 1. Overview

The RemoteWork Desktop tracking client operates on a strict **offline-first** principle. Tracking must never depend on an active network connection or the availability of the backend server.

The core pipeline is structured as follows:

```
Collector (Activity / Session / Application)
    │
    ▼
Local Persistence (SQLite)
    │
    ▼
Sync Queue (Persistent SQLite Queue)
    │
    ▼
Sync Engine (Controlled Retry & Backoff)
    │
    ▼
Transport Abstraction (ISyncTransport)
    │
    ▼
Backend (Remote Ingestion API)
```

Data collection writes synchronously to local SQLite storage. Local persistence succeeds regardless of internet connectivity. An independent, asynchronous background engine (`SyncEngine`) periodically reads queued items and transmits them to the remote backend via `ISyncTransport`.

---

## 2. Sync Queue Design

### 2.1 Storage & Schema

The sync queue is persisted directly in the local SQLite database in table `SyncQueue`.

| Column | Type | Notes |
|---|---|---|
| `QueueItemId` | TEXT (PK) | Unique GUID identifier for the queue entry |
| `EntityType` | TEXT | Logical entity classification: `"Session"`, `"ActivityBatch"`, `"ApplicationActivity"` |
| `EntityId` | TEXT | Stable unique identifier of the tracked domain entity (e.g. `BatchId`, `SessionId`) |
| `PayloadJson` | TEXT | Serialized JSON representation of the payload to transmit |
| `Status` | TEXT | Current queue state: `Pending`, `InProgress`, `Synced`, `Failed` |
| `CreatedAt` | INTEGER | UTC ticks (long) |
| `LastAttemptAt` | INTEGER? | UTC ticks (long) of the most recent sync attempt |
| `NextAttemptAt` | INTEGER? | UTC ticks (long) calculated from exponential backoff for next retry |
| `AttemptCount` | INTEGER | Total transmission attempts executed |
| `ErrorMessage` | TEXT? | Sanitized error or status message from last failed attempt |
| `IsPermanentFailure` | INTEGER | Boolean flag (0/1): true if failure is unrecoverable (e.g., validation rejection) |

Indexes:
- `IX_SyncQueue_Status`: Quick lookup of active statuses.
- `IX_SyncQueue_Status_CreatedAt`: Ordered extraction of pending items (FIFO).
- `IX_SyncQueue_EntityType_EntityId`: Fast idempotency checks during enqueue.

---

## 3. Explicit State Transitions

The queue item status is strictly managed through the domain state machine in `SyncQueueItem`:

```
               ┌────────────────────────────────────────────────────────┐
               │                                                        │
               ▼                                                        │
         [  PENDING  ] ─────────► [ IN_PROGRESS ] ──────────► [  SYNCED  ] (Terminal)
               ▲                         │
               │                         ▼
               └─────────────────── [  FAILED  ]
                 (Retry Backoff /
                  Crash Recovery)
```

### State Transition Rules

1. `Pending -> InProgress`:
   Triggered when the `SyncEngine` claims eligible items for a batch transmission. Increments `AttemptCount` and updates `LastAttemptAt`.
2. `InProgress -> Synced`:
   Triggered upon transport confirmation. Clears `ErrorMessage`, resets `NextAttemptAt`, and marks completion.
3. `InProgress -> Failed`:
   Triggered if transport or remote API rejects the item. Sets `ErrorMessage`, flags `IsPermanentFailure` if unrecoverable, and calculates `NextAttemptAt` based on exponential backoff.
4. `Failed -> InProgress`:
   Triggered on subsequent retry when `UtcNow >= NextAttemptAt`.
5. `InProgress -> Pending` (Crash / Restart Recovery):
   If the agent process terminates while items are in flight, the `SyncEngine` resets all orphaned `InProgress` items to `Pending` at startup, ensuring zero data loss.
6. `Synced -> *`:
   Terminal state. Transitions out of `Synced` are strictly prohibited and throw `InvalidOperationException`.

---

## 4. Idempotency & Duplicate Prevention

A major failure mode in distributed data ingestion is duplicate record creation resulting from network timeouts where the server received data but the client timed out awaiting acknowledgement.

### 4.1 Client-Side Dedup (`EnqueueIfNotExistsAsync`)
Before adding a record to `SyncQueue`, the repository checks if an item with matching `(EntityType, EntityId)` already exists.
- If it exists, enqueue is skipped, preventing redundant items from entering the queue.

### 4.2 Stable Unique Entity Identifiers
Every tracked record has a client-generated GUID:
- `ActivityBatch.BatchId`
- `Session.SessionId`
- `ApplicationActivity.ActivityId`

These IDs are embedded into the payload and sent as the primary key reference to the backend. When retried, the backend can upsert or deduplicate based on this stable ID.

---

## 5. Controlled Retry & Exponential Backoff

The sync engine uses exponential backoff to avoid hammering remote infrastructure during outages:

$$\text{delay} = \min\left(\text{InitialDelay} \times \text{Multiplier}^{(\text{AttemptCount} - 1)}, \text{MaxInterval}\right)$$

### Default Parameters (`SyncOptions`):
- `InitialRetryDelaySeconds`: 2 seconds
- `BackoffMultiplier`: 2.0
- `MaxRetryIntervalSeconds`: 300 seconds (5 minutes)
- `MaxRetryAttempts`: 10
- `SyncIntervalSeconds`: 15 seconds
- `BatchSize`: 25 items

### Permanent vs. Transient Errors
- **Transient Errors** (Network down, HTTP 502/503/504, timeout):
  `IsPermanentFailure = false`. Item is scheduled for retry with exponential backoff until `MaxRetryAttempts` is exceeded.
- **Permanent Errors** (HTTP 400 Bad Request, schema validation failure, malformed payload):
  `IsPermanentFailure = true`, `NextAttemptAt = null`. The item is quarantined in `Failed` state and will **never** be retried, preventing endless retry loops.

---

## 6. Offline Detection

Offline detection is decoupled from OS network adapter status. Network adapters may report "connected" (e.g., connected to Wi-Fi) while access to the backend is blocked (captive portal, firewall, server outage).

### Detection Rules:
1. Failed communication with the transport (connection refused, timeout, HTTP 5xx) immediately sets `IsOnline = false`.
2. While offline, tracking continues unaffected.
3. The sync engine throttles its background cycle (increasing poll interval up to `MaxRetryIntervalSeconds`) to prevent busy-looping.
4. Successful transport communication immediately restores `IsOnline = true` and triggers immediate drainage of queued items.

---

## 7. Transport Abstraction (`ISyncTransport`)

Communication with the backend is abstracted through `ISyncTransport`:

```csharp
public interface ISyncTransport
{
    Task<SyncResult> SendAsync(SyncQueueItem item, CancellationToken ct = default);
    Task<SyncBatchResult> SendBatchAsync(IReadOnlyList<SyncQueueItem> items, CancellationToken ct = default);
    Task<bool> CheckConnectivityAsync(CancellationToken ct = default);
}
```

The current implementation uses `InMemorySyncTransport`, providing deterministic controls for network availability, latency, partial batch failures, and error simulation. Production FastAPI HTTP transport will implement this exact interface in Phase 12.

---

## 8. Gauzy Reference Analysis

The Ever Gauzy open-source desktop time tracker architecture was analyzed as a reference implementation.

### 8.1 What Concept Was Useful
- **Local-first SQLite buffering**: Gauzy uses a local SQLite database (`gauzy.sqlite3`) as the single source of truth prior to backend synchronization.
- **State Machine on synchronization items**: Tracking whether a timeslot or interval has been synced via explicit status flags (`synced: false`, `SYNCING`, `SYNCED`, `FAILED`).
- **Pinging / API-driven offline mode**: Gauzy's `DesktopOfflineModeHandler` determines offline status via active API reachability rather than OS network adapter flags.

### 8.2 What Was Adapted
- **Persistent SQLite queue**: We created a dedicated, indexed `SyncQueue` table with UTC tick timestamps for high performance on SQLite.
- **Controlled retry with exponential backoff**: Unlike simple fixed polling, our sync engine incorporates exponential backoff and limits retry attempts.
- **Idempotency-first design**: Unified `EntityId` ensures client and backend deduplication.

### 8.3 What Was Intentionally Simplified
- **Unified Polymorphic Queue**: Gauzy separates sync into multiple distinct queue structures (`SequenceQueue` for timers, `TimeSlotQueue` for intervals, `ScreenshotQueue` for images) managed across Electron IPC boundaries and Angular Akita state stores. RemoteWork unifies all uploadable entities into a single generic `SyncQueueItem` queue processed by a centralized `SyncEngine`.
- **Elimination of IPC overhead**: In RemoteWork, tracking and sync occur within the unified .NET headless runtime, avoiding complex serialization across Electron process bridges.
- **Deterministic state transitions**: Replaced RxJS-based reactive queue state listeners with a clear, thread-safe domain state machine.
