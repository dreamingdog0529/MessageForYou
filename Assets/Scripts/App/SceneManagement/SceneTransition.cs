namespace M4U.App.SceneManagement;

public enum SceneTransitionMode
{
    /// <summary>現シーンを履歴に積んで遷移する</summary>
    Push = 0,
    /// <summary>履歴に積まずに現シーンを置き換える</summary>
    Replace = 1,
    /// <summary>履歴をクリアして遷移する</summary>
    Reset = 2,
    /// <summary>履歴から前のシーンに戻る</summary>
    Back = 3,
}

/// <summary>
/// シーンに入る際の遷移情報。シーンの子スコープに登録されるため、シーン内の任意のサービスからインジェクトできる。
/// </summary>
public abstract class SceneEnterContext
{
    public SceneKey Scene { get; }
    public SceneTransitionMode Mode { get; }
    public SceneKey? From { get; }

    public bool IsRestored => Mode is SceneTransitionMode.Back;

    private protected SceneEnterContext(SceneKey scene, SceneTransitionMode mode, SceneKey? from)
    {
        Scene = scene;
        Mode = mode;
        From = from;
    }
}

public sealed class SceneEnterContext<TArgs, TState> : SceneEnterContext
    where TState : struct
{
    public TArgs Args { get; }

    /// <summary>Back で戻ってきた場合のみ、離脱時に <c>CaptureState</c> で保存した状態が入る</summary>
    public TState? RestoredState { get; }

    internal SceneEnterContext(
        SceneKey scene,
        SceneTransitionMode mode,
        SceneKey? from,
        TArgs args,
        TState? restoredState)
        : base(scene, mode, from)
    {
        Args = args;
        RestoredState = restoredState;
    }
}

/// <summary>シーンから離脱する際の遷移情報</summary>
public sealed class SceneExitContext
{
    public SceneKey Scene { get; }
    public SceneTransitionMode Mode { get; }
    public SceneKey To { get; }

    internal SceneExitContext(SceneKey scene, SceneTransitionMode mode, SceneKey to)
    {
        Scene = scene;
        Mode = mode;
        To = to;
    }
}
