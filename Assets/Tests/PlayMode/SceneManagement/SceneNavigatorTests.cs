using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.App.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace M4U.Tests.SceneManagement;

public sealed class SceneNavigatorTests
{
    private SceneCallRecorder _recorder = null!;
    private FakeSceneLoaderFactory _loaderFactory = null!;
    private LifetimeScope _rootScope = null!;
    private SceneNavigator _navigator = null!;
    private Scene _standaloneScene;

    [SetUp]
    public void SetUp()
    {
        _recorder = new SceneCallRecorder();
        _loaderFactory = new FakeSceneLoaderFactory()
            .Map(TestScenes.A, typeof(SceneALifetimeScope))
            .Map(TestScenes.B, typeof(SceneBLifetimeScope))
            .Map(TestScenes.WithoutScope, null)
            .MapUnbound(TestScenes.Unbound, typeof(SceneALifetimeScope));

        var recorder = _recorder;
        _rootScope = LifetimeScope.Create(builder => builder.RegisterInstance(recorder), "TestRootScope");
        _navigator = new SceneNavigator(_rootScope, _loaderFactory);
    }

    [UnityTearDown]
    public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
    {
        _navigator.Dispose();
        await _loaderFactory.UnloadAllAsync();
        if (_standaloneScene.IsValid() && _standaloneScene.isLoaded)
        {
            await SceneManager.UnloadSceneAsync(_standaloneScene).ToUniTask();
        }
        if (_rootScope != null) UnityEngine.Object.Destroy(_rootScope.gameObject);
    });

    /// <summary>エディタでシーンを直接再生した状態を再現する。SceneNavigator を経由せずにスコープを構築し、アクティブシーンにする</summary>
    private SceneLifetimeScope CreateStandaloneScene(Type scopeType)
    {
        _standaloneScene = SceneManager.CreateScene("StandaloneScene");

        var gameObject = new GameObject("SceneScope");
        gameObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(gameObject, _standaloneScene);

        var scope = (SceneLifetimeScope)gameObject.AddComponent(scopeType);
        scope.parentReference.Object = _rootScope;
        gameObject.SetActive(true);

        SceneManager.SetActiveScene(_standaloneScene);
        return scope;
    }

    [Test]
    public void StandaloneScope_IsBuiltWithDefaultContext()
    {
        var scope = CreateStandaloneScene(typeof(SceneALifetimeScope));

        Assert.That(scope.IsStandalone, Is.True);
        Assert.That(scope.Parent, Is.SameAs(_rootScope));

        var context = (SceneEnterContext<int, CounterState>)scope.Container.Resolve<SceneEnterContext>();
        Assert.That(context.Scene, Is.EqualTo(SceneKey.Builtin("StandaloneScene")));
        Assert.That(context.Mode, Is.EqualTo(SceneTransitionMode.Reset));
        Assert.That(context.From, Is.Null);
        Assert.That(context.Args, Is.EqualTo(0));
        Assert.That(context.RestoredState, Is.Null);
    }

    [UnityTest]
    public IEnumerator TryEnterLoadedScene_EntersStandaloneScene() => UniTask.ToCoroutine(async () =>
    {
        var scope = CreateStandaloneScene(typeof(SceneALifetimeScope));

        var entered = await _navigator.TryEnterLoadedSceneAsync();

        Assert.That(entered, Is.True);
        Assert.That(_recorder.Enters, Has.Count.EqualTo(1));
        Assert.That(_recorder.Enters[0], Is.SameAs(scope.EnterContext));
        Assert.That(_recorder.InjectedContexts[0], Is.SameAs(_recorder.Enters[0]));
        Assert.That(_navigator.Current, Is.EqualTo(SceneKey.Builtin("StandaloneScene")));
        Assert.That(_navigator.CanGoBack, Is.False);
        Assert.That(_navigator.IsTransitioning, Is.False);
    });

    [UnityTest]
    public IEnumerator TryEnterLoadedScene_ThenReset_UnloadsStandaloneScene() => UniTask.ToCoroutine(async () =>
    {
        CreateStandaloneScene(typeof(SceneALifetimeScope));
        await _navigator.TryEnterLoadedSceneAsync();

        await _navigator.ResetAsync(TestScenes.B, "next");

        Assert.That(_recorder.Exits[0].Scene, Is.EqualTo(SceneKey.Builtin("StandaloneScene")));
        Assert.That(_standaloneScene.isLoaded, Is.False);
        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.B.Key));
    });

    [UnityTest]
    public IEnumerator TryEnterLoadedScene_WithoutStandaloneScope_ReturnsFalse() => UniTask.ToCoroutine(async () =>
    {
        var entered = await _navigator.TryEnterLoadedSceneAsync();

        Assert.That(entered, Is.False);
        Assert.That(_navigator.Current, Is.Null);
        Assert.That(_recorder.Enters, Is.Empty);
    });

    [UnityTest]
    public IEnumerator TryEnterLoadedScene_AfterEntering_Throws() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);

        await AssertThrowsAsync<InvalidOperationException>(async () => await _navigator.TryEnterLoadedSceneAsync());
    });

    [UnityTest]
    public IEnumerator TransitionToSceneWithStandaloneScope_ThrowsAndUnloadsScene() => UniTask.ToCoroutine(async () =>
    {
        await AssertThrowsAsync<InvalidOperationException>(() => _navigator.ResetAsync(TestScenes.Unbound, 0));

        Assert.That(_navigator.Current, Is.Null);
        Assert.That(_loaderFactory.Loaded, Is.Empty);
        Assert.That(_recorder.Enters, Is.Empty);
    });

    [UnityTest]
    public IEnumerator Reset_EntersSceneWithArgs() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 7);

        var context = (SceneEnterContext<int, CounterState>)_recorder.Enters[0];
        Assert.That(context.Mode, Is.EqualTo(SceneTransitionMode.Reset));
        Assert.That(context.Args, Is.EqualTo(7));
        Assert.That(context.From, Is.Null);
        Assert.That(context.RestoredState, Is.Null);

        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.A.Key));
        Assert.That(_navigator.CanGoBack, Is.False);
        Assert.That(_navigator.IsTransitioning, Is.False);
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_loaderFactory.Loaded[0].Scene));
    });

    [UnityTest]
    public IEnumerator SceneScope_IsChildOfRootScope_AndReceivesInjectedContext() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);

        var scope = _loaderFactory.Loaded[0].Scope!;
        Assert.That(scope.Parent, Is.SameAs(_rootScope));
        Assert.That(_recorder.InjectedContexts, Has.Count.EqualTo(1));
        Assert.That(_recorder.InjectedContexts[0], Is.SameAs(_recorder.Enters[0]));
        Assert.That(scope.Container.Resolve<SceneEnterContext>(), Is.SameAs(_recorder.Enters[0]));
    });

    [UnityTest]
    public IEnumerator Push_PassesArgsAndUnloadsPreviousScene() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);

        await _navigator.PushAsync(TestScenes.B, "hello");

        var exit = _recorder.Exits[0];
        Assert.That(exit.Scene, Is.EqualTo(TestScenes.A.Key));
        Assert.That(exit.Mode, Is.EqualTo(SceneTransitionMode.Push));
        Assert.That(exit.To, Is.EqualTo(TestScenes.B.Key));

        var context = (SceneEnterContext<string, EmptySceneState>)_recorder.Enters[1];
        Assert.That(context.Mode, Is.EqualTo(SceneTransitionMode.Push));
        Assert.That(context.Args, Is.EqualTo("hello"));
        Assert.That(context.From, Is.EqualTo(TestScenes.A.Key));
        Assert.That(context.RestoredState, Is.Null);

        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.B.Key));
        Assert.That(_navigator.CanGoBack, Is.True);
        Assert.That(_loaderFactory.Loaded, Has.Count.EqualTo(1));
        Assert.That(_loaderFactory.Loaded[0].Key, Is.EqualTo(TestScenes.B.Key));
    });

    [UnityTest]
    public IEnumerator Back_RebuildsPreviousSceneFromCapturedState() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 5);

        _recorder.NextStateValue = 42;
        await _navigator.PushAsync(TestScenes.B, "hello");

        var wentBack = await _navigator.BackAsync();

        Assert.That(wentBack, Is.True);

        var exit = _recorder.Exits[1];
        Assert.That(exit.Scene, Is.EqualTo(TestScenes.B.Key));
        Assert.That(exit.Mode, Is.EqualTo(SceneTransitionMode.Back));
        Assert.That(exit.To, Is.EqualTo(TestScenes.A.Key));

        var context = (SceneEnterContext<int, CounterState>)_recorder.Enters[2];
        Assert.That(context.Mode, Is.EqualTo(SceneTransitionMode.Back));
        Assert.That(context.IsRestored, Is.True);
        Assert.That(context.Args, Is.EqualTo(5));
        Assert.That(context.RestoredState?.Value, Is.EqualTo(42));
        Assert.That(context.From, Is.EqualTo(TestScenes.B.Key));

        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.A.Key));
        Assert.That(_navigator.CanGoBack, Is.False);
        Assert.That(_loaderFactory.Loaded, Has.Count.EqualTo(1));
        Assert.That(_loaderFactory.Loaded[0].Key, Is.EqualTo(TestScenes.A.Key));
    });

    [UnityTest]
    public IEnumerator Back_ThroughMultipleLevels_RestoresEachState() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);
        _recorder.NextStateValue = 10;
        await _navigator.PushAsync(TestScenes.A, 2);
        _recorder.NextStateValue = 20;
        await _navigator.PushAsync(TestScenes.B, "top");

        await _navigator.BackAsync();
        var second = (SceneEnterContext<int, CounterState>)_recorder.Enters[3];
        Assert.That(second.Args, Is.EqualTo(2));
        Assert.That(second.RestoredState?.Value, Is.EqualTo(20));

        await _navigator.BackAsync();
        var first = (SceneEnterContext<int, CounterState>)_recorder.Enters[4];
        Assert.That(first.Args, Is.EqualTo(1));
        Assert.That(first.RestoredState?.Value, Is.EqualTo(10));

        Assert.That(_navigator.CanGoBack, Is.False);
    });

    [UnityTest]
    public IEnumerator Back_WithEmptyHistory_ReturnsFalse() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);

        var wentBack = await _navigator.BackAsync();

        Assert.That(wentBack, Is.False);
        Assert.That(_recorder.Exits, Is.Empty);
        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.A.Key));
        Assert.That(_navigator.IsTransitioning, Is.False);
    });

    [UnityTest]
    public IEnumerator Replace_DoesNotPushHistory() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);

        await _navigator.ReplaceAsync(TestScenes.B, "replaced");

        Assert.That(_recorder.Exits[0].Mode, Is.EqualTo(SceneTransitionMode.Replace));
        Assert.That(_recorder.Enters[1].Mode, Is.EqualTo(SceneTransitionMode.Replace));
        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.B.Key));
        Assert.That(_navigator.CanGoBack, Is.False);
    });

    [UnityTest]
    public IEnumerator Reset_ClearsHistory() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);
        await _navigator.PushAsync(TestScenes.B, "b");
        Assert.That(_navigator.CanGoBack, Is.True);

        await _navigator.ResetAsync(TestScenes.A, 2);

        Assert.That(_navigator.CanGoBack, Is.False);
        Assert.That(_recorder.Enters[2].Mode, Is.EqualTo(SceneTransitionMode.Reset));
        Assert.That(_recorder.Enters[2].From, Is.EqualTo(TestScenes.B.Key));
    });

    [UnityTest]
    public IEnumerator ConcurrentTransition_Throws() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);

        var gate = new UniTaskCompletionSource();
        _recorder.EnterGate = gate;
        var push = _navigator.PushAsync(TestScenes.B, "slow").Preserve();

        Assert.That(_navigator.IsTransitioning, Is.True);
        await AssertThrowsAsync<InvalidOperationException>(() => _navigator.ResetAsync(TestScenes.A, 2));
        await AssertThrowsAsync<InvalidOperationException>(async () => await _navigator.BackAsync());

        gate.TrySetResult();
        await push;

        Assert.That(_navigator.IsTransitioning, Is.False);
        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.B.Key));
    });

    [UnityTest]
    public IEnumerator Cancellation_BeforeUnload_KeepsCurrentScene() => UniTask.ToCoroutine(async () =>
    {
        await _navigator.ResetAsync(TestScenes.A, 1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(() => _navigator.PushAsync(TestScenes.B, "b", cts.Token));

        Assert.That(_navigator.Current, Is.EqualTo(TestScenes.A.Key));
        Assert.That(_navigator.CanGoBack, Is.False);
        Assert.That(_navigator.IsTransitioning, Is.False);
        Assert.That(_loaderFactory.Loaded, Has.Count.EqualTo(1));
        Assert.That(_loaderFactory.Loaded[0].Key, Is.EqualTo(TestScenes.A.Key));
    });

    [UnityTest]
    public IEnumerator MismatchedDefinition_ThrowsAndUnloadsScene() => UniTask.ToCoroutine(async () =>
    {
        await AssertThrowsAsync<InvalidOperationException>(() => _navigator.ResetAsync(TestScenes.MismatchedA, "x"));

        Assert.That(_navigator.Current, Is.Null);
        Assert.That(_navigator.IsTransitioning, Is.False);
        Assert.That(_loaderFactory.Loaded, Is.Empty);
        Assert.That(_recorder.Enters, Is.Empty);
    });

    [UnityTest]
    public IEnumerator SceneWithoutScope_ThrowsAndUnloadsScene() => UniTask.ToCoroutine(async () =>
    {
        await AssertThrowsAsync<InvalidOperationException>(() => _navigator.ResetAsync(TestScenes.WithoutScope, 0));

        Assert.That(_navigator.Current, Is.Null);
        Assert.That(_loaderFactory.Loaded, Is.Empty);
    });

    [UnityTest]
    public IEnumerator Disposed_RejectsTransition() => UniTask.ToCoroutine(async () =>
    {
        _navigator.Dispose();

        await AssertThrowsAsync<ObjectDisposedException>(() => _navigator.ResetAsync(TestScenes.A, 1));
    });

    private static async UniTask AssertThrowsAsync<TException>(Func<UniTask> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception e)
        {
            Assert.Fail($"Expected {typeof(TException).Name} but got {e.GetType().Name}: {e.Message}");
            return;
        }

        Assert.Fail($"Expected {typeof(TException).Name} but no exception was thrown");
    }
}
