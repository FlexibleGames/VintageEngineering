using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using VintageEngineering.Electrical;
using VintageEngineering.RecipeSystem.Recipes;
using VintageEngineering.RecipeSystem;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Config;

namespace VintageEngineering
{
    public class BEMetalPress : ElectricBE, ITexPositionSource
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private InvMetalPress inventory;
        private GUIMetalPress clientDialog;


        // a bouncer to limit GUI updates
        private float updateBouncer = 0;

        // Recipe stuff, generic and hard coded for now
        #region RecipeStuff
        /// <summary>
        /// Current Recipe (if any) that the machine can or is crafting.
        /// </summary>
        public RecipeMetalPress currentPressRecipe;

        /// <summary>
        /// Current power applied to the current recipe.
        /// </summary>
        public ulong recipePowerApplied;

        /// <summary>
        /// 0 -> 1 float of recipe progress
        /// </summary>
        public float RecipeProgress
        {
            get
            {
                if (currentPressRecipe == null) { return 0f; }
                return (float)recipePowerApplied / (float)currentPressRecipe.PowerPerCraft;
            }
        }
        private bool isCrafting = false;

        #endregion

        /// <summary>
        /// Is this machine currently working on something?
        /// </summary>
        public bool IsCrafting { get { return isCrafting; } }

        public override bool CanExtractPower => false;
        public override bool CanReceivePower => true;

        private ItemSlot InputSlot
        {
            get
            {
                return this.inventory[0];
            }
        }
        private ItemSlot OutputSlot
        {
            get { return this.inventory[1]; }
        }

        private ItemSlot ExtraOutputSlot
        { get { return this.inventory[2]; } }

        private ItemSlot MoldSlot
        {
            get { return this.inventory[3]; }
        }

