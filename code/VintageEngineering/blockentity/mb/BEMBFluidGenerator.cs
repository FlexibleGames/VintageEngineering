using System;
using System.Collections.Generic;
using System.Text;
using VintageEngineering.API;
using VintageEngineering.GUI;
using VintageEngineering.Multiblock;
using VintageEngineering.RecipeSystem;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class BEMBFluidGenerator : VEMBEntityCore, IVELiquidInterface
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
        /// Slot ID 0 is fluid input<br/>        
        /// </summary>
        private InventoryGeneric _inventory;

        private LiquidFuelProperties _liquidFuelProps;
        public LiquidFuelProperties LiquidFuelProps => _liquidFuelProps;
        private float _currentBurnTime = 0f;
        private bool _isBurning = false;
        /// <summary>
        /// Slot ID 0 is fluid input
        /// </summary>
        public override InventoryBase Inventory => _inventory;
        public ItemSlot InputSlot => _inventory[0];
        /// <summary>
        /// How full (0-100) is the Input Tank
        /// </summary>
        public int PercentInputTank
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

        public override string InventoryClassName => "InvFluidGenerator";
        public int[] InputLiquidContainerSlotIDs => [0];
        public int[] OutputLiquidContainerSlotIDs => null;
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

            string left = BlockFacing.FromCode(rotside).GetCCW().Code;
            string right = BlockFacing.FromCode(rotside).GetCW().Code;
            if (blockFacing.Code != left && blockFacing.Code != right) return null;
            return _inventory[0] as ItemSlotLiquidOnly;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            return null;
        }

        private void SlotModified(int slotid)
        {
            if (slotid == 0)
            {
                _clientUpdateDelay += 0.1f;
                if (!_inventory[slotid].Empty)
                {
                    if (FindMatchingRecipe())
                    {                        
                        SetState(EnumBEState.On);
                    }
                }
            }
        }

        public ItemSlot GetAutoPushIntoSlot(BlockFacing face, ItemSlot fromSlot)
        {
            if (fromSlot.Empty || !fromSlot.Itemstack.Collectible.IsLiquid()) return null;
            if (!InputSlot.Empty && InputSlot.Itemstack.Collectible != fromSlot.Itemstack.Collectible) return null;

            if (fromSlot.Itemstack.Collectible.GetType() != typeof(ItemLiquidFuel)) return null;

            return _inventory[0];
        }

        public bool FindMatchingRecipe()
        {
            if (InputSlot.Empty) return false;
            if (InputSlot.Itemstack.Collectible == null) return false;
            if (InputSlot.Itemstack.Collectible.GetType() != typeof(ItemLiquidFuel)) return false;
            if (InputSlot.Itemstack.ItemAttributes["liquidfuel"].Exists)
            {
                _liquidFuelProps = LiquidFuelProperties.FromJSON(InputSlot.Itemstack.ItemAttributes["liquidfuel"]);
            }
            else
            {
                _liquidFuelProps = null;                
            }
            return true;
        }
        #endregion

        public BEMBFluidGenerator()
        {
            _inventory = new InventoryGeneric(1, null, null, delegate (int id, InventoryGeneric self)
            {                
                return new ItemSlotLiquidOnly(self, 800);
            });
            _inventory.SlotModified += SlotModified;
            _inventory.OnGetAutoPushIntoSlot = GetAutoPushIntoSlot;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _inventory.Pos = Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", api);
            (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres = Block.Attributes["fluidCapacityLiters"].AsFloat(1f);            
            
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
                    AnimUtil.InitializeAnimator("vembfluidgen", null, null, new Vec3f(0f, GetRotation(), 0f));
                }
            }
            if (IsBuilt)
            {                
                if (!InputSlot.Empty)
                {
                    FindMatchingRecipe();
                }
                else
                {
                    _currentBurnTime = 0f;
                    _liquidFuelProps = null;
                }
            }
            else
            {
                SetState(EnumBEState.Sleeping);
            }
        }

        public override void ActivateCore(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {
            //if (IsBuilt) FindValidateExtensions();
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
                if (!InputSlot.Empty) _isBurning = FindMatchingRecipe();
                if (_isBurning) SetState(EnumBEState.On);
                _updateBouncer = 0f;
            }
            if (IsBuilt && !_isBurning && !InputSlot.Empty)
            {
                _isBurning = FindMatchingRecipe();
            }
            if (IsBuilt && Electric.MachineState == EnumBEState.On && _isBurning)
            {                
                long ratedpow = (long)(_liquidFuelProps.EnergyPPS * dt);
                if (Electric.CurrentPower != Electric.MaxPower && (Electric.MaxPower - Electric.CurrentPower) >= ((ulong)ratedpow))
                {
                    _currentBurnTime += dt;
                    if (_currentBurnTime >= _liquidFuelProps.Duration)
                    {                        
                        _currentBurnTime = 0f;
                        InputSlot.TakeOut(1);
                        _isBurning = FindMatchingRecipe();
                        if (!_isBurning) SetState(EnumBEState.Sleeping);
                    }
                    Electric.electricpower += ((ulong)ratedpow);
                }
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
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-input")}: {IconHelper.PercentToBar(PercentInputTank, 10)} {PercentInputTank:N0}% {inputfluid}");            
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(percentpower, 10)} {percentpower:N0}%");
            if (_liquidFuelProps != null) dsc.AppendLine($"{_liquidFuelProps.EnergyPPS} PPS");
            if (!InputSlot.Empty) dsc.AppendLine($"{InputSlot.Itemstack.StackSize} Portions");
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
                        // player is good, Derrick is built, player is holding a wrench
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
            tree.SetFloat("currentburntime", _currentBurnTime);
            tree.SetBool("isburning", _isBurning);
            tree.SetString("machinestate", Electric.MachineState.ToString());
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));            
            EnumBEState syncstate = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "On"));
            if (!InputSlot.Empty) FindMatchingRecipe();
            _currentBurnTime = tree.GetFloat("currentburntime", 0f);
            _isBurning = tree.GetBool("isburning", false);
            if (Electric.MachineState != syncstate) SetState(syncstate);
        }

        #endregion
    }
}
