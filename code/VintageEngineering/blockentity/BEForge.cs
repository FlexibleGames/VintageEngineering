using System;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageEngineering
{
    public class BEForge : ElectricBE
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private float updateBouncer = 0f;
        private GUIForge clientDialog;

        public string DialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-forge");
            }
        }

        public BEForge()
        {
            inv = new InvForge(null, null);
            inv.SlotModified += OnSlotModified;
        }

        public override bool CanExtractPower => false;
        public override bool CanReceivePower => true;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
                HeatPerSecondBase = base.Block.Attributes["heatpersecond"].AsInt(0);
                if (environmentTemp == 0f)
                {
                    environmentTemp = api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature;
                }
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("veforge", null, null, new Vec3f(0, GetRotation(), 0f));
                }
            }
            inv.Pos = this.Pos;
            inv.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!inv[0].Empty) FindMatchingRecipe();
        }

        #region RecipeAndInventoryStuff
        private InvForge inv;
        //private RecipeForge currentRecipe;
        private CombustibleProperties _cproperties;
        private int _currentTempGoal;
        
        //private float _burntimeelapsed;
        //private ulong recipePowerApplied;
        private bool isHeating = false;
        //private bool isCrafting = false;
        private int HeatPerSecondBase;
        //private float currentTemp;
        internal int tempGoal; // this will be set in the gui
        public float environmentTemp;
        private float environmentTempDelay = 0f;
        public float CurrentTemp 
        { 
            get 
            {
                if (InputSlot.Empty) return 0;
                // will return 20 if temp attribute does not exist.
                return InputSlot.Itemstack.Collectible.GetTemperature(Api.World, InputSlot.Itemstack);
            } 
        }
        public float RecipeProgress
        {
            get
            {
                if (_currentTempGoal == 0)
                {
                    return 0f;
                }
                return (float)CurrentTemp / (float)_currentTempGoal;
            }
        }
        /// <summary>
        /// If we're crafting we're always heating, even when we're at the right temp
        /// </summary>
        public bool IsCrafting { get { return isHeating; } }
        /// <summary>
        /// If we're heating we may not be crafting (yet)
        /// </summary>
        public bool IsHeating { get { return isHeating; } }

        public ItemSlot InputSlot { get { return inv[0]; } }
        public ItemSlot OutputSlot { get { return inv[1]; } }

        /// <summary>
        /// Slotid's 1 is the OutputSlot
        /// </summary>
        /// <param name="slotid">1</param>
        /// <returns>ItemSlot</returns>
        public ItemSlot OutputSlots(int slotid)
        {
            if (slotid < 1 || slotid > 1) return null;
            return inv[slotid];
        }

        public override string InventoryClassName { get { return "InvForge"; } }

        public override InventoryBase Inventory { get { return inv; } }

        public void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                // something changed with the input slot
                FindMatchingRecipe();
                MarkDirty(true, null);

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
                }
            }
        }

        /// <summary>
        /// Output slots IDs are slotid 1<br/>
        /// Pass in slotid = 0 and forStack = null to return if ANY slot has room.
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0 && forStack == null)
            {
                if (inv[1].Empty) return true; // both slots are stacksize of 1 ONLY
                return false;
            }
            if (slotid < 1 || slotid > 1) return false; // not output slots
            if (inv[slotid].Empty) return true;
            return false;
        }

        /// <summary>
        /// If the input slot contains something we can heat, then return true.<br/>
        /// If a temp goal was set in GUI, will try to heat ANYTHING up to that temp.
        /// </summary>
        /// <returns>True if item can be heated.</returns>
        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.            
            if (MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                isHeating = false;
                return false;
            }
            if (InputSlot.Empty)
            {
                isHeating = false;
//                isCrafting = false;
                StateChange(EnumBEState.Sleeping);
                return false;
            }
            isHeating = false;
            _currentTempGoal = 0;
            _cproperties = InputSlot.Itemstack.Collectible.CombustibleProps;
            if (_cproperties != null && _cproperties.SmeltingType != EnumSmeltType.Cook)
            {
                BakingProperties bakingProperties = BakingProperties.ReadFrom(InputSlot.Itemstack);

                if (bakingProperties == null)
                {
                    if (tempGoal == 0) // if goal = 0, then it's in Auto mode
                    {
                        if (InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].Exists)
                        {
                            int workable = InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].AsInt();
                            _currentTempGoal = workable;
                            if (CurrentTemp < workable)
                            {
                                isHeating = true;
                                StateChange(EnumBEState.On);
                                return true;
                            }
                        }
                        else // if (CurrentTemp < (_cproperties.MeltingPoint / 2) + 50) // an extra 50 degrees
                        {
                            _currentTempGoal = (_cproperties.MeltingPoint / 2) + 50;
                            StateChange(EnumBEState.On);
                            isHeating = true;
                            return true;
                        }
                    }
                    else
                    {
                        _currentTempGoal = tempGoal;
                        isHeating = true;
                        StateChange(EnumBEState.On);
                        return true;
                    }
                }
                _cproperties = null; // a baking thing or not enough items, ignore the combustable props
            }
            if (tempGoal == 0 && _cproperties == null) // no props and we're in Auto mode, lets check some attributes
            {
                if (InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].Exists)
                {
                    int workable = InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].AsInt();
                    workable += 50;
                    if (CurrentTemp < workable)
                    {
                        _currentTempGoal = workable;
                        isHeating = true;
                        StateChange(EnumBEState.On);
                        return true;
                    }
                }
                else
                {
                    // no Combustable props, auto temp, no workabletemp attributes... 
                    // whatever we have it can't be heated in this mode.
                    _currentTempGoal = 0;
                    isHeating = false;
                    StateChange(EnumBEState.Sleeping);
                    return false;
                }
            }
            else //if (CurrentTemp < tempGoal) // no combustable props, lets use tempGoal
            {
                _currentTempGoal = tempGoal;
                isHeating = true;
                StateChange(EnumBEState.On);
                return true;
            }
            _currentTempGoal = 0;
            isHeating = false;            
            StateChange(EnumBEState.Sleeping);
            return false;
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
            if (fromTemp < 350) basechange = HeatPerSecondBase * deltatime;
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


        //private float ChangeTemperature(float fromTemp, float toTemp, float deltatime)
        //{
        //    float basechange = 0f;
        //    if (fromTemp <= 600) basechange = HeatPerSecondBase;
        //    else basechange = (1 / (fromTemp - fromTemp / 2)) * 30000; // base change per second
        //    basechange = Math.Min(basechange, HeatPerSecondBase); // ensure change is <= HeatPerSecondBase
        //    float tickchange = basechange * deltatime;
        //    if (fromTemp > toTemp) tickchange = -tickchange;
        //    if (Math.Abs(fromTemp - toTemp) < 1f) return toTemp; // if it's within a degree, return totemp
        //    return fromTemp + tickchange;
        //}

        #endregion

        public void OnSimTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return; // only tick on the server
            environmentTempDelay += dt;
            if (environmentTempDelay > 300) // a weather pull every 5 minutes seems reasonable
            {
                environmentTemp = Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature;
                environmentTempDelay = 0f;
            }

            if (IsSleeping || MachineState == EnumBEState.Paused)
            {
                // A sleeping machine runs this routine every 2 seconds instead of 10 times a second.
                updateBouncer += dt;
                if (!InputSlot.Empty)
                {
                    if (InputSlot.Itemstack.Collectible.HasTemperature(InputSlot.Itemstack))
                    {
                        InputSlot.Itemstack.Collectible.SetTemperature(Api.World, 
                                        InputSlot.Itemstack,
                                        ChangeTemperature(CurrentTemp, environmentTemp, dt),
                                        true);
                    }
                }
                if (updateBouncer < 2f) return;
                updateBouncer = 0f;
            }
            if (MachineState == EnumBEState.On) // machine is on and actively crafting something
            {
                float powerpertick = MaxPPS * dt;
                if (CurrentPower == 0 || CurrentPower < powerpertick) { return; } // power is low!
                if (!OutputSlot.Empty) { return; } // something is in the output slot
                if (isHeating) // we're heating
                {
                    if (!InputSlot.Empty)
                    {
                        if (InputSlot.Itemstack.Collectible.GetTemperature(Api.World, InputSlot.Itemstack) < _currentTempGoal)
                        {
                            InputSlot.Itemstack.Collectible.SetTemperature(Api.World,
                                InputSlot.Itemstack,
                                ChangeTemperature(CurrentTemp, _currentTempGoal, dt), true);
                        }
                    }
                    else
                    { 
                        FindMatchingRecipe(); // how'd this happen?
                        return;
                    }
                    electricpower -= (ulong)Math.Round(powerpertick); // consume power when heating up
                }

                if (RecipeProgress >= 1f) // target temp has been achieved!
                {
                    if (OutputSlot.Empty)
                    {
                        if (!InputSlot.TryFlipWith(OutputSlot))
                        {
                            return;
                        }
                    }
                    else return; // this shouldn't ever fire... but just in case
                }
            }
            this.MarkDirty(true, null);
        }

        public override void StateChange(EnumBEState newstate)
        {
            //if (MachineState == newstate) return; // no change, nothing to see here.            
            MachineState = newstate;

            if (MachineState == EnumBEState.On)
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
            if (Api != null && Api.Side == EnumAppSide.Client && clientDialog != null && clientDialog.IsOpened())
            {
                clientDialog.Update(RecipeProgress, CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
            }
            MarkDirty(true);
        }


        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    clientDialog = new GUIForge(DialogTitle, Inventory, this.Pos, capi, this);
                    clientDialog.Update(RecipeProgress, CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (clientDialog != null)
            {
                clientDialog.TryClose();
                GUIForge gUILog = clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                clientDialog = null;
            }
        }

        public override string GetMachineHUDText()
        {
            string outtext = base.GetMachineHUDText() + System.Environment.NewLine;

            float recipeProgressPercent = RecipeProgress * 100;

//            string crafting = isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}";
            string heating = isHeating ? $"{Lang.Get("vinteng:gui-word-heating")}: " : "";
            heating += $"{CurrentTemp:N1}°";

            return outtext + Environment.NewLine + heating;
        }

        #region ServerClientStuff
        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable Button
            {
                if (IsEnabled) StateChange(EnumBEState.Off); // turn off
                else
                {
                    StateChange((IsCrafting || IsHeating) ? EnumBEState.On : EnumBEState.Sleeping);
                }
                MarkDirty(true, null);
            }
            if (packetid == 1004)
            {
                int newTemp = SerializerUtil.Deserialize<int>(data);
                tempGoal = newTemp; // 25 degree steps...
                FindMatchingRecipe();
                MarkDirty(true);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(RecipeProgress, CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inv.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetInt("currenttempgoal", _currentTempGoal);
            tree.SetInt("tempgoal", tempGoal);
//            tree.SetBool("iscrafting", isCrafting);
            tree.SetBool("isheating", isHeating);
            tree.SetFloat("currenttemp", CurrentTemp); // this is the INPUTSTACK's current temp
            tree.SetFloat("worldtemp", environmentTemp);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));            
            if (Api != null) inv.AfterBlocksLoaded(Api.World);
            _currentTempGoal = tree.GetInt("currenttempgoal");
            tempGoal = tree.GetInt("tempgoal");
            isHeating = tree.GetBool("isheating", false);            
            environmentTemp = tree.GetFloat("worldtemp", 20);
            float currentItemTemp = tree.GetFloat("currenttemp");
            if (!inv[0].Empty) FindMatchingRecipe();
            if (!InputSlot.Empty)
            {
                InputSlot.Itemstack.Collectible.SetTemperature(worldForResolving,
                    InputSlot.Itemstack, currentItemTemp, true);
            }

            if (Api != null && Api.Side == EnumAppSide.Client) { StateChange(MachineState); }
            if (clientDialog != null)
            {
                clientDialog.Update(RecipeProgress, CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
            }
        }

        #endregion
    }
}
