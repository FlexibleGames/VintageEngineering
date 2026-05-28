using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using VintageEngineering.API;
using VintageEngineering.GUI;
using VintageEngineering.Multiblock;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{ 
    public class BEMBPumpJack : VEMBEntityCore, IVELiquidInterface
    {
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private int _sourceBlocksPerSecond = 0;
        private float _wellValidationDelay = 0f;

        /// <summary>
        /// Represents the position of the Well Source block, entity includes the interface IFluidWell.
        /// </summary>
        private BlockPos _wellPosition = null;

        #region InitAndMisc
        public BEMBPumpJack()
        {            
            float capacity = 8001f;
            _inventory = new InventoryGeneric(1, null, null, delegate (int id, InventoryGeneric self)
            {
                return new ItemSlotLiquidOnly(self, capacity);
            });
            _inventory.SlotModified += SlotModified;
            _inventory.OnGetSuitability += GetSuitability;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            float powerpercent = 0f;
            if (Electric.MaxPower > 0) powerpercent = (float)(Electric.CurrentPower / (double)Electric.MaxPower);
            int percentpower = (int)(powerpercent * 100);

            base.GetBlockInfo(forPlayer, dsc);            
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-fluid")}: {IconHelper.PercentToBar(PercentFluid, 10)} {PercentFluid:N0}%");
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(percentpower, 10)} {percentpower:N0}%");
            if (_wellPosition != null)
            {
                IFluidWell thewell = GetWellAt(_wellPosition);
                if (thewell != null)
                {
                    dsc.AppendLine(Lang.Get("vinteng:gui-wellinfo") + ":" + Environment.NewLine);
                    Api.World.BlockAccessor.GetBlockEntity(_wellPosition)?.GetBlockInfo(forPlayer, dsc);
                }
            }
            else
            {
                dsc.AppendLine($"{Lang.Get("vinteng:gui-wellerror")}");
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 500, 0);
                _sourceBlocksPerSecond = Block.Attributes["sourceBlocksPerSecond"].AsInt(1);
                _sourceBlocksPerSecond = Math.Clamp(_sourceBlocksPerSecond, 1, 10);
                if (_wellPosition == null) // first time initializing (aka just built)
                {
                    // there are, of course, many edge cases why position is null here
                    // but the tick will revalidate every 120 seconds
                    // maybe I should trigger it on the interaction block instead?
                    ValidateWell();
                }
                _wellValidationDelay = 0f;
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vembpumpjack", null, null, new Vec3f(0f, GetRotation(), 0f));
                }
            }
            _inventory.Pos = Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", api);
            (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres = Block.Attributes["fluidCapacityLiters"].AsFloat(1f);
        }

        /// <summary>
        /// What is the BlockPos of the Pumpjack head? Must be over the Well Casing that leads down to the well.
        /// </summary>
        public BlockPos HeadPosition
        {
            get
            {
                string side = Block.Variant["side"] ?? "north";
                BlockFacing pointingto = BlockFacing.FromCode(side);
                return Pos.AddCopy(pointingto, 2);
            }
        }

        public BlockPos WellPosition => _wellPosition;

        public int GetRotation()
        {
            string side = Block.Variant["side"];
            int adjustedIndex = (BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3 & 3;
            return adjustedIndex * 90;
        }
        #endregion

        public void OnSimTick(float dt)
        {
            if (sapi == null) return; // only run this on the server

            if (Block.Variant["state"] != "built") return;

            _wellValidationDelay += dt;
            EnumBEState newstate = MachineState;
            if (_wellValidationDelay >= 120f)
            {
                // revalidate every 2 minutes
                if (!ValidateWell())
                {
                    // if the well is invalid, shut it down
                    SetState(EnumBEState.Sleeping);                    
                }
                else 
                {
                    // well is valid, but we may have to wake up
                    if (MachineState == EnumBEState.Sleeping || MachineState == EnumBEState.Off)
                    {
                        newstate = EnumBEState.On;
                    }
                }
                _wellValidationDelay = 0f;
            }
            ulong powertick = ((ulong)(Electric.MaxPPS * dt));
            if (Electric.CurrentPower == 0 || Electric.CurrentPower < powertick)
            {
                // if we're supposed to be on, but we don't have enough power, sleep
                if (newstate == EnumBEState.On) newstate = EnumBEState.Sleeping;
            }
            else 
            {
                if (_wellPosition != null) newstate = EnumBEState.On; // power is green
            }

            if (_wellPosition == null) newstate = EnumBEState.Sleeping; // final check

            if (newstate == EnumBEState.On)
            {
                // we have enough power and a valid well! \o/
                int literspersecond = _sourceBlocksPerSecond * 1000;
                int portionperliter = 100;                
                IFluidWell thewell = GetWellAt(_wellPosition);
                if (thewell == null) { SetState(EnumBEState.Sleeping); return; }
                Item portion = Api.World.GetItem(new AssetLocation(thewell?.FluidPortionCode));
                if (portion != null)
                {
                    ItemStack portionstack = new ItemStack(portion);
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(portionstack);
                    if (props != null)
                    {
                        // this is an insane chain of things I have to do just to get the damn props
                        portionperliter = (int)props.ItemsPerLitre; // almost always 100, should be 1000 for milliliters. 
                    }
                }
                // a truly crazy way of turning blocks/s of source fluid into portions/s
                // by default 1 block/s * 1000 * 100 = 100,000 portions per second
                long portionpersecond = literspersecond * portionperliter;
                double availportion = Output.CapacityLitres * portionperliter; // 100 portions per liter is the standard... default to this, if it's empty this is the available by default.
                if (!_inventory[0].Empty) availportion = Output.CapacityLitres * BlockLiquidContainerBase.GetContainableProps(Output.Itemstack).ItemsPerLitre - _inventory[0].Itemstack.StackSize;

                portionpersecond = Math.Min(portionpersecond, (long)availportion);

                long texastea = thewell.PumpTick(dt, portionpersecond);
                if (!_inventory[0].Empty) _inventory[0].Itemstack.StackSize += (int)texastea;
                else
                {
                    _inventory[0].Itemstack = new ItemStack(portion, (int)texastea);
                }
                if (texastea > 0) Electric.electricpower -= powertick; // Electric.RatedPower(dt, false);                
            }
            if (MachineState != newstate) SetState(newstate);

            _clientUpdateDelay += dt;
            if (_clientUpdateDelay > 0.5)
            {
                _clientUpdateDelay = 0f;
                MarkDirty(true);
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            // force a well validation check
            if (Api.Side == EnumAppSide.Server) _wellValidationDelay = 121f;
            return true;
        }

        /// <summary>
        /// Validates the Well. _wellPosition will be set after this call if there is one and the well is valid.
        /// </summary>
        /// <returns>True if well is valid and ready for pumpin'</returns>
        public bool ValidateWell()
        {
            if (Block.Variant["state"] != "built") return false;            
            AssetLocation casingcode = new AssetLocation(Block.Attributes["wellCasingCode"].AsString());
            Block casing = Api.World.GetBlock(casingcode);
            if (casing == null) return false; // if casing can't be found, bounce
            if (Api.World.BlockAccessor.GetBlock(HeadPosition.DownCopy()) != casing) return false; // if casing isn't under the head, bounce

            // this validates all casings down to the well, it also sets the well position if found
            if (!ValidateCasings(HeadPosition.DownCopy())) return false;

            return true;
        }

        /// <summary>
        /// Validates all Well Casing blocks from the surface down to the well.<br/>
        /// If the well exists, this will also set the _wellPosition value.<br/>
        /// Returns false if either the casing is broken or the well isn't found.
        /// </summary>
        /// <param name="start">Position to start in. Use Copy() when passing into this function as the position will change.</param>
        /// <returns>True if casing is valid and well is found, otherwise false.</returns>
        public bool ValidateCasings(BlockPos start)
        {
            Block casing = Api.World.GetBlock(new AssetLocation(Block.Attributes["wellCasingCode"].AsString()));
            
            while (start.Y > 0)
            {
                Block blockat = Api.World.BlockAccessor.GetBlock(start.Down());
                if (blockat == casing) continue;
                else
                {
                    if (Api.World.BlockAccessor.GetBlockEntity(start) is IFluidWell)
                    {
                        _wellPosition = start.Copy();
                        return true;
                    }
                    _wellPosition = null;
                    return false;
                }
            }
            _wellPosition = null;
            return false;
        }
      
        public IFluidWell GetWellAt(BlockPos pos)
        {
            if (pos == null) return null;
            return Api.World.BlockAccessor.GetBlockEntity(pos) as IFluidWell;
        }

        #region InventoryStuff
        public virtual bool AllowPipeLiquidTransfer
        {
            get
            {
                if (Block.Attributes == null) return false;
                return Block.Attributes["allowPipeLiquidTransfer"].AsBool(false);
            }
        }

        public virtual bool AllowHeldLiquidTransfer
        {
            get
            {
                if (Block.Attributes == null) return false;
                return Block.Attributes["allowHeldLiquidTransfer"].AsBool(false);
            }
        }

        public virtual float TransferSizeLitresPerSecond
        {
            get
            {
                if (Block.Attributes == null) return 0f;
                return Block.Attributes["transferLitresPerSecond"].AsFloat(0.01f);
            }
        }

        private InventoryGeneric _inventory;
        public override string InventoryClassName => "InvPumpJack";
        public override InventoryBase Inventory => _inventory;

        public ItemSlot OutputSlot => _inventory[0];
        public ItemSlotLiquidOnly Output => OutputSlot as ItemSlotLiquidOnly;

        public int[] InputLiquidContainerSlotIDs => null;

        public int[] OutputLiquidContainerSlotIDs => [ 0 ];

        /// <summary>
        /// How full is the internal tank? 0-100
        /// </summary>
        public int PercentFluid
        {
            get
            {
                if (_inventory[0].Empty) return 0;
                else
                {
                    int portions = _inventory[0].Itemstack.StackSize;
                    float capacity = (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[0].Itemstack).ItemsPerLitre;
                    float full = portions / capacity;
                    return (int)(full * 100);
                }
            }
        }

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot = null)
        {
            // you can not push liquid into this machine.
            return null;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            string rotside = Block.Variant["side"];
            string cw = BlockFacing.FromCode(rotside).GetCW().Code;
            string ccw = BlockFacing.FromCode(rotside).GetCCW().Code;

            // check left and right faces at the very least.
            if (blockFacing.Code != cw && blockFacing.Code != ccw) return null;

            return OutputSlot as ItemSlotLiquidOnly;
        }

        private void SlotModified(int slotid)
        {
            if (slotid == 0) _clientUpdateDelay += 0.05f; //MarkDirty(true);
        }

        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0)
            {
                if (_inventory[slotid].Empty) return true;
                float capportion = (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[0].Itemstack).ItemsPerLitre;
                if (_inventory[slotid].Itemstack.StackSize == capportion) return false;
                return true;
            }
            else return false;
        }

        public float GetSuitability(ItemSlot sourceslot, ItemSlot targetSlot, bool isMerge)
        {
            return (isMerge ? _inventory.BaseWeight + 3f : _inventory.BaseWeight + 1f) + (sourceslot.Inventory is InventoryBasePlayer ? 1 : 0);
        }

        #endregion

        #region MachineState
        private EnumBEState _state;
        public EnumBEState MachineState => _state;
        public bool IsSleeping => _state == EnumBEState.Sleeping;
        public bool IsActive => _state == EnumBEState.On;

        protected virtual void SetState(EnumBEState newstate)
        {
            if (_state == newstate) return;

            _state = newstate;
            if (_state == EnumBEState.On)
            {
                if (AnimUtil != null && Block.Attributes["craftinganimcode"].Exists && capi != null)
                {
                    AnimUtil.StartAnimation(new AnimationMetaData
                    {
                        Animation = Block.Attributes["craftinganimcode"].AsString(),
                        Code = Block.Attributes["craftinganimcode"].AsString(),
                        AnimationSpeed = 2f,
                        EaseOutSpeed = 4f,
                        EaseInSpeed = 1f
                    });
                }
            }
            else
            {
                if (AnimUtil != null && AnimUtil.activeAnimationsByAnimCode.Count > 0)
                {
                    AnimUtil.StopAnimation(Block.Attributes["craftinganimcode"].AsString());
                }
            }
            MarkDirty(true); // _clientUpdateDelay += 0.05f; 
        }
        #endregion

        #region TreeAttributes
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            _inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            if (_wellPosition != null) tree.SetBlockPos("wellposition", _wellPosition);
            tree.SetString("machinestate", MachineState.ToString());
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _wellPosition = tree.GetBlockPos("wellposition", null);
            EnumBEState syncstate = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "On"));
            if (MachineState != syncstate) SetState(syncstate);
        }
        #endregion
    }
}
