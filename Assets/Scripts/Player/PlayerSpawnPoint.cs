using JetBrains.Annotations;
using UnityEngine;

namespace M4U.Player
{
    /// <summary>
    /// 自身の周囲(XZ 平面上の円)からランダムに地面上のスポーン位置を探す。
    /// </summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        private const int MaxAttempts = 10;

        [SerializeField] private float _radius = 1.0f;
        [SerializeField] private float _maxDropDistance = 5.0f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        public bool TryGetSpawnPosition(out Vector3 position)
        {
            for (var i = 0; i < MaxAttempts; i++)
            {
                if (Physics.Raycast(RandomCastOrigin(), Vector3.down, out var hit, _maxDropDistance, _groundLayers, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point;
                    return true;
                }
            }

            position = default;
            return false;
        }

        private Vector3 RandomCastOrigin() => transform.position + (Vector3)Random.insideUnitCircle * _radius;

        [UsedImplicitly]
        private void OnDrawGizmosSelected()
        {
            var center = transform.position;
            var bottom = center + Vector3.down * _maxDropDistance;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, _radius);
            Gizmos.DrawLine(center, bottom);
        }
    }
}
