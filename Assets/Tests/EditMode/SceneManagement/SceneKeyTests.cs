using System;
using M4U.App.SceneManagement;
using NUnit.Framework;

namespace M4U.Tests.SceneManagement;

public sealed class SceneKeyTests
{
    [Test]
    public void Builtin_CreatesBuiltinKey()
    {
        var key = SceneKey.Builtin("Title");

        Assert.That(key.Name, Is.EqualTo("Title"));
        Assert.That(key.Type, Is.EqualTo(SceneType.Builtin));
        Assert.That(key.IsBuiltin, Is.True);
        Assert.That(key.IsAddressables, Is.False);
    }

    [Test]
    public void Addressables_CreatesAddressablesKey()
    {
        var key = SceneKey.Addressables("Assets/Scenes/Title.unity");

        Assert.That(key.Type, Is.EqualTo(SceneType.Addressables));
        Assert.That(key.IsAddressables, Is.True);
        Assert.That(key.IsBuiltin, Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    public void Builtin_RejectsEmptyName(string? name)
    {
        Assert.Throws<ArgumentException>(() => SceneKey.Builtin(name!));
    }

    [Test]
    public void Addressables_RejectsAddressOutsideScenesFolder()
    {
        Assert.Throws<ArgumentException>(() => SceneKey.Addressables("Title"));
    }

    [Test]
    public void Equality_ComparesNameAndType()
    {
        Assert.That(SceneKey.Builtin("Title"), Is.EqualTo(SceneKey.Builtin("Title")));
        Assert.That(SceneKey.Builtin("Title"), Is.Not.EqualTo(SceneKey.Builtin("Other")));
        Assert.That(
            SceneKey.Builtin("Assets/Scenes/Title"),
            Is.Not.EqualTo(SceneKey.Addressables("Assets/Scenes/Title")));
    }
}
