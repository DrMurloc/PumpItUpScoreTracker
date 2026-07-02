using MediatR;

namespace ScoreTracker.SharedKernel.Messaging;

/// <summary>
///     A read request: returns data, causes no side effects, and never travels the bus.
///     Commands are <see cref="IRequest" /> records named *Command; bus trigger messages
///     are plain *Command records in Application/Messages; facts are *Event records.
/// </summary>
public interface IQuery<out TResult> : IRequest<TResult>
{
}
