[![](https://img.shields.io/nuget/v/soenneker.utils.debounce.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.debounce/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.debounce/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.debounce/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.utils.debounce.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.debounce/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.Debounce

A utility that lets you *debounce* work in .NET.
Give it a delay, async/sync delegate, and the `Debouncer` guarantees that multiple rapid calls collapse into exactly one invocation.

---

### Why would I need this?

* **API calls:** Prevent hammering a server while the user types.
* **Disk I/O:** Batch frequent save requests into a single write.
* **Telemetry:** Send aggregated metrics after bursts of activity.
* **Search boxes / auto-complete:** React only after the user pauses typing.

---

### Quick start

```bash
dotnet add package Soenneker.Utils.Debounce
```

```csharp
using Soenneker.Utils.Debounce;

var debouncer = new Debouncer();

// Fire only once, 300 ms after the *last* request:
void OnTextChanged(string text)
{
    debouncer.Debounce(
        delayMs: 300,
        action: async ct =>
        {
            var results = await SearchAsync(text, ct);
            UpdateUI(results);
        });
}

void OnResize()
{
    debouncer.Debounce(
        delayMs: 250,
        action: () =>
        {
            // Runs on the thread-pool after 250 ms of quiescence
            SaveWindowLayout();
        });
}
```

#### Leading-edge execution

Pass `runLeading: true` if you want the first call to run immediately **and** the trailing call to run after the quiet period:

```csharp
debouncer.Debounce(
    delayMs: 500,
    runLeading: true,
    action: ct => Logger.LogAsync("Burst started", ct));
```

Either wrap it in a `using` statement or dispose the debouncer when you’re done:

```csharp
await debouncer.DisposeAsync();
```

`DisposeAsync()` waits for any in-flight work to finish, ensuring graceful shutdown.

---

### Design highlights

* **Pure TPL:** Built on `System.Threading.Timer`
* **Thread-safe:** Internal state is guarded with `Interlocked` swaps.
* **Cancellation-friendly:** Each queued delegate receives *its own* `CancellationToken`.
* **Zero allocations on idle:** Work objects are created only when you call `Debounce`.
* **Tested:** xUnit suite covering timing, cancellation, and disposal semantics.