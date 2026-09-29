using ECM2;
using UnityEngine;

namespace M4U.Player
{
    public interface IPlayerMovement
    {
        bool IsGrounded { get; }

        void Move(float horizontal, float vertical);
        void Jump();
        void StopJumping();
    }

    public sealed class ECM2PlayerMovement : IPlayerMovement
    {
        private readonly Character _character;

        public bool IsGrounded => _character.IsGrounded();

        public ECM2PlayerMovement(Character character)
        {
            _character = character;
        }

        public void Move(float horizontal, float vertical)
        {
            var direction = new Vector3(horizontal, 0f, vertical);

            if (_character.camera != null)
            {
                direction = direction.relativeTo(_character.camera.transform);
            }

            _character.SetMovementDirection(direction);
        }

        public void Jump() => _character.Jump();

        public void StopJumping() => _character.StopJumping();
    }
}
