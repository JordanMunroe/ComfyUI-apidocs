# Example: Queue Management

This example demonstrates how to manage the ComfyUI execution queue, including checking status, clearing items, and interrupting running workflows.

## Overview

The queue system in ComfyUI manages all workflow executions. Understanding queue management is essential for:
- Preventing queue overload
- Canceling unwanted executions
- Prioritizing important workflows
- Building responsive applications

## Prerequisites

- ComfyUI server running (default: `http://127.0.0.1:8188`)
- Python 3.7+ with `requests` library

## Complete Example

```python
import requests
import json
import time
from typing import Dict, List, Optional

COMFYUI_URL = "http://127.0.0.1:8188"

def get_queue_status() -> Dict:
    """
    Get current queue status.
    
    Returns:
        dict: Queue information with running and pending items
    """
    response = requests.get(f"{COMFYUI_URL}/queue")
    
    if response.status_code == 200:
        return response.json()
    else:
        print(f"✗ Failed to get queue: {response.status_code}")
        return {}

def print_queue_summary(queue_data: Dict):
    """
    Print a human-readable queue summary.
    """
    running = queue_data.get('queue_running', [])
    pending = queue_data.get('queue_pending', [])
    
    print("=" * 60)
    print("Queue Status")
    print("=" * 60)
    
    if running:
        print(f"\n🔄 Running ({len(running)} item(s)):")
        for item in running:
            queue_num, prompt_id = item[0], item[1]
            extra_data = item[3] if len(item) > 3 else {}
            client_id = extra_data.get('client_id', 'unknown')
            print(f"  [{queue_num}] {prompt_id[:8]}... (client: {client_id[:8]}...)")
    else:
        print("\n🔄 Running: None")
    
    if pending:
        print(f"\n⏳ Pending ({len(pending)} item(s)):")
        for item in pending:
            queue_num, prompt_id = item[0], item[1]
            extra_data = item[3] if len(item) > 3 else {}
            client_id = extra_data.get('client_id', 'unknown')
            print(f"  [{queue_num}] {prompt_id[:8]}... (client: {client_id[:8]}...)")
    else:
        print("\n⏳ Pending: None")
    
    print("=" * 60)

def clear_queue() -> bool:
    """
    Clear all pending items from the queue.
    
    Returns:
        bool: True if successful
    """
    response = requests.post(
        f"{COMFYUI_URL}/queue",
        json={"clear": True}
    )
    
    if response.status_code == 200:
        print("✓ Queue cleared successfully")
        return True
    else:
        print(f"✗ Failed to clear queue: {response.status_code}")
        return False

def delete_queue_items(prompt_ids: List[str]) -> bool:
    """
    Delete specific items from the queue.
    
    Args:
        prompt_ids: List of prompt IDs to delete
    
    Returns:
        bool: True if successful
    """
    response = requests.post(
        f"{COMFYUI_URL}/queue",
        json={"delete": prompt_ids}
    )
    
    if response.status_code == 200:
        print(f"✓ Deleted {len(prompt_ids)} item(s) from queue")
        return True
    else:
        print(f"✗ Failed to delete items: {response.status_code}")
        return False

def interrupt_execution(prompt_id: Optional[str] = None) -> bool:
    """
    Interrupt the currently executing workflow.
    
    Args:
        prompt_id: Optional specific prompt ID to interrupt
                  If None, interrupts current execution
    
    Returns:
        bool: True if successful
    """
    data = {}
    if prompt_id:
        data['prompt_id'] = prompt_id
        print(f"Interrupting prompt: {prompt_id}")
    else:
        print("Interrupting current execution")
    
    response = requests.post(
        f"{COMFYUI_URL}/interrupt",
        json=data if data else {}
    )
    
    if response.status_code == 200:
        print("✓ Interrupt signal sent")
        return True
    else:
        print(f"✗ Failed to interrupt: {response.status_code}")
        return False

def get_queue_info() -> Dict:
    """
    Get simplified queue information.
    
    Returns:
        dict: Queue info with remaining count
    """
    response = requests.get(f"{COMFYUI_URL}/prompt")
    
    if response.status_code == 200:
        return response.json()
    else:
        print(f"✗ Failed to get queue info: {response.status_code}")
        return {}

def wait_for_queue_empty(timeout: int = 300, poll_interval: int = 2) -> bool:
    """
    Wait until the queue is empty.
    
    Args:
        timeout: Maximum time to wait in seconds
        poll_interval: How often to check in seconds
    
    Returns:
        bool: True if queue became empty, False if timeout
    """
    print(f"Waiting for queue to empty (timeout: {timeout}s)...")
    start_time = time.time()
    
    while (time.time() - start_time) < timeout:
        info = get_queue_info()
        exec_info = info.get('exec_info', {})
        remaining = exec_info.get('queue_remaining', 0)
        
        if remaining == 0:
            print("✓ Queue is empty")
            return True
        
        print(f"  Queue remaining: {remaining}", end='\r')
        time.sleep(poll_interval)
    
    print(f"\n✗ Timeout after {timeout} seconds")
    return False

def monitor_queue(duration: int = 60, interval: int = 5):
    """
    Monitor queue status over time.
    
    Args:
        duration: How long to monitor in seconds
        interval: Update interval in seconds
    """
    print(f"Monitoring queue for {duration} seconds (interval: {interval}s)")
    print("Press Ctrl+C to stop\n")
    
    start_time = time.time()
    
    try:
        while (time.time() - start_time) < duration:
            queue = get_queue_status()
            running = len(queue.get('queue_running', []))
            pending = len(queue.get('queue_pending', []))
            
            timestamp = time.strftime('%H:%M:%S')
            print(f"[{timestamp}] Running: {running}, Pending: {pending}")
            
            time.sleep(interval)
    except KeyboardInterrupt:
        print("\n✓ Monitoring stopped")

def submit_and_manage_workflow(workflow: Dict, client_id: str) -> Optional[str]:
    """
    Submit a workflow and demonstrate queue management.
    
    Args:
        workflow: The workflow definition
        client_id: Client identifier
    
    Returns:
        str: Prompt ID if successful
    """
    # Submit workflow
    print("Submitting workflow...")
    response = requests.post(
        f"{COMFYUI_URL}/prompt",
        json={
            "prompt": workflow,
            "client_id": client_id
        }
    )
    
    if response.status_code != 200:
        print(f"✗ Failed to submit: {response.status_code}")
        return None
    
    result = response.json()
    prompt_id = result['prompt_id']
    print(f"✓ Submitted with ID: {prompt_id}")
    
    # Check queue
    time.sleep(0.5)
    queue = get_queue_status()
    print_queue_summary(queue)
    
    return prompt_id

# Usage Examples

if __name__ == "__main__":
    print("ComfyUI Queue Management Examples\n")
    
    # Example 1: Check queue status
    print("\n" + "="*60)
    print("Example 1: Check Queue Status")
    print("="*60)
    queue = get_queue_status()
    print_queue_summary(queue)
    
    # Example 2: Get queue info (simple)
    print("\n" + "="*60)
    print("Example 2: Get Queue Info")
    print("="*60)
    info = get_queue_info()
    exec_info = info.get('exec_info', {})
    remaining = exec_info.get('queue_remaining', 0)
    print(f"Items remaining in queue: {remaining}")
    
    # Example 3: Delete specific items
    print("\n" + "="*60)
    print("Example 3: Delete Specific Items")
    print("="*60)
    # First, get current queue
    queue = get_queue_status()
    pending = queue.get('queue_pending', [])
    
    if pending:
        # Delete the first pending item
        prompt_id_to_delete = pending[0][1]
        print(f"Deleting prompt: {prompt_id_to_delete}")
        delete_queue_items([prompt_id_to_delete])
    else:
        print("No pending items to delete")
    
    # Example 4: Clear entire queue
    print("\n" + "="*60)
    print("Example 4: Clear Queue")
    print("="*60)
    queue = get_queue_status()
    pending_count = len(queue.get('queue_pending', []))
    
    if pending_count > 0:
        confirm = input(f"Clear {pending_count} pending item(s)? (y/n): ")
        if confirm.lower() == 'y':
            clear_queue()
    else:
        print("Queue is already empty")
    
    # Example 5: Interrupt execution
    print("\n" + "="*60)
    print("Example 5: Interrupt Execution")
    print("="*60)
    queue = get_queue_status()
    running = queue.get('queue_running', [])
    
    if running:
        confirm = input("Interrupt current execution? (y/n): ")
        if confirm.lower() == 'y':
            interrupt_execution()
    else:
        print("Nothing is currently running")
    
    # Example 6: Monitor queue
    print("\n" + "="*60)
    print("Example 6: Monitor Queue")
    print("="*60)
    monitor = input("Monitor queue for 30 seconds? (y/n): ")
    if monitor.lower() == 'y':
        monitor_queue(duration=30, interval=3)
```

