using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.App.SceneManagement;
using R3;
using UnityEngine;

namespace M4U.App.Scenes.DebugMovement
{
    public sealed class DebugMovementSceneEntrypoint : SceneEntrypoint<Unit, EmptySceneState>
    {
        public override UniTask EnterAsync(SceneEnterContext<Unit, EmptySceneState> context, CancellationToken cancellation)
        {
            Debug.Log($"[{nameof(DebugMovementSceneEntrypoint)}] Entered {context.Scene} (Mode: {context.Mode}, From: {context.From?.ToString() ?? "none"})");
            return UniTask.CompletedTask;
        }

        public override EmptySceneState CaptureState() => default;
    }
}
