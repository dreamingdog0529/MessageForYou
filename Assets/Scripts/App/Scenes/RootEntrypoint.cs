using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.App.SceneManagement;
using M4U.Input;
using R3;
using VContainer.Unity;

namespace M4U.App.Scenes
{
    public sealed class RootEntrypoint : IAsyncStartable, IDisposable
    {
        private readonly InputService _inputService;
        private readonly ISceneNavigator _sceneNavigator;
        private readonly SceneDefinition<Unit, EmptySceneState>? _initialScene;
        private IDisposable? _subscription;

        public RootEntrypoint(
            InputService inputService,
            ISceneNavigator sceneNavigator,
            SceneDefinition<Unit, EmptySceneState>? initialScene)
        {
            _inputService = inputService;
            _sceneNavigator = sceneNavigator;
            _initialScene = initialScene;
        }

        public static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // TODO: 終了確認ダイアログを表示してから Application.Quit() する
#endif
        }

        async UniTask IAsyncStartable.StartAsync(CancellationToken cancellation)
        {
            _inputService.Initialize();

            _subscription = _inputService.Common
                .Where(x => x.Cancel)
                .Subscribe(_ => QuitApp());

            if (!await _sceneNavigator.TryEnterLoadedSceneAsync(cancellation) && _initialScene != null)
            {
                await _sceneNavigator.ResetAsync(_initialScene, Unit.Default, cancellation);
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
