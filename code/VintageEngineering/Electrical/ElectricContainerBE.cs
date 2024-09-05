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
        /// Utility for setting, starting, and stopping animations.
        /// </summary>
        protected BlockEntityAnimationUtil AnimUtil
        {
            get
            {
                return Electric.AnimUtil;
            }
        }

        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);
            Electric = GetBehavior<ElectricBEBehavior>();
            if (Electric == null)
            {
                worldForResolve.Logger.Fatal("The Electric behavior is required on {0}", Block.Code);
                throw new FormatException("The Electric behavior is required on ${Block.Code}");
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
    }
}