## Advanced: Queue Management with Priorities

While ComfyUI doesn't have native priority levels, you can implement priority-like behavior:

```python
def submit_with_priority(workflow: Dict, client_id: str, high_priority: bool = False):
    """
    Submit workflow with priority flag.
    High priority items are added to the front of the queue.
    """
    response = requests.post(
        f"{COMFYUI_URL}/prompt",
        json={
            "prompt": workflow,
            "client_id": client_id,
            "front": high_priority  # Add to front of queue
        }
    )
    
    if response.status_code == 200:
        result = response.json()
        priority_str = "HIGH" if high_priority else "NORMAL"
        print(f"✓ Submitted with {priority_str} priority: {result['prompt_id']}")
        return result
    else:
        print(f"✗ Submission failed: {response.status_code}")
        return None

# Usage
submit_with_priority(workflow, client_id, high_priority=True)
```

## Queue Analysis

Analyze queue contents:

```python
def analyze_queue():
    """
    Provide detailed queue analysis.
    """
    queue = get_queue_status()
    running = queue.get('queue_running', [])
    pending = queue.get('queue_pending', [])
    
    print("📊 Queue Analysis")
    print("="*60)
    
    # Total counts
    print(f"Total items: {len(running) + len(pending)}")
    print(f"  Running: {len(running)}")
    print(f"  Pending: {len(pending)}")
    
    # Analyze by client
    client_counts = {}
    for item in running + pending:
        extra_data = item[3] if len(item) > 3 else {}
        client_id = extra_data.get('client_id', 'unknown')
        client_counts[client_id] = client_counts.get(client_id, 0) + 1
    
    if client_counts:
        print("\nBy Client:")
        for client_id, count in sorted(client_counts.items(), key=lambda x: -x[1]):
            print(f"  {client_id[:16]}...: {count} item(s)")
    
    # Analyze queue numbers (workflow order)
    if pending:
        queue_nums = [item[0] for item in pending]
        print(f"\nQueue Number Range: {min(queue_nums)} - {max(queue_nums)}")
    
    print("="*60)
```

