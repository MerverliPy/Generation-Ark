using System;
using System.Collections.Generic;
using System.Linq;
using GenerationArk.Simulation.Core;
using GenerationArk.Simulation.Diagnostics;

namespace GenerationArk.Simulation.Scheduling;

public sealed class DeterministicScheduler : ISimScheduler
{
    private const int DefaultMaxEventsPerPhase = 100_000;

    private readonly ScheduledEventHandlerRegistry _handlers;
    private readonly ScheduledEventHeap _heap = new();
    private readonly Dictionary<ScheduledEventId, ScheduledEvent> _activeById = new();
    private readonly Dictionary<ScheduledEventOwnerId, SortedSet<ScheduledEventId>> _idsByOwner = new();
    private readonly int _maxEventsPerPhase;

    private ulong _nextEventId = 1;
    private ulong _nextCreationSequence = 1;
    private bool _isTickExecuting;
    private ScheduledEvent? _executingEvent;
    private bool _executingEventCancelled;

    public DeterministicScheduler(
        ScheduledEventHandlerRegistry handlers,
        int maxEventsPerPhase = DefaultMaxEventsPerPhase)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        if (maxEventsPerPhase <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEventsPerPhase),
                maxEventsPerPhase,
                "Maximum events per phase must be positive.");
        }

        _maxEventsPerPhase = maxEventsPerPhase;
    }

    public SimTick CurrentTick { get; private set; } = SimTick.Zero;
    public SimPhase? CurrentPhase { get; private set; }
    public int PendingCount => _activeById.Count;

    public ScheduledEventId ScheduleAt(
        SimTick dueTick,
        SimPhase phase,
        int priority,
        ScheduledEventData eventData)
        => ScheduleCore(
            NormalizeDueTick(dueTick, phase),
            phase,
            priority,
            eventData,
            repeatIntervalTicks: null);

    public ScheduledEventId ScheduleAfter(
        long delayTicks,
        SimPhase phase,
        int priority,
        ScheduledEventData eventData)
    {
        if (delayTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayTicks),
                delayTicks,
                "Schedule delay cannot be negative.");
        }

        return ScheduleAt(CurrentTick + delayTicks, phase, priority, eventData);
    }

    public ScheduledEventId ScheduleRepeating(
        SimTick firstTick,
        long intervalTicks,
        SimPhase phase,
        int priority,
        ScheduledEventData eventData)
    {
        if (intervalTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalTicks),
                intervalTicks,
                "Repeat interval must be positive.");
        }

        return ScheduleCore(
            NormalizeDueTick(firstTick, phase),
            phase,
            priority,
            eventData,
            intervalTicks);
    }

    public bool Cancel(ScheduledEventId eventId)
    {
        if (_executingEvent?.Id == eventId)
        {
            _executingEventCancelled = true;
            return true;
        }

        if (!_activeById.TryGetValue(eventId, out ScheduledEvent? scheduledEvent))
        {
            return false;
        }

        RemoveActive(scheduledEvent);
        CompactHeapIfNeeded();
        return true;
    }

    public int CancelOwnedBy(ScheduledEventOwnerId owner)
    {
        int cancelled = 0;
        if (_executingEvent?.Data.Owner == owner && !_executingEventCancelled)
        {
            _executingEventCancelled = true;
            cancelled++;
        }

        if (!_idsByOwner.TryGetValue(owner, out SortedSet<ScheduledEventId>? ids))
        {
            return cancelled;
        }

        ScheduledEventId[] snapshot = ids.ToArray();
        foreach (ScheduledEventId id in snapshot)
        {
            if (Cancel(id))
            {
                cancelled++;
            }
        }

        return cancelled;
    }

    public SchedulerSnapshot CaptureSnapshot()
    {
        ScheduledEventSnapshot[] events = _activeById.Values
            .OrderBy(static item => item, ScheduledEventComparer.Instance)
            .Select(static item => item.ToSnapshot())
            .ToArray();

        return new SchedulerSnapshot(
            CurrentTick.Value,
            _nextEventId,
            _nextCreationSequence,
            events);
    }

    public void Restore(SchedulerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_isTickExecuting)
        {
            throw new InvalidOperationException("Cannot restore the scheduler during a simulation tick.");
        }

        if (snapshot.CurrentTick < 0)
        {
            throw new InvalidOperationException("Scheduler snapshot current tick cannot be negative.");
        }

        ScheduledEventSnapshot[] eventSnapshots = snapshot.Events
            ?? throw new InvalidOperationException("Scheduler snapshot event collection cannot be null.");

        var restored = new List<ScheduledEvent>(eventSnapshots.Length);
        var ids = new HashSet<ScheduledEventId>();
        var sequences = new HashSet<ulong>();
        ulong maximumId = 0;
        ulong maximumSequence = 0;

        foreach (ScheduledEventSnapshot item in eventSnapshots)
        {
            ScheduledEvent scheduledEvent = FromSnapshot(item, new SimTick(snapshot.CurrentTick));
            if (!ids.Add(scheduledEvent.Id))
            {
                throw new InvalidOperationException(
                    $"Scheduler snapshot contains duplicate event ID {scheduledEvent.Id}.");
            }

            if (!sequences.Add(scheduledEvent.CreationSequence))
            {
                throw new InvalidOperationException(
                    $"Scheduler snapshot contains duplicate creation sequence {scheduledEvent.CreationSequence}.");
            }

            maximumId = Math.Max(maximumId, scheduledEvent.Id.Value);
            maximumSequence = Math.Max(maximumSequence, scheduledEvent.CreationSequence);
            restored.Add(scheduledEvent);
        }

        if (snapshot.NextEventId == 0 || snapshot.NextEventId <= maximumId)
        {
            throw new InvalidOperationException("Scheduler snapshot next event ID is not greater than all pending IDs.");
        }

        if (snapshot.NextCreationSequence == 0 || snapshot.NextCreationSequence <= maximumSequence)
        {
            throw new InvalidOperationException(
                "Scheduler snapshot next creation sequence is not greater than all pending sequences.");
        }

        _heap.Clear();
        _activeById.Clear();
        _idsByOwner.Clear();
        CurrentTick = new SimTick(snapshot.CurrentTick);
        CurrentPhase = null;
        _nextEventId = snapshot.NextEventId;
        _nextCreationSequence = snapshot.NextCreationSequence;
        _executingEvent = null;
        _executingEventCancelled = false;

        foreach (ScheduledEvent scheduledEvent in restored.OrderBy(
                     static item => item,
                     ScheduledEventComparer.Instance))
        {
            AddActive(scheduledEvent);
        }
    }

    internal void BeginTick(SimTick tick)
    {
        if (_isTickExecuting)
        {
            throw new InvalidOperationException("A scheduler tick is already executing.");
        }

        SimTick expected = CurrentTick + 1;
        if (tick != expected)
        {
            throw new InvalidOperationException(
                $"Scheduler expected tick {expected} but received {tick}.");
        }

        CurrentTick = tick;
        CurrentPhase = null;
        _isTickExecuting = true;
    }

    internal void EnterPhase(SimPhase phase)
    {
        if (!_isTickExecuting)
        {
            throw new InvalidOperationException("Cannot enter a scheduler phase outside an executing tick.");
        }

        int nextIndex = SimPhaseOrder.IndexOf(phase);
        if (CurrentPhase is SimPhase current
            && nextIndex <= SimPhaseOrder.IndexOf(current))
        {
            throw new InvalidOperationException(
                $"Scheduler phase order moved from {current} to {phase}.");
        }

        CurrentPhase = phase;
    }

    internal void ExecuteDueEvents(
        SimContext context,
        SimulationTrace? trace = null)
    {
        if (!_isTickExecuting || CurrentPhase is null)
        {
            throw new InvalidOperationException("Scheduled events can execute only inside a simulation phase.");
        }

        if (context.Tick != CurrentTick || context.Phase != CurrentPhase.Value)
        {
            throw new InvalidOperationException("Scheduler context does not match the active tick and phase.");
        }

        int executed = 0;
        while (TryPeekActive(out ScheduledEvent next))
        {
            if (next.DueTick < CurrentTick)
            {
                throw new InvalidOperationException(
                    $"Scheduled event {next.Id} became past due at tick {CurrentTick}.");
            }

            if (next.DueTick > CurrentTick)
            {
                return;
            }

            int eventPhaseIndex = SimPhaseOrder.IndexOf(next.Phase);
            int currentPhaseIndex = SimPhaseOrder.IndexOf(CurrentPhase.Value);
            if (eventPhaseIndex < currentPhaseIndex)
            {
                throw new InvalidOperationException(
                    $"Scheduled event {next.Id} missed phase {next.Phase} at tick {CurrentTick}.");
            }

            if (eventPhaseIndex > currentPhaseIndex)
            {
                return;
            }

            if (++executed > _maxEventsPerPhase)
            {
                throw new InvalidOperationException(
                    $"Scheduler exceeded {_maxEventsPerPhase} events in tick {CurrentTick}, phase {CurrentPhase}.");
            }

            ScheduledEvent scheduledEvent = PopActive();
            _executingEvent = scheduledEvent;
            _executingEventCancelled = false;

            try
            {
                trace?.RecordScheduledEvent(scheduledEvent);
                _handlers.Get(scheduledEvent.Data.Type).Handle(context, scheduledEvent);

                if (scheduledEvent.RepeatIntervalTicks is long interval
                    && !_executingEventCancelled)
                {
                    ScheduledEvent repeated = scheduledEvent with
                    {
                        DueTick = scheduledEvent.DueTick + interval
                    };
                    AddActive(repeated);
                }
            }
            finally
            {
                _executingEvent = null;
                _executingEventCancelled = false;
            }
        }
    }

    internal void EndTick()
    {
        if (!_isTickExecuting)
        {
            throw new InvalidOperationException("No scheduler tick is executing.");
        }

        if (TryPeekActive(out ScheduledEvent next) && next.DueTick <= CurrentTick)
        {
            throw new InvalidOperationException(
                $"Scheduled event {next.Id} remained due after tick {CurrentTick}.");
        }

        _isTickExecuting = false;
        CurrentPhase = null;
        CompactHeapIfNeeded();
    }

    private ScheduledEventId ScheduleCore(
        SimTick dueTick,
        SimPhase phase,
        int priority,
        ScheduledEventData eventData,
        long? repeatIntervalTicks)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        _ = SimPhaseOrder.IndexOf(phase);

        ScheduledEventId id = new(_nextEventId);
        ulong sequence = _nextCreationSequence;
        _nextEventId = checked(_nextEventId + 1);
        _nextCreationSequence = checked(_nextCreationSequence + 1);

        var scheduledEvent = new ScheduledEvent(
            id,
            dueTick,
            phase,
            priority,
            sequence,
            eventData,
            repeatIntervalTicks);
        AddActive(scheduledEvent);
        return id;
    }

    private SimTick NormalizeDueTick(SimTick requestedTick, SimPhase phase)
    {
        _ = SimPhaseOrder.IndexOf(phase);
        if (requestedTick.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedTick),
                requestedTick,
                "Scheduled-event tick cannot be negative.");
        }

        if (requestedTick < CurrentTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedTick),
                requestedTick,
                $"Cannot schedule an event before current tick {CurrentTick}.");
        }

        if (requestedTick > CurrentTick)
        {
            return requestedTick;
        }

        if (!_isTickExecuting || CurrentPhase is null)
        {
            return CurrentTick + 1;
        }

        int requestedPhaseIndex = SimPhaseOrder.IndexOf(phase);
        int currentPhaseIndex = SimPhaseOrder.IndexOf(CurrentPhase.Value);
        return requestedPhaseIndex > currentPhaseIndex
            ? requestedTick
            : CurrentTick + 1;
    }

    private void AddActive(ScheduledEvent scheduledEvent)
    {
        if (!_activeById.TryAdd(scheduledEvent.Id, scheduledEvent))
        {
            throw new InvalidOperationException($"Duplicate scheduled-event ID {scheduledEvent.Id}.");
        }

        if (scheduledEvent.Data.Owner is ScheduledEventOwnerId owner)
        {
            if (!_idsByOwner.TryGetValue(owner, out SortedSet<ScheduledEventId>? ids))
            {
                ids = new SortedSet<ScheduledEventId>();
                _idsByOwner.Add(owner, ids);
            }

            ids.Add(scheduledEvent.Id);
        }

        _heap.Push(scheduledEvent);
    }

    private void RemoveActive(ScheduledEvent scheduledEvent)
    {
        _activeById.Remove(scheduledEvent.Id);
        if (scheduledEvent.Data.Owner is not ScheduledEventOwnerId owner
            || !_idsByOwner.TryGetValue(owner, out SortedSet<ScheduledEventId>? ids))
        {
            return;
        }

        ids.Remove(scheduledEvent.Id);
        if (ids.Count == 0)
        {
            _idsByOwner.Remove(owner);
        }
    }

    private ScheduledEvent PopActive()
    {
        if (!TryPeekActive(out ScheduledEvent active))
        {
            throw new InvalidOperationException("No active scheduled event is available.");
        }

        ScheduledEvent popped = _heap.Pop();
        if (!Equals(popped, active))
        {
            throw new InvalidOperationException("Scheduled-event heap active entry changed unexpectedly.");
        }

        RemoveActive(popped);
        return popped;
    }

    private bool TryPeekActive(out ScheduledEvent scheduledEvent)
    {
        while (_heap.TryPeek(out ScheduledEvent candidate))
        {
            if (_activeById.TryGetValue(candidate.Id, out ScheduledEvent? active)
                && Equals(candidate, active))
            {
                scheduledEvent = candidate;
                return true;
            }

            _heap.Pop();
        }

        scheduledEvent = null!;
        return false;
    }

    private void CompactHeapIfNeeded()
    {
        if (_heap.Count <= checked(_activeById.Count * 2 + 64))
        {
            return;
        }

        _heap.Clear();
        foreach (ScheduledEvent scheduledEvent in _activeById.Values.OrderBy(
                     static item => item,
                     ScheduledEventComparer.Instance))
        {
            _heap.Push(scheduledEvent);
        }
    }

    private static ScheduledEvent FromSnapshot(
        ScheduledEventSnapshot snapshot,
        SimTick currentTick)
    {
        if (snapshot.Id == 0)
        {
            throw new InvalidOperationException("Scheduled-event ID cannot be zero.");
        }

        if (snapshot.CreationSequence == 0)
        {
            throw new InvalidOperationException("Scheduled-event creation sequence cannot be zero.");
        }

        var dueTick = new SimTick(snapshot.DueTick);
        if (dueTick <= currentTick)
        {
            throw new InvalidOperationException(
                $"Pending scheduled event {snapshot.Id} is not after snapshot tick {currentTick}.");
        }

        var phase = (SimPhase)snapshot.Phase;
        _ = SimPhaseOrder.IndexOf(phase);

        if (snapshot.RepeatIntervalTicks is long interval && interval <= 0)
        {
            throw new InvalidOperationException("Scheduled-event repeat interval must be positive.");
        }

        var data = new ScheduledEventData(
            new ScheduledEventTypeId(snapshot.Type),
            snapshot.Owner is ulong owner ? new ScheduledEventOwnerId(owner) : null,
            snapshot.Payload ?? throw new InvalidOperationException("Scheduled-event payload cannot be null."));

        return new ScheduledEvent(
            new ScheduledEventId(snapshot.Id),
            dueTick,
            phase,
            snapshot.Priority,
            snapshot.CreationSequence,
            data,
            snapshot.RepeatIntervalTicks);
    }
}
