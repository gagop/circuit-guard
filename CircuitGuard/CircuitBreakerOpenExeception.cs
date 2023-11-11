using System.Runtime.Serialization;

namespace CircuitGuard;

public class CircuitGuardOpenException : Exception
{
    public CircuitGuardOpenException(): base("Service in unavailable. CircuitBreaker is in open state.")
    {
    }

    protected CircuitGuardOpenException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public CircuitGuardOpenException(string? message) : base(message)
    {
    }

    public CircuitGuardOpenException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}