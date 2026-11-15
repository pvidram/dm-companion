using DmCompanion.Core.Combat;

namespace DmCompanion.Core.Maps;

/// <summary>
/// Operaciones sobre un mapa. El estado es inmutable: cada operación lo sustituye.
/// Solo los trazos y ocultar/revelar tokens entran en la pila de deshacer. Con width o height &lt;= 0 (tamaño de imagen aún
/// desconocido) no se recortan las coordenadas.
/// </summary>
public sealed class MapEditor(MapState initial, double width, double height, Func<string> newId)
{
    private enum Kind { Added, Erased, Hidden }

    private sealed record Op(Kind Kind, MapStroke? Stroke = null, int Index = 0, string? TokenId = null, bool? HiddenAfter = null);

    private readonly Stack<Op> _undo = new();
    private readonly Stack<Op> _redo = new();

    public MapState State { get; private set; } = initial;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public MapToken AddToken(TokenDraft draft, double x, double y)
    {
        ValidateSize(draft.Size);
        if (string.IsNullOrWhiteSpace(draft.Name))
            throw new ArgumentException("El nombre del token no puede estar en blanco.", nameof(draft));
        var token = new MapToken(newId(), draft.Name, draft.Source, draft.Side, draft.Size,
            ClampX(x), ClampY(y), draft.Ac, draft.Hp);
        State = State with { Tokens = [.. State.Tokens, token] };
        return token;
    }

    public bool MoveToken(string id, double x, double y) =>
        Replace(id, t => t with { X = ClampX(x), Y = ClampY(y) });

    public bool UpdateToken(string id, TokenEdit edit)
    {
        ValidateSize(edit.Size);
        var token = State.Tokens.FirstOrDefault(t => t.Id == id);
        if (token is null) return false;
        var alias = string.IsNullOrWhiteSpace(edit.Alias) ? null : edit.Alias.Trim();
        if (token.Source is not null)
            return Replace(id, t => t with { Side = edit.Side, Size = edit.Size, Alias = alias });
        if (string.IsNullOrWhiteSpace(edit.Name))
            throw new ArgumentException("El nombre del token no puede estar en blanco.", nameof(edit));
        return Replace(id, t => t with { Side = edit.Side, Size = edit.Size, Name = edit.Name, Ac = edit.Ac, Hp = edit.Hp, Alias = alias });
    }

    /// <summary>Oculta o revela un token a los jugadores (Hidden=false se guarda como null). Deshacible. False si no existe o no cambia.</summary>
    public bool SetHidden(string id, bool hidden)
    {
        var token = State.Tokens.FirstOrDefault(t => t.Id == id);
        if (token is null || token.IsHidden == hidden) return false;
        Replace(id, t => t with { Hidden = hidden ? true : null });
        _undo.Push(new Op(Kind.Hidden, TokenId: id, HiddenAfter: hidden));
        _redo.Clear();
        return true;
    }

    /// <summary>Guarda los HP actuales recortados a [0, max]; max lo pone quien llama (sale de la nota o del genérico). Al subir de 0 se ponen a cero las salvaciones de muerte. Sin deshacer.</summary>
    public bool SetHp(string id, int value, int max)
    {
        if (State.Tokens.All(t => t.Id != id)) return false;
        State = HitPointOps.SetHp(State, id, value, max);
        return true;
    }

    public bool RemoveToken(string id)
    {
        if (State.Tokens.All(t => t.Id != id)) return false;
        var cleaned = TurnOps.RemoveEntriesOf(State, id);
        State = cleaned with { Tokens = [.. cleaned.Tokens.Where(t => t.Id != id)] };
        return true;
    }

    /// <summary>Aplica una operación de combate (fuera de la pila de deshacer). Devuelve true si el estado cambió.</summary>
    public bool ReplaceState(Func<MapState, MapState> change)
    {
        var next = change(State);
        if (ReferenceEquals(next, State)) return false;
        State = next;
        return true;
    }

    public MapStroke? AddStroke(string color, double strokeWidth, IReadOnlyList<double> points)
    {
        // Tríos x, y, presión: menos de dos puntos o una lista que no es múltiplo de 3 no es un trazo.
        if (points.Count < 6 || points.Count % 3 != 0) return null;
        var stroke = new MapStroke(newId(), color, strokeWidth, points);
        State = State with { Strokes = [.. State.Strokes, stroke] };
        _undo.Push(new Op(Kind.Added, stroke, State.Strokes.Count - 1));
        _redo.Clear();
        return stroke;
    }

    public bool EraseStroke(string id)
    {
        var index = State.Strokes.ToList().FindIndex(s => s.Id == id);
        if (index < 0) return false;
        var stroke = State.Strokes[index];
        State = State with { Strokes = [.. State.Strokes.Where(s => s.Id != id)] };
        _undo.Push(new Op(Kind.Erased, stroke, index));
        _redo.Clear();
        return true;
    }

    public void SetGrid(GridSettings grid)
    {
        if (!MapStateSerializer.IsValidGrid(grid))
            throw new ArgumentOutOfRangeException(nameof(grid), "La cuadrícula necesita un tamaño de celda finito mayor que 0 y desplazamientos finitos.");
        State = State with { Grid = grid };
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var op)) return false;
        Apply(op, reverse: true);
        _redo.Push(op);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var op)) return false;
        Apply(op, reverse: false);
        _undo.Push(op);
        return true;
    }

    private void Apply(Op op, bool reverse)
    {
        if (op.Kind == Kind.Hidden)
        {
            var hidden = op.HiddenAfter == true != reverse;
            Replace(op.TokenId!, t => t with { Hidden = hidden ? true : null });
            return;
        }
        var stroke = op.Stroke!;
        var add = (op.Kind == Kind.Added) != reverse;
        var strokes = State.Strokes.ToList();
        if (add) strokes.Insert(Math.Min(op.Index, strokes.Count), stroke);
        else strokes.RemoveAll(s => s.Id == stroke.Id);
        State = State with { Strokes = strokes };
    }

    private bool Replace(string id, Func<MapToken, MapToken> change)
    {
        if (State.Tokens.All(t => t.Id != id)) return false;
        State = State with { Tokens = [.. State.Tokens.Select(t => t.Id == id ? change(t) : t)] };
        return true;
    }

    private static void ValidateSize(int size)
    {
        if (size is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(size), size, "El tamaño debe ser 1, 2 o 3.");
    }

    private double ClampX(double x) => width > 0 ? Math.Clamp(x, 0, width) : x;
    private double ClampY(double y) => height > 0 ? Math.Clamp(y, 0, height) : y;
}
