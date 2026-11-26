namespace DmCompanion.UI.Interop;

public sealed record WindowFrameOptions(bool Movable, ResizeEdges Edges, WindowSize MinSize,
    WindowSize? Size = null, double? X = null, double? Y = null, WindowAnchor? Anchor = null);
