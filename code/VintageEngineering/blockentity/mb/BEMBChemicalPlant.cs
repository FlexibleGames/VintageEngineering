using Cairo.Freetype;
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Chem Plant is the top-tier MV Crafter for Oil Based products<br/>
    /// Slot ID 0 & 1 are fluid inputs, 2 is item input<br/>
    /// 3 is item output, 4, 5, 6 are fluid outputs   
    /// </summary>
    public class BEMBChemicalPlant : VEMBEntityCore, IVELiquidInterface
    {
        // 1 fence - black
        // 2 platform - mid gray
        // 3 ladder - light gray
        // 4 heavyeng - dark purple
        // 5 concrete - cyan
        // 6 lighteng - pink
        // 8 fluid - yellow
        // 9 wood slab - orange
        // 10 power - lime
        // 11 interaction - red
        // 12 item io - green
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;

        // Min power draw is 2k pps, max power draw is currently 5000 pps. This can be tuned.

        /// <summary>
        /// Value used if machine is sleeping.
        /// </summary>
        private float _updateBouncer = 0f;

        #region InventoryStuff
        /// <summary>
        /// Slot ID 0 & 1 are fluid inputs, 2 is item input<br/>
        /// 3 is item output, 4, 5, 6 are fluid outputs   
        /// </summary>
        private InventoryGeneric _inventory;

        /// <summary>
        /// Slot ID 0 & 1 are fluid inputs, 2 is item input
        /// 3 is item output, 4, 5, 6 are fluid outputs
        /// </summary>
        public override InventoryBase Inventory => _inventory;
        public ItemSlot InputSlot => _inventory[0];
        /// <summary>
        /// How full (0-100) is the Input Tank
        /// </summary>
        public int PercentInputTank1
        {
            get
            {
                if (_inventory[0].Empty) return 0;
                else
                {
                    int portions = _inventory[0].Itemstack.StackSize;
                    float cap = (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[0].Itemstack).ItemsPerLitre;
                    float full = portions / cap;
                    return (int)(full * 100);
                }
            }
        }
        public int PercentInputTank2
        {
            get
            {
                if (_inventory[1].Empty) return 0;
                else
                {
                    int portions = _inventory[1].Itemstack.StackSize;
                    float cap = (_inventory[1] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[1].Itemstack).ItemsPerLitre;
                    float full = portions / cap;
                    return (int)(full * 100);
                }
            }
        }

        public override string InventoryClassName => "InvChemPlant";
        public int[] InputLiquidContainerSlotIDs => [0, 1];
        public int[] OutputLiquidContainerSlotIDs => [4, 5, 6];
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

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot = null)
        {
            string rotside = Block.Variant["side"];

            if (blockFacing == null) return _inventory[0] as ItemSlotLiquidOnly;

            string ccw = BlockFacing.FromCode(rotside).GetCCW().Code;
            //string opposite = BlockFacing.FromCode(rotside).Opposite.Code;            
            if (blockFacing.Code == ccw) return _inventory[0] as ItemSlotLiquidOnly;
            if (blockFacing.Code == rotside) return _inventory[1] as ItemSlotLiquidOnly;
            return null;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            string rotside = Block.Variant["side"];
            string cw = BlockFacing.FromCode(rotside).GetCW().Code;
            if (blockFacing.Code == cw)
            {
                for (int x = 4; x <= 6; x++)
                {
                    if (!_inventory[x].Empty) return _inventory[x] as ItemSlotLiquidOnly;
                }
            }
            return null;
        }

        private void SlotModified(int slotid)
        {
            if (slotid <= 2)
            {
                _clientUpdateDelay += 0.1f;
                if (FindMatchingRecipe())
                {
                    SetState(EnumBEState.On);
                }
            }
        }

        public ItemSlot GetAutoPushIntoSlot(BlockFacing face, ItemSlot fromSlot)
        {
            string rotside = Block.Variant["side"];
            string opposite = BlockFacing.FromCode(rotside).Opposite.Code;
            if (face.Code == opposite)
            {
                return _inventory[2];
            }
            return null;
        }
        private ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            string rotside = Block.Variant["side"];
            string opposite = BlockFacing.FromCode(rotside).Opposite.Code;
            if (atBlockFace.Code == opposite)
            {
                return _inventory[3];
            }
            return null;
        }

        public bool FindMatchingRecipe()
        {
            return true;
        }
        #endregion

        public BEMBChemicalPlant()
        {
            _inventory = new InventoryGeneric(7, null, null, delegate (int id, InventoryGeneric self)
            {
                if (id == 2 || id == 3)
                {
                    return new ItemSlot(self);
                }
                return new ItemSlotLiquidOnly(self, 50);
            });

            _inventory.SlotModified += SlotModified;
            _inventory.OnGetAutoPushIntoSlot = GetAutoPushIntoSlot;
            _inventory.OnGetAutoPullFromSlot = GetAutoPullFromSlot;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _inventory.Pos = Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", api);
            //(_inventory[0] as ItemSlotLiquidOnly).CapacityLitres = Block.Attributes["fluidCapacityLiters"].AsFloat(1f);

            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 4000); // 10 TPS standard machine
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vembchemplant", null, null, new Vec3f(0f, GetRotation(), 0f));
                }
            }
            if (IsBuilt)
            {
                FindMatchingRecipe();
            }
            else
            {
                SetState(EnumBEState.Sleeping);
            }
        }

        public override void ActivateCore(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {            
        }

        public int GetRotation()
        {
            string side = Block.Variant["side"];
            int adjustedIndex = (BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3 & 3;
            return adjustedIndex * 90;
        }

        public void OnSimTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return;
            _clientUpdateDelay += dt;
            if (Electric.IsSleeping)
            {
                _updateBouncer += dt;
                if (_updateBouncer < 2f) return;
                _updateBouncer = 0f;
            }
            if (IsBuilt && !InputSlot.Empty)
            {
                //found = FindMatchingRecipe();
            }
            if (IsBuilt && Electric.MachineState == EnumBEState.On)
            {
                // Primary "in progress" crafting loop
            }
            if (_clientUpdateDelay >= 0.5f)
            {
                _clientUpdateDelay = 0f;
                MarkDirty(true);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            float powerpercent = 0f;
            if (Electric.MaxPower > 0) powerpercent = (float)(Electric.CurrentPower / (double)Electric.MaxPower);
            int percentpower = (int)(powerpercent * 100);
            string inputfluid = _inventory[0].Empty ? Lang.Get("vinteng:gui-word-empty") : _inventory[0].Itemstack.Collectible.GetHeldItemName(_inventory[0].Itemstack);

            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-input")}: {IconHelper.PercentToBar(PercentInputTank1, 10)} {PercentInputTank1:N0}% {inputfluid}");
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(percentpower, 10)} {percentpower:N0}%");
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                if (byPlayer != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty && Block.Variant["state"] == "built")
                {
                    if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible?.Tool == EnumTool.Wrench)
                    {
                        if (blockSel != null && !Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                        {
                            return false; // only block if we can't interact via permissions with this block
                        }
                        // player is good, MB is built, player is holding a wrench
                        BlockFacing control = BlockFacing.FromCode(Block.Variant["side"]).Opposite;
                        if (blockSel.Face == control)
                        {
                            _inventory.DropAll(byPlayer.Entity.Pos.AsBlockPos.ToVec3d());
                        }
                    }
                }
            }
            return true;
        }

        #region MachineState

        public void SetState(EnumBEState state)
        {
            Electric.MachineState = state;
            if (Electric.MachineState == EnumBEState.On)
            {
                _updateBouncer = 0f;
                if (AnimUtil != null && Block.Attributes["craftinganimcode"].Exists)
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
            MarkDirty(true);
        }
        #endregion

        #region TreeAttributes
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            _inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetString("machinestate", Electric.MachineState.ToString());
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            EnumBEState syncstate = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "On"));
            if (!_inventory[0].Empty || !_inventory[1].Empty || !_inventory[2].Empty) FindMatchingRecipe();
            if (Electric.MachineState != syncstate) SetState(syncstate);
        }

        #endregion
    }
}
