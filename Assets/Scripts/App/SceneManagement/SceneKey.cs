using System;

namespace M4U.App.SceneManagement;

public enum SceneType
{
    Builtin = 0,
    Addressables = 1,
}

public readonly record struct SceneKey
{
    public string Name { get; }
    public SceneType Type { get; }

    private SceneKey(string name, SceneType type)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (type == SceneType.Addressables && !name.StartsWith("Assets/Scenes/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Addressable scene name must start with 'Assets/Scenes/'", nameof(name));
        }

        Name = name;
        Type = type;
    }

    public static SceneKey Builtin(string name) => new(name, SceneType.Builtin);
    public static SceneKey Addressables(string address) => new(address, SceneType.Addressables);

    public bool IsBuiltin => Type is SceneType.Builtin;
    public bool IsAddressables => Type is SceneType.Addressables;

    public override string ToString() => $"{Type}:{Name}";
}
