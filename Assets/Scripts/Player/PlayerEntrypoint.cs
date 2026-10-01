using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using M4U.Input;
using R3;
using VContainer.Unity;
using UnityEngine;

namespace M4U.Player
{
    public sealed class PlayerEntrypoint : IAsyncStartable, IDisposable
    {
        private readonly IPlayerMovement _mover;
        private readonly IInputService _inputService;
        private IDisposable? _subscriptions;

        public PlayerEntrypoint(IPlayerMovement mover, IInputService input)
        {
            _mover = mover;
            _inputService = input;
        }

        public UniTask StartAsync(CancellationToken cancellation = default)
        {
            var d1 = _inputService.Player
                .Do(x => Debug.Log($"Input: {x}"))
                .Pairwise()
                .Subscribe(HandleUpdate);

            _subscriptions = Disposable.Combine(d1);

            return UniTask.CompletedTask;
        }

        private void HandleUpdate((PlayerInputSnapshot Previous, PlayerInputSnapshot Current) input)
        {
            var (previous, current) = input;

            _mover.Move(current.Horizontal, current.Vertical);

            if (current.Jump && !previous.Jump)
            {
                _mover.Jump();
            }
            else if (!current.Jump && previous.Jump)
            {
                _mover.StopJumping();
            }
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}