using System;
using System.Text;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{

    /// <summary>
    /// Blower simply pushes air into another block, used by the Blast Furnace to increase 
    /// crafting temperature and speed. Other uses possible, of course. <br/>
    /// Uses power whether or not machine block is crafting.
    /// </summary>
    public class BEBlower : BlockEntity
    {
        /// <summary>
        /// Is this blower currently powered?
        /// </summary>
        public bool IsActive { get; set; } = false;

        public ElectricBEBehavior Electric { get; private set; }

        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private float _updateBouncer = 0f;
        private long _clientUpdateMS = 0L;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 250, 0);                
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (Electric.AnimUtil != null)
                {
                    Electric.AnimUtil.InitializeAnimator("veblower", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            _clientUpdateMS = api.World.ElapsedMilliseconds;
        }

        public void OnSimTick(float dt)
        {
            // if this is sleeping, only process this tick every 2 seconds.
            if (Electric.IsSleeping)
            {
                _updateBouncer += dt;
                if (_updateBouncer < 2f) return;
                _updateBouncer = 0f;
            }            

            float ratedpower = Electric.MaxPPS * dt;
            if (Electric.CurrentPower > ratedpower)
            {
                if (!CheckForAir()) 
                {
                    SetState(EnumBEState.Sleeping);
                    return; 
                }
                // power is good, we can tick
                if (Electric.MachineState != EnumBEState.On) SetState(EnumBEState.On);
                Electric.electricpower -= (ulong)Math.Round(ratedpower, 0);
            }
            else
            {
                // not enough power, go to sleep
                if (Electric.MachineState != EnumBEState.Sleeping) SetState(EnumBEState.Sleeping);
            }
            if (Api.World.ElapsedMilliseconds - _clientUpdateMS > 500L)
            {
                _clientUpdateMS = Api.World.ElapsedMilliseconds;
                MarkDirty(true);
            }
        }

        public void SetState(EnumBEState state) 
        {
            if (Electric.MachineState != state)
            {
                // only process fully if state is actually changing
                Electric.MachineState = state;
                if (state != EnumBEState.On)
                {
                    IsActive = false;                    
                }
                else 
                { 
                    IsActive = true;                    
                }                
            }
            if (Electric.MachineState == EnumBEState.On)
            {
                if (Electric.AnimUtil != null && base.Block.Attributes["craftinganimcode"].Exists)
                {
                    Electric.AnimUtil.StartAnimation(new AnimationMetaData
                    {
                        Animation = base.Block.Attributes["craftinganimcode"].AsString(),
                        Code = base.Block.Attributes["craftinganimcode"].AsString(),
                        AnimationSpeed = 1f,
                        EaseOutSpeed = 4f,
                        EaseInSpeed = 1f
                    });
                }
            }
            else
            {
                if (Electric.AnimUtil != null && Electric.AnimUtil.activeAnimationsByAnimCode.Count > 0)
                {
                    Electric.AnimUtil.StopAnimation(base.Block.Attributes["craftinganimcode"].AsString());
                }
            }
            MarkDirty(true);
        }

        public int GetRotation()
        {
            string side = Block.Variant["side"];
            // The BlockFacing horiztonal index goes counter-clockwise from east. That needs to be converted so that
            // it goes counter-clockwise from north instead.
            int adjustedIndex = ((BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3) & 3;
            return adjustedIndex * 90;
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

        public bool CheckForAir()
        {
            Block frontof = Api.World.BlockAccessor.GetBlock(this.Pos.AddCopy(BlockFacing.FromCode(this.Block.Variant["side"]).Opposite));
            return frontof.Id == 0;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (!CheckForAir())
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("vinteng:gui-error-airblocked"));
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack)
        {
            base.OnBlockPlaced(byItemStack);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("active", IsActive);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            IsActive = tree.GetBool("active");
            SetState(Electric.MachineState);
        }
    }
}
