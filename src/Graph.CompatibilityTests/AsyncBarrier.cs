// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Releases a fixed number of participants at once, so contract tests can stage write contention
/// deterministically instead of leaning on timing. Each participant does its setup, signals, and
/// blocks until every other participant has signalled; the contended operation then runs on all of
/// them from the same starting line.
/// </summary>
/// <remarks>
/// A participant that fails before reaching the barrier must still call <see cref="Signal"/>, or the
/// remaining participants wait forever.
/// </remarks>
/// <param name="participantCount">The number of participants to wait for.</param>
internal sealed class AsyncBarrier(int participantCount)
{
    private readonly TaskCompletionSource<bool> allParticipantsReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int remainingParticipants = participantCount;

    /// <summary>Signals this participant's arrival and waits for the rest.</summary>
    public async Task SignalAndWaitAsync(CancellationToken cancellationToken)
    {
        Signal();
        await allParticipantsReady.Task.WaitAsync(cancellationToken);
    }

    /// <summary>Signals this participant's arrival without waiting.</summary>
    public void Signal()
    {
        if (Interlocked.Decrement(ref remainingParticipants) == 0)
        {
            allParticipantsReady.TrySetResult(true);
        }
    }
}
