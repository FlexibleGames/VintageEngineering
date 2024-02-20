using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;

namespace VintageEngineering
{
    public class BETestGen : BlockEntityOpenableContainer
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private TestGenInventory inventory;
        private TestGenGUI clientDialog;
                
        private float tempToGen = 100;
        private float prevGenTemp = 20f;
        private float genTemp = 20f;
        private int powerPerSecond = 10;
        private int maxTemp;
        private float fuelBurnTime;
        private float maxBurnTime;
        private float updateBouncer = 0;

        /// <summary>
        /// N E S W
        /// </summary>
        private bool[] faceHasMachine = new bool[4];

        

        public float FuelBurnTime { get { return fuelBurnTime; } }
        public float GenTemp { get { return genTemp; } }
        public bool IsGenerating
        {
            get
            {
                return (fuelBurnTime > 0f && currentPower < maxPower);
            }
        }
        public bool IsBurning
        {
            get
            {
                return fuelBurnTime > 0f;
            }
        }

        private ItemSlot FuelSlot
        {
            get
            {
                return this.inventory[0];
            }
        }
        private ItemStack FuelStack
        {
            get
            {
                return this.inventory[0].Itemstack;
            }
            set
            {
                this.inventory[0].Itemstack = value;
                this.inventory[0].MarkDirty();
            }
        }

        // Power Stuff, will be an Interface soon
        #region PowerStuff
        private ulong maxPower = 2048;
        private ulong currentPower = 0;
        // ElectricalPowerTier Enum LV
        // ElectricalEntityType Enum Producer
        public ulong MaxPower { get { return maxPower; } }
        public ulong CurrentPower { get { return currentPower; } }        
        public bool CanReceivePower { get { return false; } }
        public bool CanExtractPower { get { return true; } }
        public bool IsPowerFull { get { return currentPower == maxPower; } }
        public ulong ReceivePower(ulong powerOffered)
        {
            powerOffered -= 0;
            return powerOffered;
        }
        public ulong ExtractPower(ulong powerNeeded)
        {
            ulong powerGiven = 0;
            if (powerNeeded >= currentPower)
            {
                powerGiven = currentPower;                
                currentPower = 0;
            }
            else
            {
                powerGiven = powerNeeded;
                currentPower -= powerNeeded;
            }
            return powerGiven;
        }
        // end power stuff
#endregion

        public override InventoryBase Inventory
        {
            get
            {
                return inventory;
            }
        }

        public string DialogTitle
        {
            get
            {
                return "Test Generator";
            }
        }

        public override string InventoryClassName { get { return "TestGenInventory"; } }

        public BETestGen()
        {
            this.inventory = new TestGenInventory(null, null);
            this.inventory.SlotModified += OnSlotModified;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(null);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (this.clientDialog != null)
            {
                this.clientDialog.TryClose();
                TestGenGUI testGenGUI = this.clientDialog;
                if (testGenGUI != null) testGenGUI.Dispose();
                this.clientDialog = null;
            }
        }

        public void OnSlotModified(int slotId)
        {
            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            if (this.Api is ICoreClientAPI && this.clientDialog != null)
            {
                clientDialog.Update(genTemp, fuelBurnTime, currentPower);
                //this.SetDialogValues(this.clientDialog.Attributes);
            }
            IWorldChunk chunkatPos = this.Api.World.BlockAccessor.GetChunkAtBlockPos(this.Pos);
            if (chunkatPos == null) return;
            chunkatPos.MarkModified();
        }

        public string GetOutputText()
        {
            return $"{GenTemp:N1}°C {FuelBurnTime:N1} seconds | {CurrentPower:N0}/{MaxPower:N0} Power";
        }

