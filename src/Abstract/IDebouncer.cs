using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Utils.Debounce.Abstract;

/// <summary>
/// Delays execution of an async action until a specified interval has passed without new invocations. Ideal for throttling high-frequency operations like UI events, logging, or API calls.
/// </summary>
public interface IDebouncer : IAsyncDisposable
{
    /// <summary>
    /// Queues <paramref name="action"/> to run once no debounced call was
    /// made in the previous <paramref name="delayMs"/> milliseconds.
    /// </summary>
    /// <param name="delayMs">Quiet-period length in milliseconds.</param>
    /// <param name="action">Async delegate to execute.</param>
    /// <param name="runLeading">
    /// If true, execute once immediately, then again after the delay.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional token that cancels the *pending* execution (not the debouncer).
    /// </param>
    void Debounce(int delayMs, Func<CancellationToken, Task> action, bool runLeading = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues <paramref name="action"/> to run once no debounced call was
    /// made in the previous <paramref name="delayMs"/> milliseconds.
    /// </summary>
    /// <param name="delayMs">Quiet-period length in milliseconds.</param>
    /// <param name="action">Delegate to execute.</param>
    /// <param name="runLeading">
    /// If true, execute once immediately, then again after the delay.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional token that cancels the *pending* execution (not the debouncer).
    /// </param>
    void Debounce(int delayMs, Action<CancellationToken> action, bool runLeading = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues <paramref name="action"/> to run once no debounced call was
    /// made in the previous <paramref name="delayMs"/> milliseconds.
    /// </summary>
    /// <param name="delayMs">Quiet-period length in milliseconds.</param>
    /// <param name="action">Delegate to execute.</param>
    /// <param name="runLeading">
    /// If true, execute once immediately, then again after the delay.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional token that cancels the *pending* execution (not the debouncer).
    /// </param>
    void Debounce(int delayMs, Action action, bool runLeading = false, CancellationToken cancellationToken = default);
}