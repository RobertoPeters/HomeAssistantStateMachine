namespace Hasm.Services;

public class UndoRedo<T> where T : IEquatable<T>
{
    private readonly List<T> _undoStack = [];
    private readonly List<T> _redoStack = [];
    private readonly int _maxStackSize = 50;

    public UndoRedo(int maxStackSize)
    {
        _maxStackSize = maxStackSize;
    }

    public UndoRedo()
    {
    }

    public void Add(T item)
    {
        if (_undoStack.Any() && PeekUndo().Equals(item))
        {
            return;
        }

        PushUndo(item);
        while (_undoStack.Count > _maxStackSize)
        {
            _undoStack.RemoveAt(0);
        }
        _redoStack.Clear();
    }

    public bool CanUndo() => _undoStack.Any();
    public bool CanRedo() => _redoStack.Any();

    public T? Undo()
    {
        if (!CanUndo())
        {
            return default;
        }

        var item = PopUndo();
        PushRedo(item);
        return item;
    }

    public T? Redo()
    {
        if (!CanRedo())
        {
            return default;
        }

        var item = PopRedo();
        PushUndo(item);
        return item;
    }

    private T PeekUndo() => _undoStack[_undoStack.Count -1];
    private T PopUndo()
    {
        var result = _undoStack[_undoStack.Count - 1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        return result;
    }
    private T PopRedo()
    {
        var result = _redoStack[_redoStack.Count - 1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        return result;
    }

    private void PushUndo(T item) => _undoStack.Add(item);
    private void PushRedo(T item) => _redoStack.Add(item);
}
