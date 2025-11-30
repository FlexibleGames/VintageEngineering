using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// A simple BlockEntity for machines with no Inventory or GUI
    /// <br>Has an accessor for the ElectricBEBehavior installed on the entity.</br>
    /// </summary>
    public abstract class ElectricSimpleBE : BlockEntity
    {

        public ElectricBEBehavior Electric { get; private set; }

        internal long ListenerID { get; set; }

        //private float clientUpdateBouncer = 0;

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

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            Electric.IsLoaded = true;
            Electric.MachineState = EnumBEState.On;
            if (api.Side == EnumAppSide.Server)
            {
                ListenerID = api.Event.RegisterGameTickListener(new Action<float>(OnGameTick), 500);
            }
        }

        public void OnGameTick(float deltatime)
        {
            if (ListenerID > 0) MarkDirty(true);
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

        public override void OnBlockUnloaded()
        {
            Electric.IsLoaded = false;
            base.OnBlockUnloaded();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // The base WiredBlock handles the wire disconnection and drops.
            Electric.IsLoaded = false;
            if (ListenerID > 0) Api.Event.UnregisterGameTickListener(ListenerID);
            base.OnBlockBroken(byPlayer);
        }
    }
}
