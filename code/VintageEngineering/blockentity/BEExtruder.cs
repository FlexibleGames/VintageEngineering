using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.GUI;
using VintageEngineering.RecipeSystem.Recipes;
using VintageEngineering.RecipeSystem;
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
    public class BEExtruder : ElectricBE, ITexPositionSource
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private float updateBouncer = 0f;
        private GUIExtruder clientDialog;

        public string DialogTitle
        {
            get
            {
                return Lang.Get("vinteng:gui-title-extruder");
            }
        }

        public BEExtruder()
        {
            inv = new InvExtruder(null, null);
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
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("veextruder", null, null, new Vec3f(0, GetRotation(), 0f));
                }
                UpdateMesh(2);
            }
            inv.Pos = this.Pos;
            inv.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!inv[0].Empty) FindMatchingRecipe();
        }

        #region RecipeAndInventoryStuff
        private InvExtruder inv;
        private RecipeExtruder currentRecipe;
        private ulong recipePowerApplied;
        private bool isCrafting = false;
        public float RecipeProgress
        {
            get
            {
                if (currentRecipe == null) return 0f;
                return (float)recipePowerApplied / (float)currentRecipe.PowerPerCraft;
            }
        }

        public bool IsCrafting { get { return isCrafting; } }

        public ItemSlot InputSlot { get { return inv[0]; } }
        public ItemSlot OutputSlot { get { return inv[1]; } }
        public ItemSlot RequiresSlot { get { return inv[2]; } }

        public override string InventoryClassName { get { return "VEExtruderInv"; } }

        public override InventoryBase Inventory { get { return inv; } }

        public void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                // something changed with the input slot
                if (InputSlot.Empty)
                {
                    isCrafting = false;
                    currentRecipe = null;
                    recipePowerApplied = 0;
                    StateChange(EnumBEState.Sleeping);
                }
                else
                {
                    FindMatchingRecipe();
                    MarkDirty(true, null);
                }
                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
                }
            }
            if (slotid == 2)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    UpdateMesh(slotid);
                }
            }
        }

        /// <summary>
        /// Output slots IDs is just slotid 1 for this machine.
        /// </summary>
        /// <param name="slotid">Index of ItemSlot inventory</param>
        /// <returns>True if there is room.</returns>
        public bool HasRoomInOutput(int slotid)
        {
            if (slotid != 1) return false;
            if (inv[slotid].Empty) return true;
            if (inv[slotid].StackSize < inv[slotid].Itemstack.Collectible.MaxStackSize) return true;

            return false;
        }

        /// <summary>
        /// Find a matching Log Splitter Recipe given the Blocks inventory.
        /// </summary>
        /// <returns>True if recipe found that matches ingredient.</returns>
        public bool FindMatchingRecipe()
        {
            if (Api == null) return false; // we're running this WAY too soon, bounce.
            if (MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                return false;
            }
            if (InputSlot.Empty)
            {
                currentRecipe = null;
                isCrafting = false;
                StateChange(EnumBEState.Sleeping);
                return false;
            }

            currentRecipe = null;
            List<RecipeExtruder> mprecipes = Api?.ModLoader?.GetModSystem<VERecipeRegistrySystem>(true)?.ExtruderRecipes;

            if (mprecipes == null) return false;

            foreach (RecipeExtruder mprecipe in mprecipes)
            {
                if (mprecipe.Enabled && mprecipe.Matches(InputSlot, RequiresSlot))
                {
                    currentRecipe = mprecipe;
                    isCrafting = true;
                    StateChange(EnumBEState.On);
                    return true;
                }
            }
            currentRecipe = null;
            isCrafting = false;
            recipePowerApplied = 0;
            StateChange(EnumBEState.Sleeping);
            return false;
        }
        #endregion

        public void OnSimTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return; // only tick on the server
            if (IsSleeping)
            {
                // A sleeping machine runs this routine every 2 seconds instead of 10 times a second.
                updateBouncer += dt;
                if (updateBouncer < 2f) return;
                updateBouncer = 0f;
            }
            if (MachineState == EnumBEState.On) // machine is on and actively crafting something
            {
                if (isCrafting && RecipeProgress < 1f)
                {
                    if (CurrentPower == 0 || CurrentPower < (MaxPPS * dt)) return; // we don't have any power to progress.
                    if (!HasRoomInOutput(1) && !HasRoomInOutput(2)) return; // no room in output slots, stop
                    if (currentRecipe == null) return; // how the heck did this happen?

                    float powerpertick = MaxPPS * dt;
                    float percentprogress = powerpertick / currentRecipe.PowerPerCraft; // power to apply this tick

                    if (CurrentPower < powerpertick) return; // last check for our power requirements.

                    // round to the nearest whole number
                    recipePowerApplied += (ulong)Math.Round(powerpertick);
                    electricpower -= (ulong)Math.Round(powerpertick);
                }
                else if (!isCrafting) StateChange(EnumBEState.Sleeping);
                else if (RecipeProgress >= 1f)
                {
                    // recipe crafting complete
                    ItemStack outputprimary = currentRecipe.Outputs[0].ResolvedItemstack.Clone();
                    if (HasRoomInOutput(1))
                    {
                        // primary output is empty, set the stack.
                        if (OutputSlot.Empty) inv[1].Itemstack = outputprimary;
                        else
                        {
                            // how much space is left in primary?
                            int capleft = inv[1].Itemstack.Collectible.MaxStackSize - inv[1].Itemstack.StackSize;
                            if (capleft <= 0) Api.World.SpawnItemEntity(outputprimary, Pos.UpCopy(1).ToVec3d()); // should never fire
                            else if (capleft >= outputprimary.StackSize) inv[1].Itemstack.StackSize += outputprimary.StackSize;
                            else
                            {
                                inv[1].Itemstack.StackSize += capleft;
                                outputprimary.StackSize -= capleft;
                                Api.World.SpawnItemEntity(outputprimary, Pos.UpCopy(1).ToVec3d());
                            }
                        }
                        OutputSlot.MarkDirty();
                    }
                    else
                    {
                        Api.World.SpawnItemEntity(outputprimary, Pos.UpCopy(1).ToVec3d());
                    }

                    if (!RequiresSlot.Empty && currentRecipe.RequiresDurability)
                    {
                        string diemetal = "game:metalbit-" + RequiresSlot.Itemstack.Collectible.LastCodePart();
                        int molddur = RequiresSlot.Itemstack.Collectible.GetRemainingDurability(RequiresSlot.Itemstack);
                        molddur -= 1;
                        RequiresSlot.Itemstack.Attributes.SetInt("durability", molddur);
                        if (molddur == 0)
                        {
                            if (Api.Side == EnumAppSide.Server)
                            {
                                AssetLocation thebits = new AssetLocation(diemetal);
                                int newstack = Api.World.Rand.Next(5, 16);
                                ItemStack bitstack = new ItemStack(Api.World.GetItem(thebits), newstack);
                                Api.World.SpawnItemEntity(bitstack, Pos.UpCopy(1).ToVec3d(), null);

                                RequiresSlot.Itemstack = null;
                                Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/toolbreak"),
                                    Pos.X, Pos.Y, Pos.Z, null, 1f, 16f, 1f);                                
                            }
                        }
                        RequiresSlot.MarkDirty();
                    }
                    InputSlot.TakeOut(currentRecipe.Ingredients[0].Quantity);
                    InputSlot.MarkDirty();
                    if (InputSlot.Empty || !FindMatchingRecipe())
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

        public override void StateChange(EnumBEState newstate)
        {                    
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
                clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
            }
            MarkDirty(true);
        }


        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api != null && Api.Side == EnumAppSide.Client)
            {
                base.toggleInventoryDialogClient(byPlayer, delegate
                {
                    clientDialog = new GUIExtruder(DialogTitle, Inventory, this.Pos, capi, this);
                    clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
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
                GUIExtruder gUILog = clientDialog;
                if (gUILog != null) { gUILog.Dispose(); }
                clientDialog = null;
            }
        }

        public override string GetMachineHUDText()
        {
            string outtext = base.GetMachineHUDText() + System.Environment.NewLine;

            float recipeProgressPercent = RecipeProgress * 100;

            string crafting = isCrafting ? $"{Lang.Get("vinteng:gui-word-crafting")}: {recipeProgressPercent:N1}%" : $"{Lang.Get("vinteng:gui-machine-notcrafting")}";

            return outtext + crafting;
        }

        #region MoldMeshStuff
        protected Shape nowTesselatingShape;
        protected CollectibleObject nowTesselatingObj;
        protected MeshData moldMesh;
        private Vec3f bottomcenter = new Vec3f(0.5f, 0, 0.5f);
        private Vec3f blockcenter = new Vec3f(0.5f, 0.5f, 0.5f);

        public Size2i AtlasSize
        {
            get { return this.capi.BlockTextureAtlas.Size; }
        }
        public void UpdateMesh(int slotid)
        {
            if (Api.Side != EnumAppSide.Server)
            {
                if (inv[slotid].Empty)
                {
                    if (moldMesh != null) moldMesh.Dispose();
                    moldMesh = null;
                    MarkDirty(true, null);
                    return;
                }
                MeshData meshData = GenMesh(inv[slotid].Itemstack);
                if (meshData != null)
                {
                    TranslateMesh(meshData, 1f);
                    moldMesh = meshData;
                }
            }
        }

        public void TranslateMesh(MeshData meshData, float scale)
        {
            //meshData.Scale(bottomcenter, scale, scale, scale);                        
            meshData.Rotate(blockcenter, 0, (float)(GetRotation() * (Math.PI / 180)), (float)1.5708);
            meshData.Translate(0, (float)0.1875, 0);
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


        #region ServerClientStuff
        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
            if (packetid == 1002) // Enable Button
            {
                if (IsEnabled) StateChange(EnumBEState.Off); // turn off
                else
                {
                    StateChange(IsCrafting ? EnumBEState.On : EnumBEState.Sleeping);
                }
                MarkDirty(true, null);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);
            if (clientDialog != null && clientDialog.IsOpened()) clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inv.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetLong("recipepowerapplied", (long)recipePowerApplied);
            tree.SetBool("iscrafting", isCrafting);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inv.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) Inventory.AfterBlocksLoaded(Api.World);
            recipePowerApplied = (ulong)tree.GetLong("recipepowerapplied");
            isCrafting = tree.GetBool("iscrafting", false);
            if (!Inventory[0].Empty) FindMatchingRecipe();

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                StateChange(MachineState);
                if (clientDialog != null)
                {
                    clientDialog.Update(RecipeProgress, CurrentPower, currentRecipe);
                }
                UpdateMesh(2);
                MarkDirty(true, null);
            }
        }

        #endregion
    }
}
