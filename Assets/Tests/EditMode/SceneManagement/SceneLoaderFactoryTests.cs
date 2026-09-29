using System;
using M4U.App.SceneManagement;
using NUnit.Framework;

namespace M4U.Tests.SceneManagement;

public sealed class SceneLoaderFactoryTests
{
    [Test]
    public void Create_ReturnsBuiltinLoader_ForBuiltinKey()
    {
        var key = SceneKey.Builtin("Title");

        var loader = new SceneLoaderFactory().Create(key);

        Assert.That(loader, Is.TypeOf<BuiltinSceneLoader>());
        Assert.That(loader.Key, Is.EqualTo(key));
    }

    [Test]
    public void Create_ReturnsAddressableLoader_ForAddressablesKey()
    {
        var key = SceneKey.Addressables("Assets/Scenes/Title.unity");

        var loader = new SceneLoaderFactory().Create(key);

        Assert.That(loader, Is.TypeOf<AddressableSceneLoader>());
        Assert.That(loader.Key, Is.EqualTo(key));
    }

    [Test]
    public void BuiltinSceneLoader_RejectsAddressablesKey()
    {
        Assert.Throws<ArgumentException>(() => new BuiltinSceneLoader(SceneKey.Addressables("Assets/Scenes/Title.unity")));
    }

    [Test]
    public void AddressableSceneLoader_RejectsBuiltinKey()
    {
        Assert.Throws<ArgumentException>(() => new AddressableSceneLoader(SceneKey.Builtin("Title")));
    }
}
