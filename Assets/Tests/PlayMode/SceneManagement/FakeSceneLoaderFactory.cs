using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.App.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace M4U.Tests.SceneManagement;

/// <summary>
/// シーンアセットを使わず、実行時に空のシーンを作ってスコープを配置するローダー
/// </summary>
public sealed class FakeSceneLoaderFactory : ISceneLoaderFactory
{
    private readonly Dictionary<string, Type?> _scopeTypes = new();
    private readonly List<FakeSceneLoader> _loaded = new();
    private int _sceneCounter;

    public IReadOnlyList<FakeSceneLoader> Loaded => _loaded;

    public FakeSceneLoaderFactory Map(SceneDefinition definition, Type? scopeType)
    {
        _scopeTypes[definition.Key.Name] = scopeType;
        return this;
    }

    public ISceneLoader Create(SceneKey key)
    {
        if (!_scopeTypes.TryGetValue(key.Name, out var scopeType))
        {
            throw new InvalidOperationException($"Scene is not mapped: {key}");
        }

        return new FakeSceneLoader(this, key, scopeType, $"{key.Name}_{_sceneCounter++}");
    }

    public async UniTask UnloadAllAsync()
    {
        foreach (var loader in _loaded.ToArray())
        {
            await loader.UnloadAsync();
        }
    }

    public sealed class FakeSceneLoader : ISceneLoader
    {
        private readonly FakeSceneLoaderFactory _owner;
        private readonly Type? _scopeType;
        private readonly string _sceneName;

        public SceneKey Key { get; }
        public Scene Scene { get; private set; }
        public SceneLifetimeScope? Scope { get; private set; }

        public FakeSceneLoader(FakeSceneLoaderFactory owner, SceneKey key, Type? scopeType, string sceneName)
        {
            _owner = owner;
            _scopeType = scopeType;
            _sceneName = sceneName;
            Key = key;
        }

        public async UniTask<Scene> LoadAsync(CancellationToken cancellation)
        {
            await UniTask.Yield();

            Scene = SceneManager.CreateScene(_sceneName);
            _owner._loaded.Add(this);

            if (_scopeType != null)
            {
                var gameObject = new GameObject("SceneScope");
                gameObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(gameObject, Scene);

                Scope = (SceneLifetimeScope)gameObject.AddComponent(_scopeType);

                // 実シーンのロードと同様に、ロード中の Awake でスコープを構築させる
                gameObject.SetActive(true);
            }

            cancellation.ThrowIfCancellationRequested();
            return Scene;
        }

        public async UniTask UnloadAsync()
        {
            if (!_owner._loaded.Remove(this)) return;

            if (Scene.IsValid() && Scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(Scene).ToUniTask();
            }
        }
    }
}
