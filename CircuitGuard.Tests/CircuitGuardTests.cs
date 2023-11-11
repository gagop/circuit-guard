using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CircuitGuard.Tests;

public class CircuitGuardTests
{
    private readonly ILogger<CircuitGuard> _logger = Substitute.For<ILogger<CircuitGuard>>();

    [Fact]
    public async Task ExecuteAsync_ActionExecutes_WhenCircuitClosed()
    {
        var circuitGuard = new CircuitGuard(1, TimeSpan.FromSeconds(10), _logger);
        bool actionExecuted = false;

        await circuitGuard.ExecuteAsync(() =>
        {
            actionExecuted = true;
            return Task.CompletedTask;
        }, default);

        Assert.True(actionExecuted);
    }
    
    [Fact]
    public async Task ExecuteAsync_ThrowsCircuitGuardOpenException_WhenCircuitOpen()
    {
        var circuitGuard = new CircuitGuard(1, TimeSpan.FromSeconds(10), _logger);

        // Simulate a failure to trip the circuit
        await Assert.ThrowsAsync<Exception>(() => circuitGuard.ExecuteAsync(() => throw new Exception(), default));

        // Subsequent call should throw CircuitGuardOpenException
        await Assert.ThrowsAsync<CircuitGuardOpenException>(() => circuitGuard.ExecuteAsync(() => Task.CompletedTask, default));
    }
    
    [Fact]
    public async Task ExecuteAsync_ResetsCircuit_WhenActionSucceedsAfterHalfOpenState()
    {
        var circuitGuard = new CircuitGuard(1, TimeSpan.FromSeconds(1), _logger);

        // Cause a failure
        await Assert.ThrowsAsync<Exception>(() => circuitGuard.ExecuteAsync(() => throw new Exception(), default));

        // Wait for the circuit to enter Half-Open state
        await Task.Delay(1200);

        // Action succeeds
        await circuitGuard.ExecuteAsync(() => Task.CompletedTask, default);

        Assert.Equal(CircuitGuardState.Closed, circuitGuard.State);
    }
    
    [Fact]
    public async Task ExecuteAsync_ReturnsValue_WhenCircuitClosedAndActionSucceeds()
    {
        var circuitGuard = new CircuitGuard(1, TimeSpan.FromSeconds(10), _logger);
        var expectedResult = 42;

        var result = await circuitGuard.ExecuteAsync(() => Task.FromResult(expectedResult), default);

        Assert.Equal(expectedResult, result);
    }
    
    [Fact]
    public async Task ExecuteAsync_ThrowsCircuitGuardOpenException_WhenCircuitOpenAndActionReturnsValue()
    {
        var circuitGuard = new CircuitGuard(1, TimeSpan.FromSeconds(10), _logger);

        // Simulate a failure to trip the circuit
        await Assert.ThrowsAsync<Exception>(() => circuitGuard.ExecuteAsync(() => throw new Exception(), default));

        // Subsequent call should throw CircuitGuardOpenException, even for a value-returning action
        await Assert.ThrowsAsync<CircuitGuardOpenException>(() => circuitGuard.ExecuteAsync(() => Task.FromResult(42), default));
    }
    
    [Fact]
    public async Task ExecuteAsync_ResetsCircuit_WhenActionReturnsValueAndSucceedsAfterHalfOpenState()
    {
        var circuitGuard = new CircuitGuard(1, TimeSpan.FromSeconds(1), _logger);
        var expectedResult = 42;

        // Cause a failure
        await Assert.ThrowsAsync<Exception>(() => circuitGuard.ExecuteAsync(() => throw new Exception(), default));

        // Wait for the circuit to enter Half-Open state
        await Task.Delay(1100);

        // Action succeeds and returns a value
        var result = await circuitGuard.ExecuteAsync(() => Task.FromResult(expectedResult), default);

        Assert.Equal(CircuitGuardState.Closed, circuitGuard.State);
        Assert.Equal(expectedResult, result);
    }
}