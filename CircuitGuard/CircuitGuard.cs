using Microsoft.Extensions.Logging;

namespace CircuitGuard;

public class CircuitGuard
{
    private readonly ILogger<CircuitGuard>? _logger;
    private int _failureCount;
    private readonly int _threshold;
    private readonly TimeSpan _timeout;
    private DateTime _lastFailedTime;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CircuitGuardState State { get; private set; } = CircuitGuardState.Closed;
    public event EventHandler? StateChanged;
    
    /// <summary>
    /// Initializes a new instance of the CircuitGuard class with specified threshold and timeout.
    /// </summary>
    /// <param name="threshold">The failure threshold to trip the circuit.</param>
    /// <param name="timeout">The duration for which the circuit remains open before transitioning to half-open.</param>
    public CircuitGuard(int threshold, TimeSpan timeout)
    {
        _threshold = threshold;
        _timeout = timeout;
    }
    
    /// <summary>
    /// Initializes a new instance of the CircuitGuard class with specified threshold, timeout, and logger.
    /// </summary>
    /// <param name="threshold">The failure threshold to trip the circuit.</param>
    /// <param name="timeout">The duration for which the circuit remains open before transitioning to half-open.</param>
    /// <param name="logger">The logger used for logging events and errors.</param>
    public CircuitGuard(int threshold, TimeSpan timeout, ILogger<CircuitGuard> logger)
    {
        _threshold = threshold;
        _timeout = timeout;
        _logger = logger;
    }

    /// <summary>
    /// Executes the provided action within the circuit breaker logic.
    /// </summary>
    /// <param name="action">The action to be executed.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            switch (State)
            {
                case CircuitGuardState.Closed:
                    await ExecuteActionClosedStateAsync(action, cancellationToken);
                    break;
                case CircuitGuardState.Open:
                    await ExecuteActionOpenState(action, cancellationToken);
                    break;
                case CircuitGuardState.HalfOpen:
                    await ExecuteActionHalfOpenStateAsync(action, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Invalid state in CircuitBreaker");
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// Executes the provided action within the circuit breaker logic and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">The action to be executed.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The result from the action if it completes successfully.</returns>
    public async Task<T?> ExecuteAsync<T>(Func<Task<T?>> action, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            switch (State)
            {
                case CircuitGuardState.Closed:
                    return await ExecuteActionClosedStateAsync(action, cancellationToken);
                case CircuitGuardState.Open:
                    return await ExecuteActionOpenState(action, cancellationToken);
                case CircuitGuardState.HalfOpen:
                    return await ExecuteActionHalfOpenStateAsync(action, cancellationToken);
                default:
                    throw new InvalidOperationException("Invalid state in CircuitBreaker");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ExecuteActionClosedStateAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action();
            Reset();
        }
        catch (OperationCanceledException exc)
        {
            if (exc.CancellationToken == cancellationToken)
            {
                _logger?.LogInformation("Operation was cancelled.");
                return;
            }
            
            throw;
        }
        catch (Exception exc)
        {
            _logger?.LogError(exc, "Error during action execution in closed state.");
            RecordFailure();
            if (_failureCount >= _threshold)
            {
                Trip();
            }
            throw;
        }
    }
    
    private async Task<T?> ExecuteActionClosedStateAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try
        { 
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action();
            Reset();
            return result;
        }
        catch (OperationCanceledException exc)
        {
            if (exc.CancellationToken == cancellationToken)
            {
                _logger?.LogInformation("Operation was cancelled.");
                return default;
            }
            
            throw;
        }
        catch (Exception exc)
        {
            _logger?.LogError(exc, "Error during action execution in closed state.");
            RecordFailure();
            if (_failureCount >= _threshold)
            {
                Trip();
            }
            throw;
        }
    }

    private async Task ExecuteActionHalfOpenStateAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action();
            Reset();
        }
        catch (OperationCanceledException exc)
        {
            if (exc.CancellationToken == cancellationToken)
            {
                _logger?.LogInformation("Operation was cancelled.");
                return;
            }
            
            throw;
        }
        catch (Exception exc)
        {
            _logger?.LogError(exc, "Error during action execution in half-open state.");
            Trip();
            throw;
        }
    }
    
    private async Task<T?> ExecuteActionHalfOpenStateAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action();
            Reset();
            return result;
        }
        catch (OperationCanceledException exc)
        {
            if (exc.CancellationToken == cancellationToken)
            {
                _logger?.LogInformation("Operation was cancelled.");
                return default;
            }
            
            throw;
        }
        catch (Exception exc)
        {
            _logger?.LogError(exc, "Error during action execution in half-open state.");
            Trip();
            throw;
        }
    }

    private async Task ExecuteActionOpenState(Func<Task> action, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _lastFailedTime > _timeout)
        {
            State = CircuitGuardState.HalfOpen;
            await ExecuteActionHalfOpenStateAsync(action, cancellationToken);
        }
        else
        {
            throw new CircuitGuardOpenException();
        }
    }
    
    private async Task<T?> ExecuteActionOpenState<T>(Func<Task<T?>> action, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _lastFailedTime <= _timeout) throw new CircuitGuardOpenException();
        
        State = CircuitGuardState.HalfOpen;
        return await ExecuteActionHalfOpenStateAsync(action, cancellationToken);
    }
    
    private void Reset()
    {
        if (State == CircuitGuardState.Closed) return;
        State = CircuitGuardState.Closed;
        _failureCount = 0;
        OnStateChanged();
    }

    private void RecordFailure()
    {
        _failureCount++;
        _lastFailedTime=DateTime.UtcNow;
    }

    private void Trip()
    {
        if (State == CircuitGuardState.Open) return;
        State = CircuitGuardState.Open;
        OnStateChanged();
    }
    
    private void OnStateChanged()
    {
        _logger?.LogInformation($"Circuit breaker state changed to: {State}");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    
    
}