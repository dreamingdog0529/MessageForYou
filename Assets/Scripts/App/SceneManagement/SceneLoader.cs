using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace M4U.App.SceneManagement;

public interface ISceneLoader
{
    SceneKey Key { get; }

    UniTask<Scene> LoadAsync(CancellationToken cancellation);
    UniTask UnloadAsync();
}

public interface ISceneLoaderFactory
{
    ISceneLoader Create(SceneKey key);
}

public sealed class SceneLoaderFactory : ISceneLoaderFactory
{
    public ISceneLoader Create(SceneKey key) => key.Type switch
    {
        SceneType.Builtin => new BuiltinSceneLoader(key),
        SceneType.Addressables => new AddressableSceneLoader(key),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key.Type, "Unsupported scene type"),
    };
}

public sealed class BuiltinSceneLoader : ISceneLoader
{
    public SceneKey Key { get; }
    private Scene _scene;

    public BuiltinSceneLoader(SceneKey key)
    {
        if (!key.IsBuiltin) throw new ArgumentException("Key must be a builtin scene", nameof(key));

        Key = key;
    }

    public async UniTask<Scene> LoadAsync(CancellationToken cancellation)
    {
        if (_scene.IsValid()) throw new InvalidOperationException($"Scene is already loaded: {Key}");

        var operation = SceneManager.LoadSceneAsync(Key.Name, LoadSceneMode.Additive)
            ?? throw new InvalidOperationException($"Failed to start loading scene: {Key}");

        // Additive でロードしたシーンは末尾に追加される。遷移は同時に実行されないため、他のロードと取り違えない
        var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

        // シーンロードは途中で中断できないため、キャンセルはロード完了後に反映する
        await operation.ToUniTask(cancellationToken: CancellationToken.None);

        _scene = scene;
        cancellation.ThrowIfCancellationRequested();
        return _scene;
    }

    public async UniTask UnloadAsync()
    {
        if (!_scene.IsValid() || !_scene.isLoaded) return;

        var scene = _scene;
        _scene = default;

        var operation = SceneManager.UnloadSceneAsync(scene);
        if (operation != null) await operation.ToUniTask();
    }
}

/// <summary>遷移を経由せずに既にロードされているシーンを保持し、アンロードだけを担う</summary>
public sealed class LoadedSceneLoader : ISceneLoader
{
    public SceneKey Key { get; }
    private Scene _scene;

    public LoadedSceneLoader(SceneKey key, Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("Scene must be loaded", nameof(scene));

        Key = key;
        _scene = scene;
    }

    public UniTask<Scene> LoadAsync(CancellationToken cancellation)
        => throw new InvalidOperationException($"{nameof(LoadedSceneLoader)} cannot load scenes: {Key}");

    public async UniTask UnloadAsync()
    {
        if (!_scene.IsValid() || !_scene.isLoaded) return;

        var scene = _scene;
        _scene = default;

        // 最後にロードされているシーンはアンロードできないため、受け皿となる空のシーンを先に作る
        if (SceneManager.loadedSceneCount == 1)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("Empty"));
        }

        var operation = SceneManager.UnloadSceneAsync(scene);
        if (operation != null) await operation.ToUniTask();
    }
}
