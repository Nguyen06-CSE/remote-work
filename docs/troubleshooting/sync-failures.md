# Troubleshooting Sync Failures

## 1. Overview

This guide provides diagnostic procedures and resolution steps for synchronization issues between the RemoteWork Desktop Agent and the remote backend ingestion API.

The sync pipeline consists of:
`Tracking Engine -> SQLite Persistence -> SyncQueue -> SyncEngine -> ISyncTransport`

---

## 2. Common Failure Scenarios

### 2.1 Backend Unavailable / Network Disconnected (Transient)

**Symptoms:**
- Log message: `Transport failed during sync. Marking system offline: Connection refused...`
- `IsOnline` flips to `false`.
- Queue items transition from `Pending` -> `InProgress` -> `Failed`.
- Tracking continues normally, but queue size increases.

**Root Cause:**
- Device has lost internet connection, is behind a captive portal, or backend service is undergoing maintenance / restart.

**Resolution:**
1. Verify network connectivity on the client machine.
2. Confirm backend health check endpoint is reachable.
3. No manual agent intervention is required: once connectivity is restored, the next background cycle of `SyncEngine` will automatically detect the connection, set `IsOnline = true`, and drain the accumulated queue.

---

### 2.2 Permanent Validation Rejection (HTTP 400 / Unrecoverable Schema Mismatch)

**Symptoms:**
- Log message: `Item <QueueItemId> failed permanently: Validation error... Will not retry.`
- Queue item has `Status = "Failed"`, `IsPermanentFailure = true`, and `NextAttemptAt = null`.
- Item is not retried on subsequent sync cycles.

**Root Cause:**
- Agent version payload schema mismatch with backend requirements (e.g. required field missing or corrupted payload JSON).

**Resolution:**
1. Inspect the error message stored in SQLite:
   ```sql
   SELECT QueueItemId, EntityType, EntityId, ErrorMessage, PayloadJson
   FROM SyncQueue
   WHERE IsPermanentFailure = 1;
   ```
2. Verify if the agent requires a software update to match newer backend API specifications.
3. If the payload is malformed or corrupted, it can be purged or corrected:
   ```sql
   DELETE FROM SyncQueue WHERE QueueItemId = '<QueueItemId>';
   ```

---

### 2.3 Orphaned In-Progress Items After System Crash

**Symptoms:**
- Agent was forcefully killed or machine abruptly lost power during active sync.
- Items in `SyncQueue` remain with `Status = "InProgress"`.

**Root Cause:**
- Process termination occurred after items were marked `InProgress` but before completion acknowledgement was written to SQLite.

**Resolution:**
- **Automatic Recovery:** When the Desktop Agent restarts, `SyncEngine.StartAsync()` automatically invokes `RecoverStaleInProgressItemsAsync()`, resetting all `InProgress` records to `Pending`.
- Log verification: Look for `Restart recovery: reset X in-progress items back to Pending.` in the startup logs.
- Manual recovery (if inspecting an offline database):
  ```sql
  UPDATE SyncQueue SET Status = 'Pending' WHERE Status = 'InProgress';
  ```

---

### 2.4 Exponential Backoff Throttling

**Symptoms:**
- Network connection was restored, but items remain in `Failed` state and are not immediately synced.

**Root Cause:**
- Items that failed multiple times are subject to exponential backoff. An item will not be selected by `GetEligibleForSyncAsync()` until `UtcNow >= NextAttemptAt`.
- Max backoff delay caps at 300 seconds (5 minutes).

**Resolution:**
- Wait for the backoff interval to expire, or force an immediate retry by resetting `NextAttemptAt`:
  ```sql
  UPDATE SyncQueue
  SET NextAttemptAt = NULL, Status = 'Pending'
  WHERE Status = 'Failed' AND IsPermanentFailure = 0;
  ```

---

## 3. SQLite Diagnostic Queries

To inspect the agent's sync state using the `sqlite3` CLI:

### Locate Database File:
- **macOS:** `~/Library/Application Support/RemoteWork/Agent/remotework.db`
- **Windows:** `%LOCALAPPDATA%\RemoteWork\Agent\remotework.db`
- **Linux:** `~/.local/share/RemoteWork/Agent/remotework.db`

### Check Queue Summary by Status:
```sql
SELECT Status, COUNT(*) AS Count, MIN(CreatedAt) AS Oldest, MAX(CreatedAt) AS Newest
FROM SyncQueue
GROUP BY Status;
```

### Check Active Retry Queue:
```sql
SELECT QueueItemId, EntityType, EntityId, AttemptCount, ErrorMessage, datetime(NextAttemptAt/10000000 - 62135596800, 'unixepoch') AS RetryTimeUTC
FROM SyncQueue
WHERE Status = 'Failed' AND IsPermanentFailure = 0
ORDER BY NextAttemptAt ASC;
```

### Clear Successfully Synced Items:
```sql
DELETE FROM SyncQueue WHERE Status IN ('Synced', 'Sent');
```