        private ItemStack InputStack
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
                return Lang.Get("vinteng:gui-title-metalpress");
            }
        }

        public override string InventoryClassName { get { return "VEMetalPressInv"; } }

        public BEMetalPress()
        {
            this.inventory = new InvMetalPress(null, null);
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
                GUIMetalPress testGenGUI = this.clientDialog;
                if (testGenGUI != null) testGenGUI.Dispose();
                this.clientDialog = null;
            }
        }

        public void OnSlotModified(int slotId)
        {
//            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
//            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            if (slotId == 0 || slotId == 3)
            {
                // new thing in the input or mold slot!
                if (InputSlot.Empty)
                {
                    isCrafting = false;
                    StateChange(EnumBEState.Sleeping);                  
                    currentPressRecipe = null;
                    recipePowerApplied = 0;
                }
                else
                {
                    FindMatchingRecipe();
                }
                MarkDirty(true, null);
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
                }
            }
            if (slotId == 3)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    UpdateMesh(slotId);
                }
            }
        }

        #region MoldMeshStuff
        protected Shape nowTesselatingShape;
        protected CollectibleObject nowTesselatingObj;
        protected MeshData moldMesh;
        private Vec3f center = new Vec3f(0.5f, 0, 0.5f);

        public Size2i AtlasSize
        {
            get { return this.capi.BlockTextureAtlas.Size; }
        }
        public void UpdateMesh(int slotid)
        {
            if (Api.Side != EnumAppSide.Server)
            {
                if (inventory[slotid].Empty)
                {
                    if (moldMesh != null) moldMesh.Dispose();
                    moldMesh = null;
                    MarkDirty(true, null);
                    return;
                }
                MeshData meshData = GenMesh(inventory[slotid].Itemstack);
                if (meshData != null)
                {
                    TranslateMesh(meshData, 1f);
                    moldMesh = meshData;
                }
            }
        }

        public void TranslateMesh(MeshData meshData, float scale)
        {
            meshData.Scale(center, scale, scale, scale);
            meshData.Translate(0, 0.1875f, 0);
        }

        public MeshData GenMesh(ItemStack stack)
        {
            IContainedMeshSource meshSource = stack.Collectible as IContainedMeshSource;
            MeshData meshData;

            if (meshSource != null)
            {
                meshData = meshSource.GenMesh(stack, capi.BlockTextureAtlas, Pos);
                meshData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, base.Block.Shape.rotateY * 0.0174532924f, 0f);
            }
            else
            {
                if (stack.Class == EnumItemClass.Block)
                {
                    meshData = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
                }
                else
                {
                    nowTesselatingObj = stack.Collectible;
                    nowTesselatingShape = null;
                    if (stack.Item.Shape != null)
                    {
                        nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                    }
                    capi.Tesselator.TesselateItem(stack.Item, out meshData, this);
                    meshData.RenderPassesAndExtraBits.Fill((short)2);
                }
            }
            return meshData;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            base.OnTesselation(mesher, tessThreadTesselator); // renders an ACTIVE animation

            if (moldMesh != null)
            {
                mesher.AddMeshData(moldMesh, 1); // add a mold if we have one
            }
            if (AnimUtil.activeAnimationsByAnimCode.Count == 0 &&
                (AnimUtil.animator != null && AnimUtil.animator.ActiveAnimationCount == 0))
            {
                return false; // add base-machine mesh if we're NOT animating
            }
            return true; // do not add base mesh if we're animating
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                Item item = nowTesselatingObj as Item;
                Dictionary<string, CompositeTexture> dictionary = (Dictionary<string, CompositeTexture>)((item != null) ? item.Textures : (nowTesselatingObj as Block).Textures);
                AssetLocation assetLocation = null;
                CompositeTexture compositeTexture;
                if (dictionary.TryGetValue(textureCode, out compositeTexture))
                {
                    assetLocation = compositeTexture.Baked.BakedName;
                }
                if (assetLocation == null && dictionary.TryGetValue("all", out compositeTexture))
                {
                    assetLocation = compositeTexture.Baked.BakedName;
                }
                if (assetLocation == null)
                {
                    Shape shape = this.nowTesselatingShape;
                    if (shape != null)
                    {
                        shape.Textures.TryGetValue(textureCode, out assetLocation);
                    }
                }
                if (assetLocation == null)
                {
                    assetLocation = new AssetLocation(textureCode);
                }
                return this.getOrCreateTexPos(assetLocation);
            }
        }

        private TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition textureAtlasPosition = this.capi.BlockTextureAtlas[texturePath];
            if (textureAtlasPosition == null)
            {
                IAsset asset = this.capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"), true);
                if (asset != null)
                {
                    BitmapRef bmp = asset.ToBitmap(this.capi);
                    int num;
                    //this.capi.BlockTextureAtlas.InsertTextureCached(texturePath, bmp, out num, out textureAtlasPosition, 0.005f);
                    this.capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out num, out textureAtlasPosition, null, 0.005f);
                }
                else
                {
                    ILogger logger = this.capi.World.Logger;                    
                    AssetLocation code = base.Block.Code;
                    logger.Warning($"For render in block {((code != null) ? code.ToString() : "null")}, item {this.nowTesselatingObj.Code} defined texture {texturePath}, no such texture found.");
                }
            }
            return textureAtlasPosition;
        }

        #endregion

        /// <summary>
        /// Find a matching Metal Press Recipe given the Blocks inventory.
        /// </summary>
        /// <returns>True if recipe found that matches ingredient and mold.</returns>
        public bool FindMatchingRecipe()
        {
            if (MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                return false;
            }
            if (InputSlot.Empty)
            {
                currentPressRecipe = null;
                isCrafting = false;
                StateChange(EnumBEState.Sleeping);                
                return false;
            }

            this.currentPressRecipe = null;
            if (Api == null) return false;
            List<RecipeMetalPress> mprecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.MetalPressRecipes;
            
            if (mprecipes == null) return false;

            foreach (RecipeMetalPress mprecipe in mprecipes)
            {
                if (mprecipe.Enabled && mprecipe.Matches(InputSlot, MoldSlot))
                {
                    currentPressRecipe = mprecipe;
                    isCrafting = true;
                    StateChange(EnumBEState.On);
                    return true;
                }
            }
            currentPressRecipe = null;
            isCrafting = false;
            StateChange(EnumBEState.Sleeping);
            return false;
        }

        public override string GetMachineHUDText()
        {
            string outtext = base.GetMachineHUDText() + System.Environment.NewLine;

            float recipeProgressPercent = RecipeProgress * 100;

            string crafting = isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}";

            return outtext + crafting;
        }

        /// <summary>
        /// Output slots IDs are slotid 1 and 2<br/>
        /// Pass in slotid = 0 and forStack = null to return if ANY slot has room.
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid == 0 && forStack == null)
            {
                // a special case to check if any output is full                
                for (int i = 1; i < 3; i++)
                {
                    if (inventory[i].Empty) return true;
                    else
                    {
                        if (inventory[i].Itemstack.StackSize < inventory[i].Itemstack.Collectible.MaxStackSize) return true;
                    }
                }
                return false;
            }
            if (slotid < 1 || slotid > 2) return false; // not output slots
            if (inventory[slotid].Empty) return true;

            // check equality by code
            if (inventory[slotid].Itemstack.Collectible.Code != forStack.Collectible.Code) return false;

            // check stack size held versus max
            int numinslot = inventory[slotid].StackSize;
            if (numinslot >= forStack.Collectible.MaxStackSize) return false;

            return true;
        }

        /// <summary>
        /// Validates the InputStacks Temperature to the Current recipe requirements.
        /// </summary>
        /// <returns>True if Temperature meets requirements.</returns>
        public bool ValidateTemp()
        {
            if (currentPressRecipe == null || InputSlot.Empty) return false;
            // Validate Temperature if required
            if (currentPressRecipe.RequiresTemp != 0)
            {
                if (!InputStack.Collectible.HasTemperature(InputStack)) return false;
                float itemtemp = InputStack.Collectible.GetTemperature(Api.World, InputStack);
                if (currentPressRecipe.RequiresTemp == -1)
                {
                    // meltingpoint / 2, but what if melting point doesn't exist?
                    // sanity check then
                    CombustibleProperties cprops = InputStack.Collectible.CombustibleProps.Clone();
                    if (cprops != null)
                    {
                        if (itemtemp < (cprops.MeltingPoint / 2)) return false;
                    }
                    else return false; // it should not be possible to be here
                }
                else
                {
                    // requirestemp is > 0...
                    if (itemtemp < currentPressRecipe.RequiresTemp) return false;
                }
            }
            return true;
        }
        public void OnSimTick(float deltatime)
        {
            if (this.Api is ICoreServerAPI) // only simulates on the server!
            {
                // if the machine is ON but not crafting, it's sleeping, tick slower
                if (IsSleeping)
                {
                    updateBouncer += deltatime;
                    if (updateBouncer < 2f) return;
                }
                updateBouncer = 0;

                // if we're sleeping, bounce out of here. Extremely fast updates.
                if (MachineState == EnumBEState.On) // block is enabled (on/off)
                {
                    if (isCrafting && RecipeProgress < 1f) // machine is activly crafting and recipe isn't done
                    {
                        if (CurrentPower == 0) return; // we have no power, there's no point in trying. bounce
                        if (!HasRoomInOutput(0, null)) return; // output is full... bounce

                        if (!ValidateTemp()) return;

                        // scale power to apply to recipe by how much time has passed
                        float powerToApply = MaxPPS * deltatime;

                        if (CurrentPower < powerToApply) return; // we don't have enough power to continue... bounce.

                        // calculate percent of progress for this time-step.
                        float percentOfTotal = powerToApply / currentPressRecipe.PowerPerCraft;
                        // apply progress to recipe progress.
                        recipePowerApplied += (ulong)Math.Round(powerToApply);
                        electricpower -= (ulong)Math.Round(powerToApply);
                    }
                    else if (!IsCrafting) // machine isn't crafting
                    {
                        // enabled but not crafting means we have no valid recipe
                        StateChange(EnumBEState.Sleeping); // go to sleep
                    }
                    if (RecipeProgress >= 1f)
                    {
                        // progress finished!
                        float itemtemp = InputStack.Collectible.GetTemperature(Api.World, InputStack);
                        ItemStack outputstack = currentPressRecipe.Outputs[0].ResolvedItemstack.Clone();
                        outputstack.Collectible.SetTemperature(Api.World, outputstack, itemtemp, false);
                        if (HasRoomInOutput(1, outputstack))
                        {
                            // output is empty! need a new stack
                            // Api.World.GetItem(craftingCode)
                            if (OutputSlot.Empty)
                            {
                                Inventory[1].Itemstack = outputstack;
                            }
                            else
                            {
                                int capleft = Inventory[1].Itemstack.Collectible.MaxStackSize - Inventory[1].Itemstack.StackSize;
                                if (capleft <= 0) Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                                else if (capleft >= outputstack.StackSize)
                                {
                                    // we have enough space for the entire new stack
                                    ItemStackMergeOperation merge = new ItemStackMergeOperation(Api.World, EnumMouseButton.Left, (EnumModifierKey)0,
                                        EnumMergePriority.ConfirmedMerge, outputstack.StackSize);
                                    merge.SourceSlot = new DummySlot(outputstack);
                                    merge.SinkSlot = Inventory[1];

                                    // calling this will "merge" the temperatures
                                    Inventory[1].Itemstack.Collectible.TryMergeStacks(merge);
//                                    Inventory[1].Itemstack.StackSize += outputstack.StackSize;
                                }
                                else
                                {
                                    ItemStackMergeOperation merge = new ItemStackMergeOperation(Api.World, EnumMouseButton.Left, (EnumModifierKey)0,
                                        EnumMergePriority.ConfirmedMerge, capleft);
                                    int toSpawn = outputstack.StackSize - capleft;
                                    outputstack.StackSize -= toSpawn;
                                    merge.SourceSlot = new DummySlot(outputstack);
                                    merge.SinkSlot = Inventory[1];
                                    Inventory[1].Itemstack.Collectible.TryMergeStacks(merge);

                                    //Inventory[1].Itemstack.StackSize += capleft;
                                    // spawn what's left
                                    outputstack.StackSize = toSpawn;
                                    Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                                }

                            }
                            OutputSlot.MarkDirty();
                        }
                        else
                        {
                            // no room in main output, how'd we get in here, machine should stop when full...
                            Api.World.SpawnItemEntity(outputstack, Pos.UpCopy(1).ToVec3d());
                        }
                        if (currentPressRecipe.Outputs.Length > 1)
                        {
                            // this recipe has a second output
                            // not going to mess with temperature with secondary outputs as can't rely
                            // on them being metal/heatable.
                            int varoutput = currentPressRecipe.Outputs[1].VariableResolve(Api.World, "VintEng: Metal Press Craft output");
                            if (varoutput > 0)
                            {
                                // depending on Variable set in output stacksize COULD be 0.
                                ItemStack extraoutputstack = new ItemStack(Api.World.GetItem(currentPressRecipe.Outputs[1].ResolvedItemstack.Collectible.Code),
                                                                           varoutput);
                                if (extraoutputstack.StackSize > 0 && HasRoomInOutput(2, extraoutputstack))
                                {
                                    if (ExtraOutputSlot.Empty)
                                    {
                                        Inventory[2].Itemstack = extraoutputstack.Clone();
                                    }
                                    else
                                    {
                                        // drop extras on the ground
                                        int capremaining = Inventory[2].Itemstack.Collectible.MaxStackSize - Inventory[2].Itemstack.StackSize;
                                        if (capremaining >= extraoutputstack.StackSize)
                                        {
                                            Inventory[2].Itemstack.StackSize += extraoutputstack.StackSize;
                                        }
                                        else
                                        {
                                            Inventory[2].Itemstack.StackSize += capremaining;
                                            extraoutputstack.StackSize -= capremaining;
                                            Api.World.SpawnItemEntity(extraoutputstack, Pos.UpCopy(1).ToVec3d());
                                            // spawn what we can't fit
                                        }
                                    }
                                }
                                else
                                {
                                    // no room in output, drop on ground
                                    // TODO Drop in FRONT of the block, or some predetermined place.
                                    Api.World.SpawnItemEntity(extraoutputstack, this.Pos.UpCopy(1).ToVec3d());
                                }
                                ExtraOutputSlot.MarkDirty();
                            }
                        }

                        // damage the mold...
                        if (!MoldSlot.Empty && currentPressRecipe.RequiresDurability) // let the recipe control whether durability is used
                        {
                            string moldmetal = "game:metalbit-" + MoldSlot.Itemstack.Collectible.LastCodePart();
                            int molddur = MoldSlot.Itemstack.Collectible.GetRemainingDurability(MoldSlot.Itemstack);
                            molddur -= 1;
                            MoldSlot.Itemstack.Attributes.SetInt("durability", molddur);
                            if (molddur == 0)
                            {
                                if (Api.Side == EnumAppSide.Server)
                                {
                                    AssetLocation thebits = new AssetLocation(moldmetal);
                                    int newstack = Api.World.Rand.Next(5, 16);
                                    ItemStack bitstack = new ItemStack(Api.World.GetItem(thebits), newstack);
                                    Api.World.SpawnItemEntity(bitstack, Pos.UpCopy().ToVec3d(), null);
                                }
                                MoldSlot.Itemstack = null; // NO SOUP FOR YOU
                                Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"),
                                    this.Pos.X, this.Pos.Y, this.Pos.Z, null, 1f, 16f, 1f);
                            }
                            MoldSlot.MarkDirty();
                        }
                        // remove used ingredients from input
                        InputSlot.TakeOut(currentPressRecipe.Ingredients[0].Quantity);
                        InputSlot.MarkDirty();

                        if (!FindMatchingRecipe())
                        {
                            StateChange(EnumBEState.Sleeping);
                            isCrafting = false;                            
                        }
                        recipePowerApplied = 0;
                        MarkDirty(true, null);
                        Api.World.BlockAccessor.MarkBlockEntityDirty(this.Pos);
                    }
                }
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                this.RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vemetalpress", null, null, new Vec3f(0f, GetRotation(), 0f) );
                }
                UpdateMesh(3);
            }
            this.inventory.Pos = this.Pos;
            this.inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!inventory[0].Empty) FindMatchingRecipe();
        }

        public override void StateChange(EnumBEState newstate)
        {                  
            MachineState = newstate;

            if (MachineState == EnumBEState.On)
            {
                if (AnimUtil != null)
                {
                    if (base.Block.Attributes["craftinganimcode"].Exists)
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
                clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
            }
            MarkDirty(true, null);
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    this.clientDialog = new GUIMetalPress(DialogTitle, Inventory, this.Pos, this.Api as ICoreClientAPI, this);
                    this.clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
                    return this.clientDialog;
                });
            }
            return true;
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable button pressed
            {
                if (IsEnabled) // we're enabled, we need to turn off
                {
                    StateChange(EnumBEState.Off);
                }
                else
                {
                    StateChange(isCrafting ? EnumBEState.On : EnumBEState.Sleeping);
                }
                MarkDirty(true, null);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            this.inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetLong("recipepowerapplied", (long)recipePowerApplied);

            tree.SetBool("isCrafting", isCrafting);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            this.inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) Inventory.AfterBlocksLoaded(this.Api.World);
            recipePowerApplied = (ulong)tree.GetLong("recipepowerapplied");
            isCrafting = tree.GetBool("isCrafting");

            FindMatchingRecipe();
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                StateChange(MachineState);
                if (this.clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentPressRecipe);
                }
                UpdateMesh(3);
                MarkDirty(true, null);
            }
        }
    }
}
