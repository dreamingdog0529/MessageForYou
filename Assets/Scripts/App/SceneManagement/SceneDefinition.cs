using System;

namespace M4U.App.SceneManagement;

/// <summary>戻った際に復元する状態を持たないシーン用</summary>
public readonly struct EmptySceneState
{
}

public abstract class SceneDefinition
{
    public SceneKey Key { get; }
    public abstract Type ArgsType { get; }
    public abstract Type StateType { get; }

    private protected SceneDefinition(SceneKey key)
    {
        Key = key;
    }

    internal abstract SceneEnterContext CreateEnterContext(
        SceneTransitionMode mode,
        SceneKey? from,
        object? args,
        object? restoredState);

    /// <summary>エントリポイントの型が Args と State の型に一致するか。インスタンスを生成せずに整合性を確認するために使う</summary>
    internal abstract bool Accepts(Type entrypointType);

    public override string ToString() => Key.ToString();
}

public sealed class SceneDefinition<TArgs, TState> : SceneDefinition
    where TState : struct
{
    public override Type ArgsType => typeof(TArgs);
    public override Type StateType => typeof(TState);

    public SceneDefinition(SceneKey key) : base(key)
    {
    }

    internal override SceneEnterContext CreateEnterContext(
        SceneTransitionMode mode,
        SceneKey? from,
        object? args,
        object? restoredState)
        => new SceneEnterContext<TArgs, TState>(Key, mode, from, (TArgs)args!, (TState?)restoredState);

    internal override bool Accepts(Type entrypointType)
        => typeof(SceneEntrypoint<TArgs, TState>).IsAssignableFrom(entrypointType);
}
