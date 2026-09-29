using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace M4U.App.SceneManagement;

public interface ISceneNavigator
{
    SceneKey? Current { get; }
    bool CanGoBack { get; }
    bool IsTransitioning { get; }

    /// <summary>現シーンの状態を履歴に積んで遷移する</summary>
    UniTask PushAsync<TArgs, TState>(SceneDefinition<TArgs, TState> definition, TArgs args, CancellationToken cancellation = default)
        where TState : struct;

    /// <summary>履歴に積まずに現シーンを置き換える</summary>
    UniTask ReplaceAsync<TArgs, TState>(SceneDefinition<TArgs, TState> definition, TArgs args, CancellationToken cancellation = default)
        where TState : struct;

    /// <summary>履歴をクリアして遷移する</summary>
    UniTask ResetAsync<TArgs, TState>(SceneDefinition<TArgs, TState> definition, TArgs args, CancellationToken cancellation = default)
        where TState : struct;

    /// <summary>履歴の直前のシーンに戻る 履歴が空なら false を返す</summary>
    UniTask<bool> BackAsync(CancellationToken cancellation = default);
}

/// <remarks>
/// cancellation は現シーンの離脱処理が終わるまで有効。アンロード開始以降は遷移を中断せず最後まで完了させる。
/// </remarks>
public sealed class SceneNavigator : ISceneNavigator, IDisposable
{
    private sealed record HistoryEntry(SceneDefinition Definition, object? Args, object State);

    private sealed record ActiveScene(
        SceneDefinition Definition,
        object? Args,
        ISceneLoader Loader,
        SceneEntrypoint Entrypoint);

    private readonly LifetimeScope _rootScope;
    private readonly ISceneLoaderFactory _loaderFactory;
    private readonly Stack<HistoryEntry> _history = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly CancellationToken _disposeToken;
    private ActiveScene? _current;

    public SceneKey? Current => _current?.Definition.Key;
    public bool CanGoBack => _history.Count > 0;
    public bool IsTransitioning { get; private set; }

    public SceneNavigator(LifetimeScope rootScope, ISceneLoaderFactory loaderFactory)
    {
        Throw.IfNull(rootScope);
        Throw.IfNull(loaderFactory);

        _rootScope = rootScope;
        _loaderFactory = loaderFactory;
        _disposeToken = _disposeCts.Token;
    }

    public UniTask PushAsync<TArgs, TState>(SceneDefinition<TArgs, TState> definition, TArgs args, CancellationToken cancellation = default)
        where TState : struct
        => TransitionAsync(definition, args, null, SceneTransitionMode.Push, cancellation);

    public UniTask ReplaceAsync<TArgs, TState>(SceneDefinition<TArgs, TState> definition, TArgs args, CancellationToken cancellation = default)
        where TState : struct
        => TransitionAsync(definition, args, null, SceneTransitionMode.Replace, cancellation);

    public UniTask ResetAsync<TArgs, TState>(SceneDefinition<TArgs, TState> definition, TArgs args, CancellationToken cancellation = default)
        where TState : struct
        => TransitionAsync(definition, args, null, SceneTransitionMode.Reset, cancellation);

    public async UniTask<bool> BackAsync(CancellationToken cancellation = default)
    {
        ThrowIfCannotTransition();
        if (_history.Count == 0) return false;

        var entry = _history.Peek();
        await TransitionAsync(entry.Definition, entry.Args, entry.State, SceneTransitionMode.Back, cancellation);
        return true;
    }

    private async UniTask TransitionAsync(
        SceneDefinition definition,
        object? args,
        object? restoredState,
        SceneTransitionMode mode,
        CancellationToken cancellation)
    {
        Throw.IfNull(definition);
        ThrowIfCannotTransition();

        IsTransitioning = true;
        try
        {
            var from = await ExitCurrentAsync(mode, definition.Key, cancellation);
            if (mode is SceneTransitionMode.Reset) _history.Clear();
            if (mode is SceneTransitionMode.Back) _history.Pop();

            var context = definition.CreateEnterContext(mode, from, args, restoredState);
            await LoadAndEnterAsync(definition, args, context);
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    private void ThrowIfCannotTransition()
    {
        if (_disposeCts.IsCancellationRequested) throw new ObjectDisposedException(nameof(SceneNavigator));
        if (IsTransitioning) throw new InvalidOperationException("Scene transition is already in progress");
    }

    private async UniTask<SceneKey?> ExitCurrentAsync(
        SceneTransitionMode mode,
        SceneKey to,
        CancellationToken cancellation)
    {
        if (_current is not { } current) return null;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeToken);
        linked.Token.ThrowIfCancellationRequested();

        var state = mode is SceneTransitionMode.Push ? current.Entrypoint.InvokeCaptureState() : null;

        // ExitAsync を完了したシーンは離脱前の状態に戻せないため、完了後はキャンセルを反映しない
        await current.Entrypoint.ExitAsync(new SceneExitContext(current.Definition.Key, mode, to), linked.Token);

        if (state != null) _history.Push(new HistoryEntry(current.Definition, current.Args, state));

        _current = null;
        await current.Loader.UnloadAsync();

        return current.Definition.Key;
    }

    private async UniTask LoadAndEnterAsync(SceneDefinition definition, object? args, SceneEnterContext context)
    {
        var cancellation = _disposeToken;
        var loader = _loaderFactory.Create(definition.Key);

        SceneEntrypoint entrypoint;
        try
        {
            Scene scene;
            using (SceneScopeBinding.Enqueue(_rootScope, context))
            {
                scene = await loader.LoadAsync(cancellation);
            }

            SceneManager.SetActiveScene(scene);

            var scope = FindSceneScope(scene, definition);

            // 型が合わないとエントリポイントへのコンテキスト注入で失敗するため、Resolve する前に確認する
            if (!definition.Accepts(scope.EntrypointType))
            {
                throw new InvalidOperationException(
                    $"Entrypoint {scope.EntrypointType} does not match scene definition {definition} " +
                    $"(Args: {definition.ArgsType}, State: {definition.StateType})");
            }

            entrypoint = scope.ResolveEntrypoint();
        }
        catch
        {
            try
            {
                await loader.UnloadAsync();
            }
            catch (Exception unloadException)
            {
                // 元の例外を優先して再スローする
                Debug.LogException(unloadException);
            }

            throw;
        }

        _current = new ActiveScene(definition, args, loader, entrypoint);
        await entrypoint.InvokeEnterAsync(context, cancellation);
    }

    private static SceneLifetimeScope FindSceneScope(Scene scene, SceneDefinition definition)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var scope = root.GetComponentInChildren<SceneLifetimeScope>(true);
            if (scope == null) continue;

            if (scope.Container == null || scope.EnterContext == null)
            {
                throw new InvalidOperationException($"{nameof(SceneLifetimeScope)} in {definition} was not built by {nameof(SceneNavigator)}");
            }

            return scope;
        }

        throw new InvalidOperationException($"{nameof(SceneLifetimeScope)} was not found in {definition}");
    }

    public void Dispose()
    {
        if (_disposeCts.IsCancellationRequested) return;

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
