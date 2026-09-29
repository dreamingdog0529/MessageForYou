using System;
using R3;
using UnityEngine;

namespace M4U.Input
{

    public interface IInputService
    {
        ReadOnlyReactiveProperty<CommonInputSnapshot> Common { get; }
        ReadOnlyReactiveProperty<PlayerInputSnapshot> Player { get; }
    }

    public sealed class InputService : IInputService, IDisposable
    {
        public ReadOnlyReactiveProperty<CommonInputSnapshot> Common => _common;
        public ReadOnlyReactiveProperty<PlayerInputSnapshot> Player => _player;

        private readonly ReactiveProperty<CommonInputSnapshot> _common = new();
        private readonly ReactiveProperty<PlayerInputSnapshot> _player = new();
        private readonly InputMaster _master = new();
        private IDisposable? _subscription;

        public void Initialize()
        {
            _master.Enable();

            _subscription = Observable.EveryUpdate(UnityFrameProvider.PreUpdate)
                .Subscribe(_ => UpdateInputValues());
        }

        private void UpdateInputValues()
        {
            var move = _master.Player.Move.ReadValue<Vector2>();

            _common.Value = new CommonInputSnapshot(_master.Common.Cancel.IsPressed());
            _player.Value = new PlayerInputSnapshot(move.x, move.y, _master.Player.Jump.IsPressed());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _master.Dispose();
            _common.Dispose();
            _player.Dispose();
        }
    }
}
