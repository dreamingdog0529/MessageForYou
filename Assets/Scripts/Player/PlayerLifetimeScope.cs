using ECM2;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace M4U.Player
{
    public sealed class PlayerLifetimeScope : LifetimeScope
    {
        [SerializeField] private Character _ecm2 = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IPlayerMovement, ECM2PlayerMovement>(Lifetime.Singleton)
                .WithParameter(_ecm2);
            
            builder.RegisterEntryPoint<PlayerEntrypoint>();
        }
    }
}
