using System;
using System.Collections.Generic;
using System.Text;
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
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class BEMBTemporalForge: VEMBEntityCore
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

        private RecipeTemporalForge _currentRecipe;
        public ulong _recipePowerApplied = 0;

        public float RecipeProgress
        {
            get
            {
                if (_currentRecipe != null)
                {
                    return (_recipePowerApplied / (float)_currentRecipe.PowerPerCraft);
                }
                else return 0f;
            }
        }

        private ModSystemRifts _riftSys;
        private SystemTemporalStability _temporalSys;

        #region InventoryStuff
        /// <summary>
        /// Slot ID 0 is input, 1 is output<br/>        
        /// </summary>
        private InventoryGeneric _inventory;

        /// <summary>
        /// Slot ID 0 is input, 1 is output
        /// </summary>
        public override InventoryBase Inventory => _inventory;
        public ItemSlot InputSlot => _inventory[0];

        public override string InventoryClassName => "InvStarter";

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
            return _inventory[0];
        }
        private ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            return _inventory[1];
        }

        public bool FindMatchingRecipe()
        {
            if (Api == null) return false;            

            
            if (InputSlot.Empty)
            {
                _currentRecipe = null;
                _recipePowerApplied = 0;
                SetState(EnumBEState.Sleeping);
                return false;
            }

            _currentRecipe = null;
            List<RecipeTemporalForge> tempforgerecipes = Api.ModLoader.GetModSystem<VERecipeRegistrySystem>(true).TemporalForgeRecipes;

            if (tempforgerecipes == null) return false;

            foreach (RecipeTemporalForge mprecipe in tempforgerecipes)
            {
                if (mprecipe.Enabled && mprecipe.Matches([InputSlot]))
                {
                    _currentRecipe = mprecipe;                    
                    //SetState(EnumBEState.On);
                    return true;
                }
            }
            _currentRecipe = null;            
            _recipePowerApplied = 0;
            SetState(EnumBEState.Sleeping);
            return false;
        }
        #endregion

        public BEMBTemporalForge()
        {
            _inventory = new InventoryGeneric(2, null, null);

            _inventory.SlotModified += SlotModified;
            _inventory.OnGetAutoPushIntoSlot = GetAutoPushIntoSlot;
            _inventory.OnGetAutoPullFromSlot = GetAutoPullFromSlot;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            _riftSys = api.ModLoader.GetModSystem<ModSystemRifts>(true);
            _temporalSys = api.ModLoader.GetModSystem<SystemTemporalStability>(true);

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
                    AnimUtil.InitializeAnimator("vembtempforge", null, null, new Vec3f(0f, GetRotation(), 0f));
                }
            }
            if (IsBuilt)
            {
                if (!InputSlot.Empty)
                {
                    FindMatchingRecipe();
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
                _updateBouncer = 0f;
            }
            if (IsBuilt && !InputSlot.Empty && _currentRecipe == null)
            {
                FindMatchingRecipe();
            }
            // ONLY active during a storm or a rift is nearby 
            bool isStorming = _temporalSys.StormData.nowStormActive;
            float nearestRift = NearestRiftDistance(this.Pos.ToVec3d());
            if (isStorming || nearestRift < 21f)
            {
                if (Electric.MachineState != EnumBEState.On && _currentRecipe != null)
                {
                    SetState(EnumBEState.On);
                }
            }
            else
            {
                if (Electric.MachineState == EnumBEState.On)
                {
                    SetState(EnumBEState.Sleeping);
                }
            }
            if (IsBuilt && Electric.MachineState == EnumBEState.On && _currentRecipe != null)
            {
                float speed = isStorming ? _temporalSys.StormStrength : 0f;
                speed += nearestRift < 21f ? ((21f - nearestRift) / 20f) : 0f;
                speed += 0.05f;
                // speed will be between 0.05 and 2.05

                ulong tickpower = ((ulong)(Electric.MaxPPS * dt));
                ulong scaledpower = (ulong)(tickpower * speed);

                if (Electric.CurrentPower < tickpower) return;
                if (_recipePowerApplied < ((ulong)_currentRecipe.PowerPerCraft))
                {
                    _recipePowerApplied += scaledpower;
                    Electric.electricpower -= tickpower;
                }
                else
                {
                    // crafting cycle complete
                    if (_currentRecipe.TryCraft(Api, [InputSlot], [_inventory[1]]))
                    {
                        Electric.electricpower -= tickpower;
                        _recipePowerApplied = 0;
                        FindMatchingRecipe();
                    }
                    else
                    {
                        if (_clientUpdateDelay >= 0.5f)
                        {
                            Api.Logger.Error($"VintEng: Temporal Forge TryCraft recipe (Code: {_currentRecipe.Code}) returned false.");
                        }
                    }
                }
            }
            if (_clientUpdateDelay >= 0.5f)
            {
                _clientUpdateDelay = 0f;
                MarkDirty(true);
            }
        }

        public float NearestRiftDistance(Vec3d pos)
        {
            Rift nrift = this._riftSys.ServerRifts.Nearest((Rift rift) => (double)rift.Position.SquareDistanceTo(pos));
            if (nrift != null)
            {
                return nrift.Position.DistanceTo(pos);
            }
            return 9999f;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            float powerpercent = 0f;
            if (Electric.MaxPower > 0) powerpercent = (float)(Electric.CurrentPower / (double)Electric.MaxPower);
            int percentpower = (int)(powerpercent * 100);
            base.GetBlockInfo(forPlayer, dsc);
            if (!InputSlot.Empty) dsc.AppendLine($"{Lang.Get("vinteng:gui-word-input")}: {InputSlot.Itemstack.GetName()}");            
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(percentpower, 10)} {percentpower:N0}%");
            if (_currentRecipe != null)
            {
                dsc.AppendLine($"{Lang.Get("vinteng:gui-word-crafting")}: {_currentRecipe.Outputs[0].ResolvedItemStack?.GetName()}");
                dsc.AppendLine($"{IconHelper.PercentToBar((int)(RecipeProgress * 100), 10)} {(RecipeProgress * 100):N1}%");
                if (Electric.MachineState == EnumBEState.Sleeping)
                {
                    dsc.AppendLine(Lang.Get("vinteng:tempforge-nostorm"));
                }
            }            
            if (Electric.CurrentPower < (Electric.MaxPPS / 10f))
            {
                dsc.AppendLine(Lang.Get("vinteng:gui-machine-lowpower"));
            }
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
                            FindMatchingRecipe();
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
            tree.SetLong("recipepower", ((long)_recipePowerApplied));
            tree.SetString("machinestate", Electric.MachineState.ToString());
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            EnumBEState syncstate = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "On"));
            FindMatchingRecipe();

            if (Electric.MachineState != syncstate) SetState(syncstate);
            _recipePowerApplied = ((ulong)tree.GetLong("recipepower"));
        }

        #endregion
    }
}
