using SafeRideKids.Familia.Maui.Models;

namespace SafeRideKids.Familia.Maui.Services;

/// <summary>
/// Cache em memória da lista de crianças do responsável logado.
/// POC: não persiste em disco para evitar PII em storage local.
/// Em produção, esta lista virá de um GET /api/v1/family/me/children
/// que o backend ainda não expõe.
/// </summary>
public interface IChildrenStore
{
    IReadOnlyList<ChildSummary> Snapshot { get; }
    event EventHandler? Changed;
    void Upsert(ChildSummary child);
    void Remove(string childId);
    void Clear();
    ChildSummary? Find(string childId);
}

public sealed class ChildrenStore : IChildrenStore
{
    private readonly List<ChildSummary> _items = new();
    private readonly object _gate = new();

    public IReadOnlyList<ChildSummary> Snapshot
    {
        get
        {
            lock (_gate) { return _items.ToArray(); }
        }
    }

    public event EventHandler? Changed;

    public void Upsert(ChildSummary child)
    {
        lock (_gate)
        {
            var ix = _items.FindIndex(c => c.ChildId == child.ChildId);
            if (ix >= 0) _items[ix] = child; else _items.Add(child);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string childId)
    {
        lock (_gate)
        {
            _items.RemoveAll(c => c.ChildId == childId);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_gate) { _items.Clear(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ChildSummary? Find(string childId)
    {
        lock (_gate)
        {
            return _items.FirstOrDefault(c => c.ChildId == childId);
        }
    }
}
