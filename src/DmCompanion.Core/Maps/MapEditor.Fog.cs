namespace DmCompanion.Core.Maps;

public sealed partial class MapEditor
{
    /// <summary>Añade una zona de niebla tapada del tipo dado (la total se guarda sin tipo). Null si el polígono no es válido. Deshacible.</summary>
    public MapFogZone? AddFogZone(IReadOnlyList<double> points, FogType type = FogType.Total)
    {
        if (!FogGeometry.IsValid(points)) return null;
        var zone = new MapFogZone(newId(), [.. points], Type: type == FogType.Total ? null : type);
        SetFog([.. State.FogZones, zone]);
        PushFog(new Op(Kind.FogAdded, Zone: zone, Index: State.FogZones.Count - 1));
        return zone;
    }

    /// <summary>Tapa o destapa una zona. Deshacible. False si no existe o no cambia.</summary>
    public bool SetFogRevealed(string id, bool revealed)
    {
        var zone = State.FogZones.FirstOrDefault(z => z.Id == id);
        if (zone is null || zone.IsRevealed == revealed) return false;
        ReplaceFog(id, revealed);
        PushFog(new Op(Kind.FogRevealed, Zone: zone, RevealedAfter: revealed));
        return true;
    }

    /// <summary>Cambia el tipo de una zona. Deshacible. False si no existe o ya tiene ese tipo.</summary>
    public bool SetFogType(string id, FogType type)
    {
        var zone = State.FogZones.FirstOrDefault(z => z.Id == id);
        if (zone is null || zone.EffectiveType == type) return false;
        ReplaceFogType(id, type);
        PushFog(new Op(Kind.FogTyped, Zone: zone, TypeAfter: type));
        return true;
    }

    /// <summary>Borra una zona. Deshacible: al deshacer vuelve a su índice.</summary>
    public bool EraseFogZone(string id)
    {
        var index = State.FogZones.ToList().FindIndex(z => z.Id == id);
        if (index < 0) return false;
        var zone = State.FogZones[index];
        SetFog([.. State.FogZones.Where(z => z.Id != id)]);
        PushFog(new Op(Kind.FogErased, Zone: zone, Index: index));
        return true;
    }

    private void ApplyFog(Op op, bool reverse)
    {
        var zone = op.Zone!;
        if (op.Kind == Kind.FogRevealed)
        {
            ReplaceFog(zone.Id, op.RevealedAfter == true != reverse);
            return;
        }
        if (op.Kind == Kind.FogTyped)
        {
            ReplaceFogType(zone.Id, reverse ? zone.EffectiveType : op.TypeAfter!.Value);
            return;
        }
        var zones = State.FogZones.ToList();
        if ((op.Kind == Kind.FogAdded) != reverse) zones.Insert(Math.Min(op.Index, zones.Count), zone);
        else zones.RemoveAll(z => z.Id == zone.Id);
        SetFog(zones);
    }

    private void ReplaceFog(string id, bool revealed) =>
        SetFog([.. State.FogZones.Select(z => z.Id == id ? z with { Revealed = revealed ? true : null } : z)]);

    private void ReplaceFogType(string id, FogType type) =>
        SetFog([.. State.FogZones.Select(z => z.Id == id ? z with { Type = type == FogType.Total ? null : type } : z)]);

    /// <summary>Con la lista vacía, Fog queda en null para que no se serialice.</summary>
    private void SetFog(List<MapFogZone> zones) => State = State with { Fog = zones.Count == 0 ? null : zones };

    private void PushFog(Op op)
    {
        _undo.Push(op);
        _redo.Clear();
    }
}
