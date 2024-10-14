using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.inventory;
using VintageEngineering.RecipeSystem.Recipes;
using VintageEngineering.RecipeSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VintageEngineering.GUI;

namespace VintageEngineering
{
    public class BEBlastFurnace : BlockEntityOpenableContainer
    {

        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private GUIBlastFurnace _clientDialog;

        #region InventoryStuff
        private InvBlastFurnace _inventory;        
        public override string InventoryClassName { get { return "InvBlastFurnace"; } }
        /// <summary>
        /// SlotID: 0-3 = input, 4 = fuel, 5 = output
        /// </summary>
        public override InventoryBase Inventory { get { return _inventory; } }

        public ItemSlot[] InputSlots
        {
            get
            {
                return new ItemSlot[]
                {
                    _inventory[0],
                    _inventory[1],
                    _inventory[2],
                    _inventory[3]
                };
            }
        }
        public ItemSlot FuelSlot => _inventory[4];

        public ItemSlot[] OutputSlots
        {
            get
            {
                return new ItemSlot[]
                {
                    _inventory[5]                    
                };
            }
        }

        private void SlotModified(int slotid)
        {
            if (slotid >= 0 || slotid < 5) // input or fuel slot update
            {
                FindMatchingRecipe();
                MarkDirty(true);
                if (_clientDialog != null)
                {
                    _clientDialog.Update(RecipeProgress, CurrentTemp, CurrentRecipe);
                }
            }
        }
        /// <summary>
        /// Output slots IDs are slotid 2 for item, 3 for fluid<br/>
        /// Pass id = 0 and forStack = null to check both outputs.
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0 && forStack == null)
            {
                if (_inventory[5].Empty) return true;
                int itemout = 0;
                
                if (!_inventory[5].Empty)
                {
                    itemout = _inventory[5].Itemstack.Collectible.MaxStackSize - _inventory[5].Itemstack.StackSize;
                }
                return itemout > 0;
            }
            else
            {
                if (slotid != 5) return false; // not an output slot
                if (forStack == null) return false; // need to compare for an actual item
                if (_inventory[slotid].Empty) return true; // slot is empty, good to go.
                else
                {
                    return _inventory[slotid].GetRemainingSlotSpace(forStack) > 0;
                }
            }
        }
        #endregion

        #region MachineState
        private EnumBEState _state;
        public EnumBEState MachineState => _state;

        public bool IsSleeping => _state == EnumBEState.Sleeping;
        public bool IsCrafting => _state == EnumBEState.On;

        protected virtual void SetState(EnumBEState newstate)
        {
            _state = newstate;

            if (_state == EnumBEState.On)
            {
                if (AnimUtil != null && base.Block.Attributes["craftinganimcode"].Exists)
                {
                    AnimUtil.StartAnimation(new AnimationMetaData
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
                if (AnimUtil != null && AnimUtil.activeAnimationsByAnimCode.Count > 0)
                {
                    AnimUtil.StopAnimation(base.Block.Attributes["craftinganimcode"].AsString());
                }
            }
            if (Api != null && Api.Side == EnumAppSide.Client && _clientDialog != null && _clientDialog.IsOpened())
            {
                _clientDialog.Update(RecipeProgress, CurrentTemp, CurrentRecipe);
            }
            MarkDirty(true);
        }
        #endregion

        #region RecipeStuff
        private float _recipeTime = 0f;
        private float _totalCraftTime = 0f;
        private float _maxBurnTemp = 0f;
        private float _currentTemp = 0f;
        private float _tempgoal = 0f;
        private RecipeBlastFurnace _currentRecipe;
        private AlloyRecipe _alloyRecipe;
        private float _updateBouncer = 0f;
        private int _heatPerSecondBase = 1;
        private float _environmentTemp = 20f;
        private float _environmentTempDelay = 0f;
        private float _remainingBurnTime = 0f;
        private float _fuelTotalBurnTime = 0f;

        private int _numBlowers = 0;
        /// <summary>
        /// Number of blowers augmenting this machine
        /// </summary>
        public int NumBlowers
        {
            get { return _numBlowers; }
        }
        public int NumActiveBlowers
        {
            get
            {
                string facing = this.Block.Variant["side"]; // north,east,south,west
                BlockFacing machinefacing = BlockFacing.FromCode(facing);
                BlockFacing cwface = machinefacing.GetCW(); // left face
                BlockFacing ccwface = machinefacing.GetCCW(); // right face
            }
        }

        public float RecipeProgress
        {
            get
            {
                if (_inventory[0].Empty || _currentRecipe == null) return 0f;
                else
                {
                    return _recipeTime / _totalCraftTime;
                }
            }
        }
        public float CurrentTemp => _currentTemp;
        public bool IsHeating => _remainingBurnTime > 0;
        public RecipeBlastFurnace CurrentRecipe => _currentRecipe;

        public float RemainingFuel => _remainingBurnTime;
        public float FuelTotalBurnTime => _fuelTotalBurnTime;

        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.

            bool noinput = true;
            for (int s = 0; s < 4; s++)
            {
                if (!InputSlots[s].Empty)
                {
                    noinput = false;
                }
            }
            if (noinput)
            {
                _currentRecipe = null;
                _alloyRecipe = null;
                SetState(EnumBEState.Sleeping);
                _recipeTime = 0f;
                return false;
            }

            List<RecipeBlastFurnace> mrecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.BlastFurnaceRecipes;
            if (mrecipes == null) return false;

            foreach (RecipeBlastFurnace mrecipe in mrecipes)
            {
                if (mrecipe.Enabled && mrecipe.Matches(InputSlots, this))
                {
                    _currentRecipe = mrecipe;
                    _totalCraftTime = InputSlot.Empty ? 0f : (InputSlot.Itemstack.StackSize * mrecipe.CraftTimePerItem) * 0.9f;
                    // entire stack is crafted at once, adding items mid-burn cools all the other items like how smelting metal works.
                    SetState(EnumBEState.On);
                    return true;
                }
            }
            _recipeTime = 0;
            SetState(EnumBEState.Sleeping);
            return false;
        }

        #endregion

        public BlockEntityAnimationUtil AnimUtil
        {
            get
            {
                return this.GetBehavior<BEBehaviorAnimatable>()?.animUtil;
            }
        }
        public string DialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-creosote");
            }
        }

        public BEBlastFurnace()
        {
            _inventory = new InvBlastFurnace(null, null);
            _inventory.SlotModified += SlotModified;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //base.GetBlockInfo(forPlayer, dsc); // we do NOT need power information as this machine isn't powered.
            dsc.AppendLine($"{MachineState}");
            if (MachineState == EnumBEState.On && CurrentRecipe != null)
            {
                dsc.AppendLine($"|{Lang.Get("vinteng:gui-word-crafting")}: {CurrentRecipe.Outputs[0].ResolvedItemstack.GetName()}");
            }
            dsc.AppendLine().AppendLine($"{Lang.Get("vinteng:gui-word-temp")}{_currentTemp:N1}°C");
            dsc.AppendLine().AppendLine($"{Lang.Get("vinteng:gui-word-fuel")} {Lang.Get("vinteng:gui-word-remaining")}:{_remainingBurnTime:N1} {Lang.Get("vinteng:gui-word-seconds")}");
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
                _heatPerSecondBase = base.Block.Attributes["heatpersecond"].AsInt(0);
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("veblastfurnace", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            _inventory.Pos = this.Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            FindMatchingRecipe();
        }

        public void OnSimTick(float dt)
        {
            //if (this.Api.Side != EnumAppSide.Server) return;

            //_environmentTempDelay += dt;
            //if (_environmentTempDelay > 300)
            //{
            //    environment Temp update every 5 minutes
            //   _environmentTemp = Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
            //       Api.World.Calendar.TotalDays).Temperature;
            //    _environmentTempDelay = 0f;
            //}

            //if (_state == EnumBEState.Sleeping)
            //{
            //    _updateBouncer += dt;
            //    if (_updateBouncer > 2f)
            //    {
            //        _currentTemp = ChangeTemperature(_currentTemp, _environmentTemp, dt);
            //        if (!InputSlot.Empty)
            //        {
            //            InputSlot.Itemstack.Collectible.SetTemperature(Api.World, InputSlot.Itemstack, _currentTemp, true);
            //        }
            //        _updateBouncer = 0f;
            //        MarkDirty(true);
            //    }
            //    else return;
            //}
            //if (_state == EnumBEState.On) // machine is on and actively crafting something
            //{
            //    if (IsCrafting && RecipeProgress < 1f)
            //    {
            //        if (!HasRoomInOutput(0, null)) return;
            //        Heating stuff
            //        BurnTick(dt);
            //        if (!InputSlot.Empty)
            //        {
            //            InputSlot.Itemstack.Collectible.SetTemperature(Api.World, InputSlot.Itemstack, _currentTemp, true);
            //        }
            //        MarkDirty(true); // debugging, need client updates!
            //        if (_currentRecipe.MinTemp > 0)
            //        {
            //            if (_currentTemp < _currentRecipe.MinTemp) return;
            //            else
            //            {
            //                _recipeTime += dt;
            //            }
            //        }
            //    }
            //    else if (RecipeProgress >= 1f)
            //    {
            //        if (_currentRecipe != null)
            //        {
            //            used a CreosoteOven Recipe
            //            _currentRecipe.TryCraftNow(Api, new[] { InputSlot }, OutputSlots);
            //        }

            //        if (!FindMatchingRecipe())
            //        {
            //            SetState(EnumBEState.Sleeping);
            //        }
            //        _recipeTime = 0f;
            //        MarkDirty(true, null);
            //        Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
            //    }
            //}
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    _clientDialog = new GUIBlastFurnace(DialogTitle, Inventory, this.Pos, capi, this);
                    _clientDialog.Update(RecipeProgress, CurrentTemp, CurrentRecipe);
                    return this._clientDialog;
                });
            }
            return true;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            _environmentTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
                Api.World.Calendar.TotalDays).Temperature;
            _currentTemp = _environmentTemp;
        }
        public int GetRotation()
        {
            string side = Block.Variant["side"];
            // The BlockFacing horiztonal index goes counter-clockwise from east. That needs to be converted so that
            // it goes counter-clockwise from north instead.
            int adjustedIndex = ((BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3) & 3;
            return adjustedIndex * 90;
        }
        /// <summary>
        /// Returns an adjusted fromTemp temperature
        /// </summary>
        /// <param name="fromTemp">Starting Temp</param>
        /// <param name="toTemp">Temp Goal</param>
        /// <param name="deltatime">Time Step</param>
        /// <returns>New Temp</returns>
        private float ChangeTemperature(float fromTemp, float toTemp, float deltatime)
        {
            // TODO Detect presence of blowers and adjust temp speed
            float basechange = 0f;
            if (fromTemp < 480) basechange = _heatPerSecondBase * deltatime;
            else
            {
                float diff = Math.Abs(fromTemp - toTemp);
                basechange = deltatime + deltatime * (diff / 6); // 30 seconds to hit 1100
                if (diff < basechange) return toTemp;
            }
            if (fromTemp > toTemp) basechange = -basechange;
            if (Math.Abs(fromTemp - toTemp) < 1f) return toTemp;
            float newtemp = fromTemp + basechange;
            if (newtemp < -273) return toTemp; // something odd happened, can't go below absolute 0.
            return newtemp;
        }

        /// <summary>
        /// Burns current fuel, or starts a new piece of fuel.<br/>
        /// Manages the Temperature
        /// </summary>
        /// <param name="dt">DeltaTime</param>
        public void BurnTick(float dt)
        {
            if (_remainingBurnTime > 0f)
            {
                _remainingBurnTime -= dt;
                _currentTemp = ChangeTemperature(_currentTemp, _tempgoal, dt);
                if (_remainingBurnTime < 0f) _remainingBurnTime = 0f; // next tick will look for more fuel
                return; // we're already burning, don't need a new fuel yet
            }
            CombustibleProperties fuelProps = FuelSlot.Itemstack?.Collectible.CombustibleProps;
            if (fuelProps == null)
            {
                _currentTemp = ChangeTemperature(_currentTemp, _environmentTemp, dt);
                return;
            }

            if (fuelProps.BurnTemperature > 0f && fuelProps.BurnDuration > 0f)
            {
                _remainingBurnTime = fuelProps.BurnDuration;
                _fuelTotalBurnTime = _remainingBurnTime;
                _maxBurnTemp = fuelProps.BurnTemperature;
                _tempgoal = _maxBurnTemp;
                FuelSlot.TakeOut(1); // will invalidate itemstack if its the last one.                
                FuelSlot.MarkDirty();
                MarkDirty(true);
            }
        }
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (_clientDialog != null)
            {
                _clientDialog.TryClose();
                GUIBlastFurnace gUILog = _clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                _clientDialog = null;
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            this._inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetString("machinestate", MachineState.ToString());
            tree.SetFloat("recipetime", _recipeTime);
            tree.SetFloat("envtemp", _environmentTemp);
            tree.SetFloat("burnleft", _remainingBurnTime);
            tree.SetFloat("burntotal", _fuelTotalBurnTime);
            tree.SetFloat("currenttemp", _currentTemp);
            tree.SetFloat("tempgoal", _tempgoal);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _state = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "Sleeping"));
            FindMatchingRecipe();
            _recipeTime = tree.GetFloat("recipetime", 0f);
            _environmentTemp = tree.GetFloat("envtemp", 20f);
            _remainingBurnTime = tree.GetFloat("burnleft", 0f);
            _fuelTotalBurnTime = tree.GetFloat("burntotal", 0f);
            _currentTemp = tree.GetFloat("currenttemp", 0f);
            _tempgoal = tree.GetFloat("tempgoal", 0f);

            if (Api != null && Api.Side == EnumAppSide.Client) SetState(_state);
            if (_clientDialog != null)
            {
                _clientDialog.Update(RecipeProgress, CurrentTemp, CurrentRecipe);
            }
        }
    }
}
