using System;
using System.Collections.Generic;
using System.Text;
using VintageEngineering.Electrical;
using VintageEngineering.Renderers;
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
    public class BEForge : ElectricContainerBE
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private float updateBouncer = 0f;
        private GUIForge clientDialog;

        private VEForgeItemRenderer _itemRenderer;

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
                capi.Event.RegisterRenderer(_itemRenderer = new VEForgeItemRenderer(base.Block, Pos, capi, new Vec3f()) , EnumRenderStage.Opaque, "veforge");
                _itemRenderer.SetContents(InputSlot.Itemstack, true);
                api.Event.RegisterEventBusListener(new EventBusListenerDelegate(OnEventBusEvent), 200, "genjsontransform");
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("veforge", null, null, new Vec3f(0, Electric.GetRotation(), 0f));
                }
            }
            inv.Pos = this.Pos;
            inv.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!inv[0].Empty) FindMatchingRecipe();
        }

        private void OnEventBusEvent(string eventName, ref EnumHandling handling, IAttribute data)
        {
            _itemRenderer.RegenMesh();
        }

        #region RecipeAndInventoryStuff
        private InvForge inv;
        //private RecipeForge currentRecipe;
        private CombustibleProperties _cproperties;
        private int _currentTempGoal;
        /// <summary>
        /// If true, the Forge is heating the machine above it, not an item in its inventory.
        /// </summary>
        private bool heatingBlock = false;
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

        /// <summary>
        /// If true, Forge is heating the machine above it.
        /// </summary>
        public bool HeatingBlock { get { return heatingBlock; } }

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
            if (slotid == 0 && !heatingBlock)
            {
                // something changed with the input slot
                //if (_itemRenderer != null) _itemRenderer.SetContents(inv[0].Itemstack, true);                
                FindMatchingRecipe();

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, Electric.CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
                }
            }

            MarkDirty(true, null);
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
            if (Electric.MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                isHeating = false;
                _cproperties = null;
                return false;
            }
            if (heatingBlock)
            {
                isHeating = true;
                SetState(EnumBEState.On);
                return true;
            }
            if (InputSlot.Empty)
            {
                isHeating = false;
                SetState(EnumBEState.Sleeping);
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
                        int workable = 0;
                        if (InputSlot.Itemstack.Collectible.Attributes != null
                            && InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].Exists)
                        {
                            workable = InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].AsInt();
                            _currentTempGoal = workable;
                            if (CurrentTemp < workable)
                            {
                                isHeating = true;
                                SetState(EnumBEState.On);
                                return true;
                            }
                        }
                        else // if (CurrentTemp < (_cproperties.MeltingPoint / 2) + 50) // an extra 50 degrees
                        {
                            _currentTempGoal = (_cproperties.MeltingPoint / 2) + 50;
                            SetState(EnumBEState.On);
                            isHeating = true;
                            return true;
                        }
                    }
                    else
                    {
                        _currentTempGoal = tempGoal;
                        isHeating = true;
                        SetState(EnumBEState.On);
                        return true;
                    }
                }
                _cproperties = null; // a baking thing or not enough items, ignore the combustable props
            }
            if (tempGoal == 0 && _cproperties == null) // no props and we're in Auto mode, lets check some attributes
            {
                if (InputSlot.Itemstack.Collectible.Attributes != null
                    && InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].Exists)
                {
                    int workable = InputSlot.Itemstack.Collectible.Attributes["workableTemperature"].AsInt();
                    workable += 50;
                    if (CurrentTemp < workable)
                    {
                        _currentTempGoal = workable;
                        isHeating = true;
                        SetState(EnumBEState.On);
                        return true;
                    }
                }
                else
                {
                    // no Combustable props, auto temp, no workabletemp attributes...
                    // whatever we have it can't be heated in this mode.
                    _currentTempGoal = 0;
                    isHeating = false;
                    SetState(EnumBEState.Sleeping);
                    return false;
                }
            }
            else //if (CurrentTemp < tempGoal) // no combustable props, lets use tempGoal
            {
                _currentTempGoal = tempGoal;
                isHeating = true;
                SetState(EnumBEState.On);
                return true;
            }
            _currentTempGoal = 0;
            isHeating = false;
            SetState(EnumBEState.Sleeping);
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
            float TARGET_CLAMP = 1100f;
            float effectiveTarget = toTemp;
            bool isHeating = fromTemp < toTemp;
            bool isHighGoal = toTemp > TARGET_CLAMP;
            if (isHeating && isHighGoal && fromTemp < TARGET_CLAMP) effectiveTarget = TARGET_CLAMP;

            float basechange = 0f;
            if (fromTemp < 600) basechange = HeatPerSecondBase * deltatime;
            else
            {
                float diff = Math.Abs(fromTemp - effectiveTarget);
                basechange = deltatime + deltatime * (diff / 6); 
                if (diff < basechange) return effectiveTarget;
            }
            if (fromTemp > effectiveTarget) basechange = -basechange;
            if (Math.Abs(fromTemp - effectiveTarget) < 1f) return effectiveTarget;

            float newtemp = fromTemp + basechange;
            if (newtemp < -273) return effectiveTarget; // something odd happened, can't go below absolute 0.
            return newtemp;
        }
        #endregion

        public void OnSimTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return; // only tick on the server
            environmentTempDelay += dt;
            _clientUpdateDelay += dt;
            if (environmentTempDelay > 300) // a weather pull every 5 minutes seems reasonable
            {
                environmentTemp = Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.NowValues).Temperature;
                environmentTempDelay = 0f;
            }

            if (Electric.IsSleeping || Electric.MachineState == EnumBEState.Paused)
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

            if (Electric.MachineState == EnumBEState.On) // machine is on and actively crafting something
            {
                float powerpertick = Electric.MaxPPS * dt;
                if (Electric.CurrentPower == 0 || Electric.CurrentPower < powerpertick) { return; } // power is low!
                if (!OutputSlot.Empty) { return; } // something is in the output slot
                if (isHeating) // we're heating
                {
                    if (heatingBlock)
                    {
                        IHeatable heatable = Api.World.BlockAccessor.GetBlockEntity(this.Pos.UpCopy(1)) as IHeatable;
                        float desired = heatable.GetDesiredTemperature();
                        desired *= 1.1f; // bump desired temp 10%
                        float basintemp = heatable.GetTemperature();
                        if (desired > 0f)
                        {
                            heatable.SetTemperature(ChangeTemperature(basintemp, desired, dt));
                        }
                        else
                        {
                            if (basintemp != 20) heatable.SetTemperature(ChangeTemperature(basintemp, 20, dt));                            
                                                        
                            if (_clientUpdateDelay < 1) 
                            {                                     
                                return; 
                            }
                            else
                            {
                                MarkDirty(true);
                                return;
                            }
                            
                        }
                    }
                    else if (!InputSlot.Empty)
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
                    Electric.electricpower -= (ulong)Math.Round(powerpertick); // consume power when heating up
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
                }
            }            
            if (_clientUpdateDelay > 0.5f)
            {
                _clientUpdateDelay = 0f;
                MarkDirty(true);
            }            
        }

        /// <summary>
        /// Sets whether or not this Forge is supposed to heat a machine above it.
        /// </summary>
        /// <param name="heatableblock">True to heat block above this.</param>
        public void SetHeatableBlock(bool heatableblock)
        {
            this.heatingBlock = heatableblock;
            FindMatchingRecipe();
        }

        protected virtual void SetState(EnumBEState newstate)
        {
            //if (MachineState == newstate) return; // no change, nothing to see here.
            Electric.MachineState = newstate;

            if (Electric.MachineState == EnumBEState.On)
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
                clientDialog.Update(RecipeProgress, Electric.CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
            }
            MarkDirty(true);
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot active = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (byPlayer.Entity.Controls.Sneak)
            {
                if (InputSlot.Empty && OutputSlot.Empty) // PUT item into Forge
                {
                    if (!active.Empty)
                    {
                        if (active.TryPutInto(Api.World, inv[0], 1) == 1)
                        {
                            capi?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                            _itemRenderer?.SetContents(InputSlot.Itemstack, true);
                            MarkDirty();
                            return true;
                        }
                    }
                }
                else if (!OutputSlot.Empty)
                {
                    if (active.Empty)
                    {
                        ItemStack workitem = OutputSlot.Itemstack.Clone();
                        OutputSlot.TakeOut(1);
                        if (byPlayer.InventoryManager.TryGiveItemstack(workitem, false))
                        {
                            Api.ModLoader.GetModSystem<ModSystemSubTongsDurability>()?.OnItemPickedUp(byPlayer.Entity, workitem);
                        }
                        else
                        {
                            Api.World.SpawnItemEntity(workitem, Pos);
                        }
                        _itemRenderer?.SetContents(InputSlot.Itemstack, true);
                        return true;
                    }                    
                }
                else if (!InputSlot.Empty && OutputSlot.Empty)
                {
                    if (active.Empty)
                    {
                        ItemStack workitem = InputSlot.Itemstack.Clone();
                        InputSlot.TakeOut(1);
                        if (byPlayer.InventoryManager.TryGiveItemstack(workitem, false))
                        {
                            Api.ModLoader.GetModSystem<ModSystemSubTongsDurability>()?.OnItemPickedUp(byPlayer.Entity, workitem);
                        }
                        else
                        {
                            Api.World.SpawnItemEntity(workitem, Pos);
                        }
                        _itemRenderer?.SetContents(InputSlot.Itemstack, true);
                        return true;
                    }                    
                }                
            }
            else if (this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    clientDialog = new GUIForge(DialogTitle, Inventory, this.Pos, capi, this);
                    clientDialog.Update(RecipeProgress, Electric.CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            this.Dispose();

            if (clientDialog != null)
            {
                clientDialog.TryClose();
                GUIForge gUILog = clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                clientDialog = null;
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            this.Dispose();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.Append(isHeating ? $"{Lang.Get("vinteng:gui-word-heating")}: " : "");
            dsc.AppendLine($"{CurrentTemp:N1}°");
        }

        #region ServerClientStuff
        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable Button
            {
                if (Electric.IsEnabled) SetState(EnumBEState.Off); // turn off
                else
                {
                    SetState((IsCrafting || IsHeating) ? EnumBEState.On : EnumBEState.Sleeping);
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
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(RecipeProgress, Electric.CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
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
            tree.SetBool("heatingblock", heatingBlock);
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
            heatingBlock = tree.GetBool("heatingblock", false);
            environmentTemp = tree.GetFloat("worldtemp", 20);
            float currentItemTemp = tree.GetFloat("currenttemp");
            FindMatchingRecipe();
            if (!InputSlot.Empty && Api != null)
            {
                if (capi != null && _itemRenderer != null) _itemRenderer.SetContents(InputSlot.Itemstack, true);
                InputSlot.Itemstack.Collectible.SetTemperature(worldForResolving,
                    InputSlot.Itemstack, currentItemTemp, true);
            }
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                if (InputSlot.Empty) _itemRenderer?.SetContents(OutputSlot.Itemstack, true);
                else _itemRenderer?.SetContents(InputSlot.Itemstack, true);
            }            
            if (Api != null && Api.Side == EnumAppSide.Client) { SetState(Electric.MachineState); }
            if (clientDialog != null)
            {
                clientDialog.Update(RecipeProgress, Electric.CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
            }
        }

        #endregion
    }
}
