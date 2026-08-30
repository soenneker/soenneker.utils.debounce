[![](https://img.shields.io/nuget/v/soenneker.utils.debounce.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.debounce/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.debounce/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.debounce/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.utils.debounce.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.utils.debounce/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.debounce/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.utils.debounce/actions/workflows/codeql.yml)

# Soenneker.Utils.Debounce

A thread-safe trailing-edge debouncer for synchronous or `Task`-based callbacks.

## Installation

```bash
dotnet add package Soenneker.Utils.Debounce
```

## Usage

Keep one debouncer for each independent stream of calls. Every call replaces that debouncer's pending callback and restarts its timer:

```csharp
await using var searchDebouncer = new Debouncer();

void OnTextChanged(string text, CancellationToken cancellationToken)
{
    searchDebouncer.Debounce(
        delayMs: 300,
        action: async ct =>
        {
            SearchResults results = await SearchAsync(text, ct);
            UpdateUi(results);
        },
        cancellationToken: cancellationToken);
}
```

If calls continue arriving inside the 300 ms window, only the callback from the last call remains pending. Different logical operations should use different `Debouncer` instances; calls made through one instance intentionally replace each other.

The cancellation token is the caller-supplied token. It prevents a pending callback from starting when already cancelled and is passed to the callback, but the debouncer does not cancel it when a newer call arrives. Work that has already started is not stopped or awaited by a later `Debounce()` call.

## Leading and trailing execution

```csharp
debouncer.Debounce(
    delayMs: 500,
    action: ct => RecordBurstAsync(ct),
    runLeading: true);
```

`runLeading: true` runs the first callback in a burst immediately and still schedules a trailing callback after the quiet period. The leading callback can overlap the trailing callback if it runs longer than the delay; callback code must be safe for that possibility.

## Lifetime and errors

Dispose the debouncer from its owner, not from inside one of its callbacks. Disposal drops pending work, prevents new calls, and waits for callbacks that already started. It does not cancel those running callbacks.

`Debounce()` is fire-and-forget and cannot return callback failures to its caller. Catch and log exceptions inside asynchronous callbacks when failure visibility matters:

```csharp
debouncer.Debounce(250, async ct =>
{
    try
    {
        await SaveAsync(ct);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Debounced save failed");
    }
});
```
