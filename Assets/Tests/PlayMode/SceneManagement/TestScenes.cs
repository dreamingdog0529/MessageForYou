using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.App.SceneManagement;
using VContainer;

namespace M4U.Tests.SceneManagement;

/// <summary>ルートスコープに登録し、テスト用エントリポイントの呼び出しを記録する</summary>
public sealed class SceneCallRecorder
{
    public List<SceneEnterContext> Enters { get; } = new();
    public List<SceneExitContext> Exits { get; } = new();
    public List<SceneEnterContext> InjectedContexts { get; } = new();

    public int NextStateValue { get; set; }
    public UniTaskCompletionSource? EnterGate { get; set; }
}

public struct CounterState
{
    public int Value;
}

public static class TestScenes
{
    public static readonly SceneDefinition<int, CounterState> A = new(SceneKey.Builtin("TestSceneA"));
    public static readonly SceneDefinition<string, EmptySceneState> B = new(SceneKey.Builtin("TestSceneB"));

    /// <summary>A のシーンを指すが Args の型がエントリポイントと一致しない</summary>
    public static readonly SceneDefinition<string, CounterState> MismatchedA = new(SceneKey.Builtin("TestSceneA"));

    /// <summary>SceneLifetimeScope を持たないシーン</summary>
    public static readonly SceneDefinition<int, CounterState> WithoutScope = new(SceneKey.Builtin("TestSceneEmpty"));
}

public abstract class RecordingEntrypoint<TArgs, TState> : SceneEntrypoint<TArgs, TState>
    where TState : struct
{
    protected SceneCallRecorder Recorder { get; }

    protected RecordingEntrypoint(SceneCallRecorder recorder, SceneEnterContext<TArgs, TState> injected)
    {
        Recorder = recorder;
        recorder.InjectedContexts.Add(injected);
    }

    public override async UniTask EnterAsync(SceneEnterContext<TArgs, TState> context, CancellationToken cancellation)
    {
        Recorder.Enters.Add(context);
        if (Recorder.EnterGate is { } gate) await gate.Task;
    }

    public override UniTask ExitAsync(SceneExitContext context, CancellationToken cancellation)
    {
        Recorder.Exits.Add(context);
        return UniTask.CompletedTask;
    }
}

public sealed class SceneAEntrypoint : RecordingEntrypoint<int, CounterState>
{
    [Inject]
    public SceneAEntrypoint(SceneCallRecorder recorder, SceneEnterContext<int, CounterState> injected)
        : base(recorder, injected)
    {
    }

    public override CounterState CaptureState() => new() { Value = Recorder.NextStateValue };
}

public sealed class SceneBEntrypoint : RecordingEntrypoint<string, EmptySceneState>
{
    [Inject]
    public SceneBEntrypoint(SceneCallRecorder recorder, SceneEnterContext<string, EmptySceneState> injected)
        : base(recorder, injected)
    {
    }

    public override EmptySceneState CaptureState() => default;
}

public sealed class SceneALifetimeScope : SceneLifetimeScope<SceneAEntrypoint>
{
}

public sealed class SceneBLifetimeScope : SceneLifetimeScope<SceneBEntrypoint>
{
}
