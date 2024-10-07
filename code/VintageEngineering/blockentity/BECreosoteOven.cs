using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Electrical;
using VintageEngineering.inventory;
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
    public class BECreosoteOven : BlockEntityOpenableContainer, IVELiquidInterface
    {

        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private GUICreosoteOven _clientDialog;

        #region InventoryStuff
        public virtual bool AllowPipeLiquidTransfer
        {
            get
            {
                if (base.Block.Attributes == null) return false;
                return base.Block.Attributes["allowPipeLiquidTransfer"].AsBool(false);
            }
        }

        public virtual bool AllowHeldLiquidTransfer
        {
            get
            {
                if (base.Block.Attributes == null) return false;
                return base.Block.Attributes["allowHeldLiquidTransfer"].AsBool(false);
            }
        }

        public virtual float TransferSizeLitresPerSecond
        {
            get
            {
                if (base.Block.Attributes == null) return 0f;
                return base.Block.Attributes["transferLitresPerSecond"].AsFloat(0.01f);
            }
        }
        private InvCreosoteOven _inventory;
        public override string InventoryClassName { get { return "InvCreosoteOven"; } }
        /// <summary>
        /// SlotID: 0 = input, 1 = fuel, 2 = output item, 3 = output liquid
        /// </summary>
        public override InventoryBase Inventory { get { return _inventory; } }

        public ItemSlot InputSlot => _inventory[0];
        public ItemSlot FuelSlot => _inventory[1];

        public ItemSlot[] OutputSlots
        {
            get
            {
                return new ItemSlot[]
                {
                    _inventory[2],
                    _inventory[3] as ItemSlotLiquidOnly
                };
            }
        }

        public int[] InputLiquidContainerSlotIDs => Array.Empty<int>();
        public int[] OutputLiquidContainerSlotIDs => new int[] { 3 };

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            foreach (int slot in OutputLiquidContainerSlotIDs)
            {
                if (!Inventory[slot].Empty) return Inventory[slot] as ItemSlotLiquidOnly;
            }
            return null;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot)
        {           
            return null;
        }
        private void SlotModified(int slotid)
        {
            if (slotid == 0 || slotid == 1) // input or fuel slot update
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
                if (_inventory[2].Empty && _inventory[3].Empty) return true;
                int itemout = 0;
                int fluidout = 0;
                if (!_inventory[2].Empty)
                {
                    itemout = _inventory[2].Itemstack.Collectible.MaxStackSize - _inventory[2].Itemstack.StackSize;
                }
                else itemout = 64;

                if (!_inventory[3].Empty)
                {
                    WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(_inventory[3].Itemstack);
                    if (wprops == null) fluidout = 0;
                    fluidout = (int)((_inventory[3] as ItemSlotLiquidOnly).CapacityLitres - (_inventory[3].Itemstack.StackSize / wprops.ItemsPerLitre));
                }
                else fluidout = 50;

                return itemout > 0 && fluidout > 0;
            }
            else
            {
                if (slotid < 2 || slotid > 3) return false; // not an output slot
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
        private RecipeCreosoteOven _currentRecipe;
        private float _updateBouncer = 0f;
        private int _heatPerSecondBase = 1;
        private float _environmentTemp = 20f;
        private float _remainingBurnTime = 0f;

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
        public bool IsHeating => IsCrafting;
        public RecipeCreosoteOven CurrentRecipe => _currentRecipe;

        public float RemainingFuel => _remainingBurnTime;

        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.
   
            if (InputSlot.Empty)
            {
                _currentRecipe = null;
                SetState(EnumBEState.Sleeping);
                _recipeTime = 0f;                
                return false;
            }

            List<RecipeCreosoteOven> mrecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.CreosoteOvenRecipes;
            if (mrecipes == null) return false;

            foreach (RecipeCreosoteOven mrecipe in mrecipes)
            {
                if (mrecipe.Enabled && mrecipe.Matches(InputSlot, null))
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

        public BECreosoteOven()
        {
            _inventory = new InvCreosoteOven(null, null);
            _inventory.SlotModified += SlotModified;
        } 

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
                _heatPerSecondBase = base.Block.Attributes["heatpersecond"].AsInt(0);
                _environmentTemp = Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
                    Api.World.Calendar.TotalDays).Temperature;
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vecreosoteoven", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            _inventory.Pos = this.Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            FindMatchingRecipe();
        }

        public void OnSimTick(float dt)
        {
            if (this.Api.Side != EnumAppSide.Server) return;
            if (_state == EnumBEState.Sleeping)
            {
                _updateBouncer += dt;
                if (_updateBouncer > 2f)
                {
                    _environmentTemp = Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
                        Api.World.Calendar.TotalDays).Temperature;
                    _currentTemp = ChangeTemperature(_currentTemp, _tempgoal, _updateBouncer);
                    if (!InputSlot.Empty)
                    {
                        InputSlot.Itemstack.Collectible.SetTemperature(Api.World, InputSlot.Itemstack, _currentTemp, true);
                    }
                    _updateBouncer = 0f; 
                }
                else return;
            }
            if (_state == EnumBEState.On) // machine is on and actively crafting something
            {
                if (IsCrafting && RecipeProgress < 1f)
                {
                    if (!HasRoomInOutput(0, null)) return;
                    // Heating stuff
                    BurnTick(dt);
                    if (!InputSlot.Empty)
                    {
                        InputSlot.Itemstack.Collectible.SetTemperature(Api.World, InputSlot.Itemstack, _currentTemp, true);
                    }
                    if (_currentRecipe.MinTemp > 0)
                    {
                        if (_currentTemp < _currentRecipe.MinTemp) return;
                        else
                        {
                            _recipeTime += dt;
                        }
                    }
                }
                else if (RecipeProgress >= 1f)
                {
                    if (_currentRecipe != null)
                    {
                        // used a CreosoteOven Recipe
                        _currentRecipe.TryCraftNow(Api, new[] { InputSlot }, OutputSlots);
                    }

                    if (!FindMatchingRecipe())
                    {
                        SetState(EnumBEState.Sleeping);
                    }
                    _recipeTime = 0f;
                    MarkDirty(true, null);
                    Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
                }
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    _clientDialog = new GUICreosoteOven(DialogTitle, Inventory, this.Pos, capi, this);
                    _clientDialog.Update(RecipeProgress, CurrentTemp, CurrentRecipe);
                    return this._clientDialog;
                });
            }
            return true;
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
            float basechange = 0f;
            if (fromTemp < 180) basechange = _heatPerSecondBase * deltatime;
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
            if (_remainingBurnTime > 0) 
            {
                _remainingBurnTime -= dt;
                _currentTemp = ChangeTemperature(_currentTemp, _tempgoal, dt);
                if (_remainingBurnTime < 0) _remainingBurnTime = 0f;
                return; // we're already burning, don't need a new fuel yet
            }
            CombustibleProperties fuelProps = FuelSlot.Itemstack?.Collectible.CombustibleProps;
            if (fuelProps == null) 
            {
                _environmentTemp = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly,
                    Api.World.Calendar.TotalDays).Temperature;
                _tempgoal = _environmentTemp;
                _currentTemp = ChangeTemperature(_currentTemp, _tempgoal, dt);
                return; 
            }

            if (fuelProps.BurnTemperature > 0f && fuelProps.BurnDuration > 0f)
            {
                _remainingBurnTime = fuelProps.BurnDuration;
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
                GUICreosoteOven gUILog = _clientDialog;
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
