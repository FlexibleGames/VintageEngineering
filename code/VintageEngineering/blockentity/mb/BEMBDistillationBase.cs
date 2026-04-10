using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    public class BEMBDistillationBase : VEMBEntityCore, IVELiquidInterface
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

        // Min power draw is 2k pps, max power draw is currently 6000 pps. This can be tuned.

        /// <summary>
        /// What the FirstCodePart is for the Stackable Extension to this machine
        /// </summary>
        private string _extensionCode = string.Empty;
        /// <summary>
        /// How much PPS does each extension add to the crafting power cost.
        /// </summary>
        private long _extensionPPS = 0;
        /// <summary>
        /// Number of Distillation Extensions found.
        /// </summary>
        private int _numExtensions = 0;
        /// <summary>
        /// Validate extensions occasionaly 
        /// </summary>
        private float _extensionValidationDelay = 0f;
        /// <summary>
        /// Extension Positions, indexed by the order how they are stacked on the base.<br/>
        /// Index 0, if present, is directly on top of the base.
        /// </summary>
        private Dictionary<int, BlockPos> _extensionPositions = new();
        /// <summary>
        /// Value used if machine is sleeping.
        /// </summary>
        private float _updateBouncer = 0f;

        #region RecipeStuff
        private RecipeDistillationTower _currentRecipe;
        private long _recipePowerApplied;

        public RecipeDistillationTower CurrentRecipe => _currentRecipe;

        public float RecipeProgress
        {
            get
            {
                if (_inventory[0].Empty || _currentRecipe == null) return 0f;
                else
                {
                    return _recipePowerApplied / _currentRecipe.PowerPerCraft;
                }
            }
        }

        public bool FindMatchingRecipe()
        {
            if (Api == null) return false;
            if (InputSlot.Empty)
            {
                _currentRecipe = null;
                SetState(EnumBEState.Sleeping);
                _recipePowerApplied = 0;
                return false;
            }

            List<RecipeDistillationTower> mrecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.DistillationRecipes;
            if (mrecipes == null || mrecipes.Count == 0) return false;

            foreach (RecipeDistillationTower mrecipe in mrecipes)
            {
                if (mrecipe.Enabled && mrecipe.Matches(InputSlot, null, _numExtensions))
                {                    
                    _currentRecipe = mrecipe;
                    SetState(EnumBEState.On);
                    return true;
                }
            }
            _recipePowerApplied = 0;
            SetState(EnumBEState.Sleeping);
            return false;
        }

        #endregion

        #region InventoryStuff
        /// <summary>
        /// Slot ID 0 is fluid input, 1 is item output, 2 is fluid output<br/>
        /// SlotID 3 - 8 can be used, but they pass the check to Extensions
        /// </summary>
        private InventoryGeneric _inventory;

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
        /// <summary>
        /// How full (0-100) is the Output Tank
        /// </summary>
        public int PercentOutputTank
        {
            get
            {
                if (_inventory[2].Empty) return 0;
                else
                {
                    int portions = _inventory[2].Itemstack.StackSize;
                    float cap = (_inventory[2] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[2].Itemstack).ItemsPerLitre;
                    float full = portions / cap;
                    return (int)(full * 100);
                }
            }
        }
        /// <summary>
        /// How full (0-100) is the Output Item Slot
        /// </summary>        
        public int PercentOutputItem
        {
            get
            {
                if (_inventory[1].Empty) return 0;
                else
                {
                    int stack = _inventory[1].Itemstack.StackSize;
                    float cap = _inventory[1].Itemstack.Collectible.MaxStackSize;
                    float full = stack / cap;
                    return (int)(full * 100);
                }
            }
        }

        public bool AnyExtensionFull()
        {
            if (_numExtensions == 0) return false;
            foreach (KeyValuePair<int, BlockPos> pair in _extensionPositions)
            {
                BEMBDistillationExt distbe = Api.World.BlockAccessor.GetBlockEntity<BEMBDistillationExt>(pair.Value);
                if (distbe != null)
                {
                    if (!distbe.HasRoomInOutput(0, null)) return true;
                }
            }
            return false;
        }

        public bool HasRoomInExtension(int extindex, ItemStack forStack)
        {
            if (extindex >= _numExtensions) return false;
            if (!_extensionPositions.ContainsKey(extindex)) return false;
            else
            {
                BEMBDistillationExt distbe = Api.World.BlockAccessor.GetBlockEntity<BEMBDistillationExt>(_extensionPositions[extindex]);
                if (distbe != null)
                {
                    if (distbe.HasRoomInOutput(0, forStack)) return true;
                }
            }
            return true;
        }

        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0) return false; // this is input...
            if (slotid > 2)
            {
                // checking extensions
                return HasRoomInExtension(slotid - 2, forStack);
            }
            if (forStack == null)
            { 
                return PercentOutputItem < 100 && PercentOutputTank < 100; 
            }
            else
            {
                if (_inventory[slotid].Empty) return true;
                if (_inventory[slotid].Itemstack.Collectible.Code == forStack.Collectible.Code)
                {
                    if (_inventory[slotid].Itemstack.StackSize == _inventory[slotid].Itemstack.Collectible.MaxStackSize) return false;
                }
                else return false;
            }
            return true;
        }
        
        public override string InventoryClassName => "InvDistBase";
        public int[] InputLiquidContainerSlotIDs => [0];
        public int[] OutputLiquidContainerSlotIDs => [2];
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
            if (blockFacing.Code != left) return null;
            return _inventory[0] as ItemSlotLiquidOnly;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            string rotside = Block.Variant["side"];
            if (blockFacing == null) return _inventory[2] as ItemSlotLiquidOnly;
            // if this is facing North, the south face is the fluid output
            string right = BlockFacing.FromCode(rotside).GetCW().Code;
            if (blockFacing.Code != right) return null;

            return _inventory[2] as ItemSlotLiquidOnly;
        }

        private void SlotModified(int slotid)
        {
            if (slotid == 0 && _currentRecipe == null)
            {
                FindMatchingRecipe();
            }
        }

        public ItemSlot GetAutoPushIntoSlot(BlockFacing face, ItemSlot fromSlot)
        {
            if (fromSlot.Empty || !fromSlot.Itemstack.Collectible.IsLiquid()) return null;
            return _inventory[0];
        }

        #endregion

        public BEMBDistillationBase()
        {
            _inventory = new InventoryGeneric(3, null, null, delegate (int id, InventoryGeneric self)
            {
                if (id == 1) return new ItemSlot(self); // item output
                return new ItemSlotLiquidOnly(self, 50); // we don't need super large capacities here.
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
            (_inventory[2] as ItemSlotLiquidOnly).CapacityLitres = Block.Attributes["fluidCapacityLiters"].AsFloat(1f);
            _extensionCode = Block.Attributes["extensionCode"].AsString(string.Empty);
            _extensionPPS = ((long)Block.Attributes["extensionPPS"].AsDouble(0));
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 1000); // 10 TPS standard machine
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vembdistbase", null, null, new Vec3f(0f, GetRotation(), 0f));
                }
            }
            if (IsBuilt)
            { 
                FindValidateExtensions();
                if (!InputSlot.Empty) FindMatchingRecipe();
            }
            else
            {
                SetState(EnumBEState.Sleeping);
            }
        }

        public override void ActivateCore(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {
            if (IsBuilt) FindValidateExtensions();
            else
            {
                _numExtensions = 0;
                _extensionPositions.Clear();
            }
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
            if (Electric.IsSleeping)
            {
                _updateBouncer += dt;
                if (_updateBouncer < 2f) return;
                _updateBouncer = 0f;
            }

            if (IsBuilt && Electric.MachineState == EnumBEState.On)
            {
                long ratedpow = (long)((Electric.MaxPPS + (ulong)(_numExtensions * _extensionPPS)) * dt);                
                if (Electric.CurrentPower == 0 || Electric.CurrentPower < (ulong)ratedpow) return;

                if (_currentRecipe != null)
                {
                    if (!HasRoomInOutput(-1, null) || AnyExtensionFull()) return;

                    if (RecipeProgress < 1f)
                    {
                        // we are currently crafting
                        _recipePowerApplied += ratedpow;
                        Electric.electricpower -= (ulong)ratedpow;
                    }
                    else
                    {
                        // a craft cycle completed
                        
                    }
                }

                _clientUpdateDelay += dt;
                if (_clientUpdateDelay >= 0.5f)
                {
                    _clientUpdateDelay = 0f;
                    MarkDirty(true);
                }
            }
        }

        public bool FindValidateExtensions()
        {
            if (_extensionCode == string.Empty) return false; // nothing to find.

            BlockPos aboveus = Pos.Copy();
            aboveus.Up(4);
            int numextfound = 0;

            while (aboveus.Y < 256)
            {
                Block blockabove = Api.World.BlockAccessor.GetBlock(aboveus);
                string abovecode = blockabove.Code.Path;
                if (blockabove.Id == 0) break; // exit on first air block detected (should also include rock and soil, etc)...
                // would it be better to do a whitelist rather than a blacklist? Doubtful as any block could be a building block.
                if (abovecode.Contains("rock") || abovecode.Contains("soil") || abovecode.Contains("glass") || abovecode.Contains("forestfloor") || abovecode.Contains("clay")) break;

                if (abovecode.Contains(_extensionCode))
                {
                    if (_extensionPositions.ContainsKey(numextfound))
                    {
                        if (_extensionPositions[numextfound] != aboveus)
                        {
                            Api.Logger.Warning($"VintEng: Validating Distillation Extensions found a mis-match in BlockPos: Expected {_extensionPositions[numextfound]} and had cached {aboveus}");
                        }
                        _extensionPositions[numextfound] = aboveus.Copy();
                    }
                    else
                    {
                        _extensionPositions.Add(numextfound, aboveus.Copy());                        
                    }
                    Api.World.BlockAccessor.GetBlockEntity<BEMBDistillationExt>(aboveus)?.FindValidateBase();
                    numextfound++;
                }
                aboveus.Up(1);
            }
            _numExtensions = numextfound;
            return true;
        }
        // A call for an extension that is built above the base to tell the the base to revalidate
        public void NewExtensionBuilt(BlockPos newExt)
        {
            FindValidateExtensions();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            float powerpercent = 0f;
            if (Electric.MaxPower > 0) powerpercent = (float)(Electric.CurrentPower / (double)Electric.MaxPower);
            int percentpower = (int)(powerpercent * 100);
            string inputfluid  = _inventory[0].Empty ? Lang.Get("vinteng:gui-word-empty") : _inventory[0].Itemstack.Collectible.GetHeldItemName(_inventory[0].Itemstack);
            string outputfluid = _inventory[2].Empty ? Lang.Get("vinteng:gui-word-empty") : _inventory[2].Itemstack.Collectible.GetHeldItemName(_inventory[2].Itemstack);

            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-input")}: {IconHelper.PercentToBar(PercentInputTank, 10)} {PercentInputTank:N0}% {inputfluid}");
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-output")}: {IconHelper.PercentToBar(PercentOutputTank, 10)} {PercentOutputTank:N0}% {outputfluid}");
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(percentpower, 10)} {percentpower:N0}%");
            dsc.AppendLine($"#{Lang.Get("vinteng:gui-word-extensions")}: {_numExtensions}");
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
                        BlockFacing control = BlockFacing.FromCode(Block.Variant["side"]).GetCW();
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
            tree.SetLong("recipepower", _recipePowerApplied);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _recipePowerApplied = tree.GetLong("recipepower");
            EnumBEState syncstate = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "On"));
            if (Electric.MachineState != syncstate) SetState(syncstate);
        }

        #endregion
    }
}
