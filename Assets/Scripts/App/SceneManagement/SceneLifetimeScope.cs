using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace M4U.App.SceneManagement;

/// <summary>
/// 各シーンのルートに1つ配置するスコープ。親と <see cref="SceneEnterContext"/> は <see cref="ISceneNavigator"/> がロード時に渡す。
/// シーン内に入れ子のスコープを置く場合は、parentReference でこのスコープを指定する。
/// </summary>
public abstract class SceneLifetimeScope : LifetimeScope
{
    private SceneScopeBinding? _binding;

    internal SceneEnterContext? EnterContext => _binding?.Context;
    internal abstract Type EntrypointType { get; }

    protected override void Awake()
    {
        _binding = SceneScopeBinding.Consume();
        base.Awake();
    }

    protected override LifetimeScope FindParent() => _binding?.Parent ?? base.FindParent();

    protected sealed override void Configure(IContainerBuilder builder)
    {
        if (_binding?.Context is { } context) builder.RegisterInstance(context).As(context.GetType());
        RegisterEntrypoint(builder);
        ConfigureScene(builder);
    }

    private protected abstract void RegisterEntrypoint(IContainerBuilder builder);

    protected virtual void ConfigureScene(IContainerBuilder builder)
    {
    }

    internal SceneEntrypoint ResolveEntrypoint() => Container.Resolve<SceneEntrypoint>();
}

public abstract class SceneLifetimeScope<TEntrypoint> : SceneLifetimeScope
    where TEntrypoint : SceneEntrypoint
{
    internal sealed override Type EntrypointType => typeof(TEntrypoint);

    private protected sealed override void RegisterEntrypoint(IContainerBuilder builder)
    {
        builder.Register<TEntrypoint>(Lifetime.Singleton).As<SceneEntrypoint>().AsSelf();
    }
}

/// <summary>
/// ロード中のシーンの <see cref="SceneLifetimeScope"/> に親とコンテキストを渡す。
/// VContainer の EnqueueParent / Enqueue はシーン内の全スコープに作用するため使わない。
/// </summary>
internal sealed class SceneScopeBinding
{
    private static SceneScopeBinding? s_pending;

    // ドメインリロード無効時、ロード中に Play を止めると Scope.Dispose が呼ばれず残るため
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_pending = null;

    public LifetimeScope Parent { get; }
    public SceneEnterContext Context { get; }

    private SceneScopeBinding(LifetimeScope parent, SceneEnterContext context)
    {
        Parent = parent;
        Context = context;
    }

    public static Scope Enqueue(LifetimeScope parent, SceneEnterContext context)
    {
        if (s_pending != null) throw new InvalidOperationException("Another scene scope binding is pending");

        s_pending = new SceneScopeBinding(parent, context);
        return new Scope();
    }

    public static SceneScopeBinding? Consume()
    {
        var binding = s_pending;
        s_pending = null;
        return binding;
    }

    public readonly struct Scope : IDisposable
    {
        public void Dispose() => s_pending = null;
    }
}