## Auto-retry Failed Workflows

Automatically retry if a workflow fails:

```python
def submit_with_retry(workflow: Dict, client_id: str, max_retries: int = 3):
    """
    Submit workflow with automatic retry on failure.
    """
    for attempt in range(max_retries):
        print(f"Attempt {attempt + 1}/{max_retries}")
        
        response = requests.post(
            f"{COMFYUI_URL}/prompt",
            json={"prompt": workflow, "client_id": client_id}
        )
        
        if response.status_code == 200:
            result = response.json()
            
            # Check for validation errors
            if not result.get('node_errors'):
                print(f"✓ Successfully queued: {result['prompt_id']}")
                return result
            else:
                print(f"⚠️  Validation errors: {result['node_errors']}")
                if attempt < max_retries - 1:
                    print("Retrying...")
                    time.sleep(1)
        else:
            print(f"✗ HTTP error: {response.status_code}")
            if attempt < max_retries - 1:
                time.sleep(2 ** attempt)  # Exponential backoff
    
    print(f"✗ Failed after {max_retries} attempts")
    return None
```

## Queue Throttling

Prevent queue overload:

```python
class QueueThrottler:
    """
    Throttle workflow submissions based on queue size.
    """
    def __init__(self, max_pending: int = 5, max_total: int = 10):
        self.max_pending = max_pending
        self.max_total = max_total
    
    def can_submit(self) -> bool:
        """Check if we can submit a new workflow."""
        queue = get_queue_status()
        running = len(queue.get('queue_running', []))
        pending = len(queue.get('queue_pending', []))
        
        if pending >= self.max_pending:
            print(f"⚠️  Pending limit reached: {pending}/{self.max_pending}")
            return False
        
        if (running + pending) >= self.max_total:
            print(f"⚠️  Total queue limit reached: {running + pending}/{self.max_total}")
            return False
        
        return True
    
    def wait_for_slot(self, timeout: int = 300, poll_interval: int = 2) -> bool:
        """Wait until a queue slot is available."""
        start_time = time.time()
        
        while (time.time() - start_time) < timeout:
            if self.can_submit():
                return True
            
            print("Waiting for queue slot...", end='\r')
            time.sleep(poll_interval)
        
        print(f"\n✗ Timeout waiting for queue slot")
        return False

# Usage
throttler = QueueThrottler(max_pending=5, max_total=10)

if throttler.can_submit():
    # Submit workflow
    pass
else:
    # Wait for slot or skip
    if throttler.wait_for_slot(timeout=60):
        # Submit workflow
        pass
```

## Clean Up Old Queue Items

Remove stale items from queue:

```python
def clean_old_pending_items(max_age_seconds: int = 3600):
    """
    Remove pending items older than specified age.
    Note: This requires tracking submission times separately.
    """
    queue = get_queue_status()
    pending = queue.get('queue_pending', [])
    
    current_time = time.time()
    to_delete = []
    
    for item in pending:
        prompt_id = item[1]
        extra_data = item[3] if len(item) > 3 else {}
        create_time = extra_data.get('create_time', 0) / 1000  # Convert from ms
        
        age = current_time - create_time
        if age > max_age_seconds:
            to_delete.append(prompt_id)
            print(f"Removing old item: {prompt_id} (age: {age/60:.1f} minutes)")
    
    if to_delete:
        delete_queue_items(to_delete)
        print(f"✓ Removed {len(to_delete)} old item(s)")
    else:
        print("No old items to remove")
```

## Next Steps

- [Simple workflow execution](./simple-workflow-execution.md)
- [WebSocket monitoring](./websocket-monitoring.md)
- [Download outputs](./download-outputs.md)

## Related Documentation

- [Queue Management API](../API.md#queue-management)
- [Workflow Execution API](../API.md#workflow-execution)
- [Interrupt Endpoint](../API.md#interrupt-execution)
