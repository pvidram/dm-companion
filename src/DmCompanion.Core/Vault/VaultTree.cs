namespace DmCompanion.Core.Vault;

public sealed record VaultTreeNode(string Name, string Path, VaultEntity? Entity, IReadOnlyList<VaultTreeNode> Children)
{
    public bool IsFolder => Entity is null;
}

public static class VaultTree
{
    public static VaultTreeNode Build(IEnumerable<VaultEntity> entities)
    {
        var root = new FolderBuilder(string.Empty, string.Empty);
        foreach (var entity in entities)
        {
            var segments = entity.RelativePath.Split('/');
            var folder = root;
            for (var i = 0; i < segments.Length - 1; i++) folder = folder.GetOrAddFolder(segments[i]);
            folder.Files.Add(entity);
        }
        return root.ToNode();
    }

    private sealed class FolderBuilder(string name, string path)
    {
        private readonly Dictionary<string, FolderBuilder> _folders = new(StringComparer.OrdinalIgnoreCase);

        public string Name => name;

        public List<VaultEntity> Files { get; } = [];

        public FolderBuilder GetOrAddFolder(string segment)
        {
            if (!_folders.TryGetValue(segment, out var folder))
                _folders[segment] = folder = new FolderBuilder(segment, path.Length == 0 ? segment : $"{path}/{segment}");
            return folder;
        }

        public VaultTreeNode ToNode()
        {
            var folders = _folders.Values
                .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(folder => folder.ToNode());
            var files = Files
                .OrderBy(file => file.FileTitle, StringComparer.CurrentCultureIgnoreCase)
                .Select(file => new VaultTreeNode(file.FileTitle, file.RelativePath, file, []));
            return new VaultTreeNode(name, path, null, [.. folders, .. files]);
        }
    }
}
