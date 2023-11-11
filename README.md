# CircuitGuard

<img src="/assets/state.png" alt="State diagram showing all the possible states of CircuitGuard" />

A circuit breaker library for .NET, CircuitGuard is made to improve your application's stability and fault tolerance. In distributed systems like microservices, when controlling external service dependencies is essential, it is very helpful. CircuitGuard facilitates smooth degradation and stops cascading failures, which contribute to the upkeep of a service environment.

The Circuit Breaker pattern is a software design pattern used to enhance system stability and resilience. It prevents a system from repeatedly trying to execute an operation that's likely to fail, thus protecting the system from cascading failures. When the number of failures for an operation exceeds a certain threshold, the circuit breaker "trips" and temporarily blocks further attempts to perform the operation, allowing the system to recover.

## Possible states of CircuitGuard
- **Closed State** - In the Closed state, the circuit breaker allows all operations to proceed normally. It is the initial state where requests or actions are executed without any restrictions. The system monitors for failures during this state, and if the number of failures crosses a predefined threshold, the circuit breaker transitions to the Open state. Successful operations will reset the failure count.

- **Open State** - When in the Open state, the circuit breaker blocks all operations or requests to the underlying system or service. This state is activated when the failure count in the Closed state exceeds the threshold. The Open state is maintained for a specified timeout period to give the failing system time to recover. No operations are allowed to pass through during this period. After the timeout elapses, the circuit breaker moves to the Half-Open state to test the stability of the system.

- **Half-Open State** - The Half-Open state is a transitional state where the circuit breaker allows a limited number of test operations to pass through to the underlying system or service. This state is entered from the Open state after the timeout period has elapsed. If these test operations are successful, indicating that the system has recovered, the circuit breaker transitions back to the Closed state, resuming normal operation. However, if these test operations fail, it indicates that the system is still not stable, and the circuit breaker returns to the Open state.

## Installation

To install CircuitGuard, use the following NuGet command:

```sh
dotnet add package CircuitGuard
```

## Example
Registering a service
```cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<CircuitGuard>(sp => 
        new CircuitGuard(
            threshold: 5, 
            timeout: TimeSpan.FromMinutes(1),
            logger: sp.GetRequiredService<ILogger<CircuitGuard>>()));
}
```

Using the CircuitGuard
```cs
[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly CircuitGuard _circuitGuard;

    public WeatherForecastController(CircuitGuard circuitGuard)
    {
        _circuitGuard = circuitGuard;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            await _circuitGuard.ExecuteAsync(async () =>
            {
                // Call to an external service
            });

            return Ok("Operation succeeded");
        }
        catch (CircuitGuardOpenException)
        {
            return StatusCode(503, "Service Unavailable");
        }
    }
}
```

## License
CircuitGuard is licensed under MIT License.