        private void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("gentemperature", this.genTemp);
            dialogTree.SetFloat("fuelburntime", this.fuelBurnTime);
            dialogTree.SetLong("maxpower", (long)this.MaxPower);
            dialogTree.SetLong("currentpower", (long)this.currentPower);
        }

        public void OnBurnTick(float deltatime)
        {
            if (this.Api is ICoreServerAPI)
            {
                updateBouncer += deltatime;
                if (IsGenerating) // we have fuel and space for power
                {
                    this.fuelBurnTime -= deltatime;
                    if (this.fuelBurnTime <= 0f)
                    {
                        fuelBurnTime = 0f;
                        maxBurnTime = 0f; 
                    }
                }
                if (IsGenerating) // we have fuel and space for power, duplicate check in case previous check ran us dry.
                {
                    genTemp = ChangeTemperature(genTemp, maxTemp, deltatime);
                    if (genTemp >= tempToGen)
                    {
                        currentPower += (ulong)(deltatime * powerPerSecond);
                        if (currentPower > maxPower) currentPower = maxPower;
                    }
                }
                if (!IsGenerating) // either fuel is out or power is full
                {
                    genTemp = ChangeTemperature(genTemp, 20, deltatime);
                }
                if (!IsPowerFull && !IsBurning) // space for power AND not burning anything
                {
                    CanDoBurn();
                }
                if (updateBouncer >= 0.5f)
                {
                    prevGenTemp = genTemp;
                    if (faceHasMachine[0] || faceHasMachine[1] || faceHasMachine[2] || faceHasMachine[3]) CanGivePower();
                    MarkDirty(false, null);
                    updateBouncer = 0f;
                }
            }
        }

        public void CanDoBurn()
        {
            CombustibleProperties fuelProps = FuelSlot.Itemstack?.Collectible.CombustibleProps;
            if (fuelProps == null) return;
            if (fuelProps.BurnTemperature > 0f && fuelProps.BurnDuration > 0f)
            {
                maxBurnTime = fuelBurnTime = fuelProps.BurnDuration;
                maxTemp = fuelProps.BurnTemperature;                
                FuelStack.StackSize--;
                if (FuelStack.StackSize <= 0)
                {
                    FuelStack = null;
                }
                MarkDirty();
            }
        }

        public void CanGivePower()
        {
            // a temporary routine to push power into a machine, will be an electric network eventually
            BETestMachine beTestMachine;
            for (int x = 0; x < 4; x++)
            {
                if (currentPower > 0)
                {
                    if (faceHasMachine[x])
                    {                        
                        // we have power and this face has a machine... lets give some power
                        switch(x)
                        {
                            case 0: 
                                beTestMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.NorthCopy()) as BETestMachine;
                                if (beTestMachine != null)
                                {
                                    currentPower = beTestMachine.ReceivePower(currentPower);
                                    if (currentPower == 0) continue;
                                }
                                break;
                            case 1:
                                beTestMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.EastCopy()) as BETestMachine;
                                if (beTestMachine != null)
                                {
                                    currentPower = beTestMachine.ReceivePower(currentPower);
                                    if (currentPower == 0) continue;
                                }
                                break;
                            case 2:
                                beTestMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.SouthCopy()) as BETestMachine;
                                if (beTestMachine != null)
                                {
                                    currentPower = beTestMachine.ReceivePower(currentPower);
                                    if (currentPower == 0) continue;
                                }
                                break;
                            case 3:
                                beTestMachine = this.Api.World.BlockAccessor.GetBlockEntity(this.Pos.WestCopy()) as BETestMachine;
                                if (beTestMachine != null)
                                {
                                    currentPower = beTestMachine.ReceivePower(currentPower);
                                    if (currentPower == 0) continue;
                                }
                                break;
                        }
                    }
                }
            }
        }

        public void NeighborUpdate(IWorldAccessor world)
        {
            // checks neighbor blocks searching for a machine.
            // for the test, only check horizontally N E S W
            faceHasMachine[0] = false;
            if (world.BlockAccessor.GetBlock(this.Pos.NorthCopy()).FirstCodePart() == "vetestmachine")
            {
                faceHasMachine[0] = true;
            }

            faceHasMachine[1] = false;
            if (world.BlockAccessor.GetBlock(this.Pos.EastCopy()).FirstCodePart() == "vetestmachine")
            {
                faceHasMachine[1] = true;
            }

            faceHasMachine[2] = false;
            if (world.BlockAccessor.GetBlock(this.Pos.SouthCopy()).FirstCodePart() == "vetestmachine")
            {
                faceHasMachine[2] = true;
            }

            faceHasMachine[3] = false;
            if (world.BlockAccessor.GetBlock(this.Pos.WestCopy()).FirstCodePart() == "vetestmachine")
            {
                faceHasMachine[3] = true;
            }
        }
        

        public float ChangeTemperature(float fromTemp, float toTemp, float deltaTime)
        {
            float diff = Math.Abs(fromTemp - toTemp);
            deltaTime += deltaTime * (diff / 28f);
            if (diff < deltaTime)
            {
                return toTemp;
            }
            if (fromTemp > toTemp)
            {
                deltaTime = -deltaTime;
            }
            if (Math.Abs(fromTemp - toTemp) < 1f)
            {
                return toTemp;
            }
            return fromTemp + deltaTime;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }
            this.inventory.Pos = this.Pos;
            this.inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            this.RegisterGameTickListener(new Action<float>(OnBurnTick), 100, 0);            
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
//                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
//                    SetDialogValues(dtree);
                    this.clientDialog = new TestGenGUI(DialogTitle, Inventory, this.Pos, this.Api as ICoreClientAPI, this);
                    clientDialog.Update(genTemp, fuelBurnTime, currentPower);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            foreach(ItemSlot slot in this.inventory)
            {
                if (slot.Itemstack != null && !slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            this.inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            
            ITreeAttribute facetree = new TreeAttribute();
            ToFaceTree(facetree);
            tree["faceConnections"] = facetree;

            tree.SetFloat("genTemp", genTemp);
            tree.SetInt("maxTemp", maxTemp);
            tree.SetFloat("fuelBurnTime", fuelBurnTime);
            tree.SetLong("currentPower", (long)currentPower);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            this.inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            FromFaceTree(tree.GetTreeAttribute("faceConnections"));
            if (Api != null) Inventory.AfterBlocksLoaded(this.Api.World);
            genTemp = tree.GetFloat("genTemp", 0);
            maxTemp = tree.GetInt("maxTemp", 0);
            fuelBurnTime = tree.GetFloat("fuelBurnTime", 0);
            currentPower = (ulong)tree.GetLong("currentPower", 0);
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                if (this.clientDialog != null) clientDialog.Update(genTemp, fuelBurnTime, currentPower);
                MarkDirty(true, null);
            }
        }

        private void ToFaceTree(ITreeAttribute tree)
        {
            tree.SetBool("faceNorth", faceHasMachine[0]);
            tree.SetBool("faceEast", faceHasMachine[1]);
            tree.SetBool("faceSouth", faceHasMachine[2]);
            tree.SetBool("faceWest", faceHasMachine[3]);
        }
        private void FromFaceTree(ITreeAttribute tree)
        {
            if (tree == null)
            {
                // failsafe in case of a missing tree attribute.
                faceHasMachine[0] = faceHasMachine[1] = faceHasMachine[2] = faceHasMachine[3] = false;
                return;
            }
            faceHasMachine[0] = tree.GetBool("faceNorth");
            faceHasMachine[1] = tree.GetBool("faceEast");
            faceHasMachine[2] = tree.GetBool("faceSouth");
            faceHasMachine[3] = tree.GetBool("faceWest");
        }
    }
}
