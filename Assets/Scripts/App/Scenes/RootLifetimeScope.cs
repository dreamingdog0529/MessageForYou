using M4U.App.SceneManagement;
using M4U.Input;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace M4U.App.Scenes
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField] private SceneType _initialSceneType = SceneType.Builtin;
        [SerializeField] private string _initialSceneName = "";

        protected override void Configure(IContainerBuilder builder)
        {
            var inputService = ConfigureInput(builder);
            ConfigureSceneManagement(builder);

            // 初期シーンは Args = Unit, State = EmptySceneState のエントリポイントを持つ必要がある
            var initialScene = string.IsNullOrEmpty(_initialSceneName)
                ? null
                : new SceneDefinition<Unit, EmptySceneState>(_initialSceneType switch
                {
                    SceneType.Addressables => SceneKey.Addressables(_initialSceneName),
                    _ => SceneKey.Builtin(_initialSceneName),
                });

            builder.RegisterEntryPoint<RootEntrypoint>()
                .WithParameter(inputService)
                .WithParameter(initialScene);
        }

        private static InputService ConfigureInput(IContainerBuilder builder)
        {
            var inputService = new InputService();
            builder.RegisterInstance<IInputService>(inputService);
            return inputService;
        }

        private void ConfigureSceneManagement(IContainerBuilder builder)
        {
            builder.Register<ISceneLoaderFactory, SceneLoaderFactory>(Lifetime.Singleton);
            builder.Register<ISceneNavigator, SceneNavigator>(Lifetime.Singleton)
                .WithParameter<LifetimeScope>(this);
        }
    }
}
