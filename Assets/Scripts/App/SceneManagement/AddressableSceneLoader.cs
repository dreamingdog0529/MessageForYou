using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace M4U.App.SceneManagement;

public sealed class AddressableSceneLoader : ISceneLoader
{
    public SceneKey Key { get; }
    private AsyncOperationHandle<SceneInstance> _handle;

    public AddressableSceneLoader(SceneKey key)
    {
        if (!key.IsAddressables) throw new ArgumentException("Key must be an addressable scene", nameof(key));

        Key = key;
    }

    public async UniTask<Scene> LoadAsync(CancellationToken cancellation)
    {
        if (_handle.IsValid()) throw new InvalidOperationException($"Scene is already loaded: {Key}");

        _handle = Addressables.LoadSceneAsync(Key.Name, LoadSceneMode.Additive);

        // シーンロードは途中で中断できないため、キャンセルはロード完了後に反映する
        SceneInstance instance;
        try
        {
            instance = await _handle.ToUniTask(cancellationToken: CancellationToken.None);
        }
        catch
        {
            // 失敗したハンドルは UnloadSceneAsync できないため、ここで解放する
            if (_handle.IsValid()) Addressables.Release(_handle);
            _handle = default;
            throw;
        }

        cancellation.ThrowIfCancellationRequested();
        return instance.Scene;
    }

    public async UniTask UnloadAsync()
    {
        if (!_handle.IsValid()) return;

        var handle = _handle;
        _handle = default;

        await Addressables.UnloadSceneAsync(handle).ToUniTask();
    }
}
