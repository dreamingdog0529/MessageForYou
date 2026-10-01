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

    /// <summary>遷移元を持たない起動用のコンテキスト。Args は default になる</summary>
    internal abstract SceneEnterContext CreateDefaultEnterContext();

    /// <summary>エントリポイントの型が Args と State の型に一致するか。インスタンスを生成せずに整合性を確認するために使う</summary>
    internal abstract bool Accepts(Type entrypointType);

    /// <summary>エントリポイントの型から Args と State の型を取り出して定義を作る</summary>
    internal static SceneDefinition ForEntrypoint(Type entrypointType, SceneKey key)
    {
        for (var type = entrypointType; type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SceneEntrypoint<,>))
            {
                var definitionType = typeof(SceneDefinition<,>).MakeGenericType(type.GetGenericArguments());
                return (SceneDefinition)Activator.CreateInstance(definitionType, key);
            }
        }

        throw new ArgumentException($"{entrypointType} does not derive from {typeof(SceneEntrypoint<,>)}", nameof(entrypointType));
    }

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

    internal override SceneEnterContext CreateDefaultEnterContext()
        => new SceneEnterContext<TArgs, TState>(Key, SceneTransitionMode.Reset, null, default!, null);

    internal override bool Accepts(Type entrypointType)
        => typeof(SceneEntrypoint<TArgs, TState>).IsAssignableFrom(entrypointType);
}
