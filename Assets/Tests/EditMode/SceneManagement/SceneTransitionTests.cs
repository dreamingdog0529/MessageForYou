using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.App.SceneManagement;
using NUnit.Framework;

namespace M4U.Tests.SceneManagement;

public sealed class SceneTransitionTests
{
    private struct TestState
    {
        public int Value;
    }

    private static readonly SceneDefinition<int, TestState> Definition = new(SceneKey.Builtin("Test"));

    [Test]
    public void SceneDefinition_ExposesArgsAndStateTypes()
    {
        Assert.That(Definition.Key, Is.EqualTo(SceneKey.Builtin("Test")));
        Assert.That(Definition.ArgsType, Is.EqualTo(typeof(int)));
        Assert.That(Definition.StateType, Is.EqualTo(typeof(TestState)));
    }

    [Test]
    public void CreateEnterContext_FromBoxedValues_RestoresTypedValues()
    {
        SceneDefinition untyped = Definition;
        var from = SceneKey.Builtin("Prev");

        var context = untyped.CreateEnterContext(
            SceneTransitionMode.Back, from, 7, new TestState { Value = 42 });

        var typed = (SceneEnterContext<int, TestState>)context;
        Assert.That(typed.Scene, Is.EqualTo(Definition.Key));
        Assert.That(typed.Mode, Is.EqualTo(SceneTransitionMode.Back));
        Assert.That(typed.From, Is.EqualTo(from));
        Assert.That(typed.Args, Is.EqualTo(7));
        Assert.That(typed.RestoredState?.Value, Is.EqualTo(42));
        Assert.That(typed.IsRestored, Is.True);
    }

    [Test]
    public void CreateEnterContext_WithoutState_HasNoRestoredState()
    {
        SceneDefinition untyped = Definition;

        var context = (SceneEnterContext<int, TestState>)untyped.CreateEnterContext(
            SceneTransitionMode.Push, null, 1, null);

        Assert.That(context.RestoredState, Is.Null);
        Assert.That(context.IsRestored, Is.False);
    }

    private sealed class TestEntrypoint : SceneEntrypoint<int, TestState>
    {
        public SceneEnterContext<int, TestState>? Entered { get; private set; }

        public override UniTask EnterAsync(SceneEnterContext<int, TestState> context, CancellationToken cancellation)
        {
            Entered = context;
            return UniTask.CompletedTask;
        }

        public override TestState CaptureState() => new() { Value = 99 };
    }

    [Test]
    public void SceneEntrypoint_InvokeEnter_PassesTypedContext()
    {
        var entrypoint = new TestEntrypoint();
        var context = Definition.CreateEnterContext(SceneTransitionMode.Reset, null, 3, null);

        entrypoint.InvokeEnterAsync(context, CancellationToken.None).Forget();

        Assert.That(entrypoint.Entered, Is.SameAs(context));
    }

    private sealed class DerivedEntrypoint : IntermediateEntrypoint
    {
        public override TestState CaptureState() => default;
    }

    private abstract class IntermediateEntrypoint : SceneEntrypoint<int, TestState>
    {
        public override UniTask EnterAsync(SceneEnterContext<int, TestState> context, CancellationToken cancellation)
            => UniTask.CompletedTask;
    }

    [TestCase(typeof(TestEntrypoint))]
    [TestCase(typeof(DerivedEntrypoint))]
    public void Accepts_EntrypointWithMatchingArgsAndState(Type entrypointType)
    {
        Assert.That(Definition.Accepts(entrypointType), Is.True);
    }

    [Test]
    public void Accepts_RejectsMismatchedOrNonEntrypointType()
    {
        var other = new SceneDefinition<string, TestState>(SceneKey.Builtin("Other"));

        Assert.That(other.Accepts(typeof(TestEntrypoint)), Is.False);
        Assert.That(Definition.Accepts(typeof(string)), Is.False);
    }

    [Test]
    public void SceneEntrypoint_InvokeEnter_RejectsMismatchedContext()
    {
        var entrypoint = new TestEntrypoint();
        var other = new SceneDefinition<string, TestState>(SceneKey.Builtin("Other"))
            .CreateEnterContext(SceneTransitionMode.Reset, null, "x", null);

        Assert.Throws<InvalidOperationException>(() => entrypoint.InvokeEnterAsync(other, CancellationToken.None));
    }

    [Test]
    public void SceneEntrypoint_InvokeCaptureState_BoxesState()
    {
        var state = new TestEntrypoint().InvokeCaptureState();

        Assert.That(state, Is.TypeOf<TestState>());
        Assert.That(((TestState)state).Value, Is.EqualTo(99));
    }
}
