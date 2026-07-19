using System;
using System.Collections.Generic;

namespace GenerationArk.Simulation.Scheduling;

internal sealed class ScheduledEventHeap
{
    private readonly List<ScheduledEvent> _items = new();

    public int Count => _items.Count;

    public void Clear() => _items.Clear();

    public void Push(ScheduledEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        SiftUp(_items.Count - 1);
    }

    public bool TryPeek(out ScheduledEvent item)
    {
        if (_items.Count == 0)
        {
            item = null!;
            return false;
        }

        item = _items[0];
        return true;
    }

    public ScheduledEvent Pop()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot pop an empty scheduled-event heap.");
        }

        ScheduledEvent result = _items[0];
        int lastIndex = _items.Count - 1;
        ScheduledEvent last = _items[lastIndex];
        _items.RemoveAt(lastIndex);

        if (_items.Count > 0)
        {
            _items[0] = last;
            SiftDown(0);
        }

        return result;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (ScheduledEventComparer.Instance.Compare(_items[index], _items[parent]) >= 0)
            {
                return;
            }

            ScheduledEvent temporary = _items[parent];
            _items[parent] = _items[index];
            _items[index] = temporary;
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            int left = checked(index * 2 + 1);
            if (left >= _items.Count)
            {
                return;
            }

            int right = left + 1;
            int smallest = left;
            if (right < _items.Count
                && ScheduledEventComparer.Instance.Compare(_items[right], _items[left]) < 0)
            {
                smallest = right;
            }

            if (ScheduledEventComparer.Instance.Compare(_items[smallest], _items[index]) >= 0)
            {
                return;
            }

            ScheduledEvent temporary = _items[index];
            _items[index] = _items[smallest];
            _items[smallest] = temporary;
            index = smallest;
        }
    }
}
