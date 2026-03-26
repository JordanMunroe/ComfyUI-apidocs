# Queue Management

This example shows how to inspect the ComfyUI execution queue, cancel running or pending prompts, and manage queue entries programmatically.

---

## Check Queue Status

`GET /queue` returns the currently running prompt and all pending prompts waiting to execute.

```javascript
/**
 * Fetches the current queue status.
 *
 * @returns {Promise<{ queue_running: object[], queue_pending: object[] }>}
 */
async function getQueueStatus() {
  const response = await fetch('http://127.0.0.1:8188/queue');
  if (!response.ok) throw new Error(`GET /queue failed: HTTP ${response.status}`);
  const queue = await response.json();

  console.log(`Running : ${queue.queue_running.length} item(s)`);
  console.log(`Pending : ${queue.queue_pending.length} item(s)`);

  // Each entry is [position, prompt_id, prompt_data, extra_data, output_ids]
  for (const [pos, promptId] of queue.queue_pending) {
    console.log(`  [${pos}] ${promptId}`);
  }

  return queue;
}
```

---

## Interrupt the Running Prompt

Send `POST /interrupt` to stop the prompt currently executing. The prompt is immediately halted and its partial outputs (if any) are written to history.

```javascript
/**
 * Interrupts the currently running prompt.
 * The server responds with an empty body on success.
 */
async function interruptCurrentPrompt() {
  const response = await fetch('http://127.0.0.1:8188/interrupt', {
    method: 'POST',
  });

  if (!response.ok) throw new Error(`POST /interrupt failed: HTTP ${response.status}`);
  console.log('Running prompt interrupted');
}
```

---

## Delete Pending Prompts from the Queue

Use `POST /queue` with a `delete` body to remove specific pending prompts by their `prompt_id`:

```javascript
/**
 * Removes one or more pending prompts from the queue.
 *
 * @param {string[]} promptIds - Array of prompt_id values to remove.
 */
async function deleteFromQueue(promptIds) {
  const response = await fetch('http://127.0.0.1:8188/queue', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ delete: promptIds }),
  });

  if (!response.ok) throw new Error(`POST /queue (delete) failed: HTTP ${response.status}`);
  console.log(`Deleted ${promptIds.length} prompt(s) from queue`);
}
```

---

## Clear the Entire Queue

To remove all pending prompts at once, fetch their IDs first, then delete them all:

```javascript
async function clearQueue() {
  const { queue_pending } = await getQueueStatus();

  // Each entry: [position, prompt_id, ...]
  const ids = queue_pending.map(([, promptId]) => promptId);

  if (ids.length === 0) {
    console.log('Queue is already empty');
    return;
  }

  await deleteFromQueue(ids);
  console.log(`Cleared ${ids.length} pending prompt(s)`);
}
```

---

## C\# Example

```csharp
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>Fetches and logs the current queue state.</summary>
static async Task PrintQueueStatusAsync(HttpClient client)
{
    var queue = await client.GetFromJsonAsync<JsonElement>("http://127.0.0.1:8188/queue");

    int running = queue.GetProperty("queue_running").GetArrayLength();
    int pending = queue.GetProperty("queue_pending").GetArrayLength();

    Console.WriteLine($"Running : {running}");
    Console.WriteLine($"Pending : {pending}");
}

/// <summary>Interrupts the currently running prompt.</summary>
static async Task InterruptAsync(HttpClient client)
{
    var response = await client.PostAsync(
        "http://127.0.0.1:8188/interrupt",
        new StringContent(""));
    response.EnsureSuccessStatusCode();
    Console.WriteLine("Interrupted");
}

/// <summary>Removes specific prompts from the pending queue.</summary>
static async Task DeleteFromQueueAsync(HttpClient client, string[] promptIds)
{
    var body     = new { delete = promptIds };
    var response = await client.PostAsJsonAsync("http://127.0.0.1:8188/queue", body);
    response.EnsureSuccessStatusCode();
    Console.WriteLine($"Deleted {promptIds.Length} prompt(s)");
}
```

---

## Poll Until Queue is Empty

```javascript
/**
 * Waits until both the running and pending queues are empty.
 * Prefer WebSocket monitoring for individual prompts; use this only
 * when you need to drain the entire queue before proceeding.
 *
 * @param {number} intervalMs - Polling interval in milliseconds.
 */
async function waitForQueueEmpty(intervalMs = 2000) {
  while (true) {
    const { queue_running, queue_pending } = await getQueueStatus();
    if (queue_running.length === 0 && queue_pending.length === 0) {
      console.log('Queue is empty');
      return;
    }
    await new Promise(resolve => setTimeout(resolve, intervalMs));
  }
}
```

---

## See Also

- [Minimal API Example](./minimal-api-example.md) — Full runnable example with WebSocket monitoring
- [Core Endpoints Reference](../core_endpoints.md) — Full `/queue` and `/interrupt` schemas
- [WebSocket Monitoring](./websocket-monitoring.md) — Preferred method for individual prompt monitoring
