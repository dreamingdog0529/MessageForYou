using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace M4U.App.SceneManagement;

/// <summary>
/// シーンのエントリポイント。VContainer のエントリポイント API ではなく <see cref="ISceneNavigator"/> から直接呼び出される。
/// </summary>
public abstract class SceneEntrypoint
{
    /// <summary>シーンから離脱する直前に呼ばれる</summary>
    public virtual UniTask ExitAsync(SceneExitContext context, CancellationToken cancellation) => UniTask.CompletedTask;

    internal abstract UniTask InvokeEnterAsync(SceneEnterContext context, CancellationToken cancellation);
    internal abstract object InvokeCaptureState();
}

public abstract class SceneEntrypoint<TArgs, TState> : SceneEntrypoint
    where TState : struct
{
    /// <summary>シーンのロード後に呼ばれる。Back で戻ってきた場合は <c>context.RestoredState</c> から再構築する</summary>
    public abstract UniTask EnterAsync(SceneEnterContext<TArgs, TState> context, CancellationToken cancellation);

    /// <summary>Push で別シーンへ遷移する直前に呼ばれ、戻ってきた際の再構築に使う状態を返す</summary>
    public abstract TState CaptureState();

    internal sealed override UniTask InvokeEnterAsync(SceneEnterContext context, CancellationToken cancellation)
    {
        if (context is not SceneEnterContext<TArgs, TState> typed)
        {
            throw new InvalidOperationException(
                $"Enter context type mismatch for {context.Scene}: expected {typeof(SceneEnterContext<TArgs, TState>)}, actual {context.GetType()}");
        }

        return EnterAsync(typed, cancellation);
    }

    internal sealed override object InvokeCaptureState() => CaptureState();
}
