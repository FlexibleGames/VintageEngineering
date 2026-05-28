using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Base BlockEntity for all machines.
    /// <br>Has an accessor for the ElectricBEBehavior installed on the entity.</br>
    /// </summary>
    public abstract class ElectricContainerBE : BlockEntityOpenableContainer
    {
        public ElectricBEBehavior Electric { get; private set; }

        /// <summary>
        /// Universal base variable to limit client MarkDirty events to more resonable beats. Increment by deltatime in SimTick events.<br/>
        /// Reset in SimTick when over the threshold (example: if (_clientUpdateDelay > 0.5) MarkDirty(true); _clientUpdateDelay = 0f;)<br/>
        /// This value is not and should not be saved to disk.
        /// </summary>
        internal float _clientUpdateDelay = 0f;

        /// <summary>
        /// Utility for setting, starting, and stopping animations.
        /// </summary>
        public BlockEntityAnimationUtil AnimUtil
        {
            get
            {
                return Electric.AnimUtil;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Electric.IsLoaded = true;
        }

        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);
            Electric = GetBehavior<ElectricBEBehavior>();
            if (Electric == null)
            {
                worldForResolve.Logger.Fatal("The Electric behavior is required on {0}", Block.Code);
                throw new FormatException($"The Electric behavior is required on {Block.Code}");
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack)
        {
            base.OnBlockPlaced(byItemStack);
            // <see cref="BlockEntityContainer.OnBlockPlaced"/> has a bug where it doesn't call the
            // block entity behaviors. So call them here to work around the bug.
            foreach (BlockEntityBehavior behavior in Behaviors)
            {
                behavior.OnBlockPlaced(byItemStack);
            }
        }

        public override void OnBlockUnloaded()
        {
            Electric.IsLoaded = false;
            base.OnBlockUnloaded();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // The base WiredBlock handles the wire disconnection and drops.
            Electric.IsLoaded = false;
            base.OnBlockBroken(byPlayer);
        }
    }
}
