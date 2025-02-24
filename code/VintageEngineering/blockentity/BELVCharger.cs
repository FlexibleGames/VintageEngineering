using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using VintageEngineering.inventory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class BELVCharger : ElectricContainerBE, IRenderer, IDisposable, ITexPositionSource
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private int _powerperdurability;
        private float _updateBouncer = 0f;

        private InvCharger inventory;
        public override InventoryBase Inventory => inventory;

        public ItemSlot InputSlot => inventory[0];
        public override string InventoryClassName => "InvCharger";

        public BELVCharger()
        {
            inventory = new InvCharger(null, null);
            inventory.SlotModified += OnSlotModified;
        }
        private void OnSlotModified(int slotid)
        {
            _updateBouncer = 0f;
            UpdateMesh(rotator);
            if (InputSlot.Empty) SetState(EnumBEState.Sleeping);
            else SetState(EnumBEState.On);
            MarkDirty(true);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _powerperdurability = base.Block.Attributes["powerperdurability"].AsInt(25);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 100, 0);
            }
            else
            {
                capi = api as ICoreClientAPI;
                capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "velvcharger");
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("velvcharger", null, null, new Vec3f(0, Electric.GetRotation(), 0f));
                }
            }
            inventory.Pos = this.Pos;
            inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            if (!InputSlot.Empty)
            {
                UpdateMesh(Electric.GetRotation());
            }
        }

        public void OnSimTick(float dt)
        {
            if (InputSlot.Empty) return;

            if (Electric.IsSleeping || Electric.MachineState == EnumBEState.Paused)
            {
                _updateBouncer += dt;
                if (_updateBouncer < 5f) return;
                 _updateBouncer = 0f;
            }
            if (Electric.RatedPower(dt, false) > Electric.CurrentPower)
            {
                if (Electric.MachineState != EnumBEState.Paused) SetState(EnumBEState.Paused);
                return; // not enough juice
            }
            // first lets check to see if it has the attribute, this is used if base-game durability
            // represents the 'charge' of the item...
            bool chargable = InputSlot.Itemstack.Collectible.Attributes["chargable"].AsBool(false);
            IChargeableItem chargeableItem = InputSlot.Itemstack.Collectible as IChargeableItem;

            if (chargeableItem == null && !chargable) return; // nothing to do with this. It shouldn't have been allowed into the inventory
            // we have something...
            if (chargable)
            {
                // use the durability!
                int curcharge = InputSlot.Itemstack.Collectible.GetRemainingDurability(InputSlot.Itemstack);
                int maxcharge = InputSlot.Itemstack.Collectible.Durability;
                if (curcharge < maxcharge)
                {
                    if (Electric.MachineState != EnumBEState.On) SetState(EnumBEState.On); // on and active.
                    ulong powertouse = Electric.RatedPower(dt, false);
                    // we can't restore fractional durability as its an INT,
                    // so the machine PPS _HAS_ to be >= 10*_powerperdurability, restore a minimum of 1.
                    int torestore = Math.Max(1, ((int)powertouse) / _powerperdurability);
                    curcharge += torestore;
                    if (curcharge > maxcharge) curcharge = maxcharge;
                    InputSlot.Itemstack.Attributes.SetInt("durability", curcharge);
                    Electric.electricpower -= powertouse;                    
                }
                else
                {
                    SetState(EnumBEState.Paused);
                }
            }
            else
            {
                // use the interface!
                int curcharge = ((int)chargeableItem.CurrentPower);
                int maxcharge = ((int)chargeableItem.MaxPower);
                if (curcharge < maxcharge)
                {
                    if (Electric.MachineState != EnumBEState.On) { SetState(EnumBEState.On); }
                    ulong powertopush = chargeableItem.RatedPower(dt, false);
                    ulong powertouse = Electric.RatedPower(dt, false);                    
                    if (powertouse > powertopush) powertouse = powertopush;
                    ulong remaining = chargeableItem.ReceivePower(powertouse, dt, false);
                    if (remaining > 0) powertouse -= remaining;
                    Electric.electricpower -= powertouse;
                }
                else
                {
                    SetState(EnumBEState.Paused);
                }
            }
            UpdateClient(dt);
        }

        private float _clientUpdate = 0f;
        /// <summary>
        /// Push updated information to client on a delay.
        /// </summary>
        /// <param name="dt">DeltaTime</param>
        private void UpdateClient(float dt)
        {
            if (Api.Side == EnumAppSide.Client) return;

            _clientUpdate += dt;
            if (_clientUpdate >= 1.0f)
            {
                MarkDirty(true);
                _clientUpdate = 0.0f;
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Empty)
            {
                if (InputSlot.Empty) return true;
                //ItemSlot getfrom = inventory.GetAutoPullFromSlot(blockSel.Face);
                InputSlot.TryPutInto(Api.World, byPlayer.InventoryManager.ActiveHotbarSlot);
            }
            else
            {
                if (inventory.CanContain(InputSlot, byPlayer.InventoryManager.ActiveHotbarSlot))
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TryFlipWith(InputSlot);
                }
            }
            return true;
        }

        public virtual void SetState(EnumBEState newstate)
        {
            bool changed = Electric.MachineState != newstate;
            Electric.MachineState = newstate;            
            MarkDirty(changed);
        }

        public override void OnBlockRemoved()
        {
            this.Dispose();
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            this.Dispose();
        }

        #region MeshAndRenderingStuff
        protected Shape nowTesselatingShape;
        protected CollectibleObject nowTesselatingObj;
        //protected MeshData originalMesh;
        protected MeshData renderedMesh;
        protected MeshRef renderedMeshRef;
        protected Matrixf ModelMat = new Matrixf();
        protected int textureId;
        private Vec3f center = new Vec3f(0.5f, 0, 0.5f);
        private float rotator = 0f;
        private float degpersecond = 10f;

        public double RenderOrder { get => 0.5; }
        public int RenderRange { get => 24; }
        public Size2i AtlasSize
        {
            get { return capi.BlockTextureAtlas.Size; }
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (InputSlot.Empty) return; // bounce, we have nothing to render

            if (Electric.MachineState == EnumBEState.On)
            {
                RotateMesh(degpersecond * dt);
            }
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;
            rpi.GlDisableCullFace();
            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.ExtraGodray = 0f;
            prog.OverlayOpacity = 0f;
            if (!InputSlot.Empty && renderedMeshRef != null)
            {
                //int num = (int)InputSlot.Itemstack.Collectible.GetTemperature(this.capi.World, InputSlot.Itemstack);
                //Vec4f lightrgbs = this.capi.World.BlockAccessor.GetLightRGBs(this.Pos.X, this.Pos.Y, this.Pos.Z);
                //float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(num);
                //int extraGlow = GameMath.Clamp((num - 550) / 2, 0, 255);
                //prog.NormalShaded = 1;
                //prog.RgbaLightIn = lightrgbs;
                //prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], (float)extraGlow / 255f);
                //prog.ExtraGlow = extraGlow;
                prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
                prog.ModelMatrix = this.ModelMat.Identity().Translate((double)this.Pos.X - camPos.X, (double)this.Pos.Y - camPos.Y, (double)this.Pos.Z - camPos.Z).Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                rpi.RenderMesh(this.renderedMeshRef);
            }
            prog.Stop();
        }
        /// <summary>
        /// Updates a mesh, use when item changes, being reset, but not on tick.
        /// </summary>
        /// <param name="rotation">Inital rotation in degrees.</param>
        public void UpdateMesh(float rotation)
        {
            if (Api.Side != EnumAppSide.Server)
            {
                if (InputSlot.Empty)
                {
                    if (renderedMeshRef != null) renderedMeshRef.Dispose();
                    if (renderedMesh != null) renderedMesh.Dispose();
                    //if (originalMesh != null) originalMesh.Dispose();
                    renderedMeshRef = null;
                    renderedMesh = null;
                    //originalMesh = null;
                    MarkDirty(true);
                    return;
                }
                if (renderedMesh == null)
                {
                    renderedMesh = GenMesh(inventory[0].Itemstack);
                }
                if (renderedMesh != null)
                {
                    if (inventory[0].Itemstack.Class == EnumItemClass.Block)
                    {
                        TranslateMesh(renderedMesh, 0.5f, 0.6875f, rotation);
                    }
                    else
                    {
                        TranslateMesh(renderedMesh, 1f, 0.6875f, rotation);
                    }
                    renderedMeshRef = capi.Render.UploadMesh(renderedMesh);
                }
            }
        }
        /// <summary>
        /// Rotate the contained item mesh by a given number of degrees.<br/>
        /// Will update the mesh using the existing MeshRef.<br/>
        /// Note: Rotation is not total rotation, but just what to rotate this call.<br/>
        /// If given rotation value is 0, it will reset the mesh.
        /// </summary>
        /// <param name="rotation">Rotation to apply.</param>
        public void RotateMesh(float rotation)
        {
            // sanity check
            if (Api.Side == EnumAppSide.Server || renderedMesh == null || renderedMeshRef == null) return;

            if (rotation != 0f)
            {
                rotator += rotation;
                if (rotator >= 360f) rotator = 0f;
                TranslateMesh(renderedMesh, 1f, 0f, rotation);
                capi.Render.UpdateMesh(renderedMeshRef, renderedMesh);                
            }
            else
            {
                UpdateMesh(Electric.GetRotation());
            }
        }

        /// <summary>
        /// Alters the passed in Mesh in 3 possible ways.<br/>
        /// Scale alters size, yoffset alters height, yrotation alters rotation of mesh.
        /// </summary>
        /// <param name="mesh">Mesh to alter</param>
        /// <param name="scale">Scale 0 -> 1</param>
        /// <param name="yoffset">Height to offset, + goes up</param>
        /// <param name="yrotation">Rotation in degrees</param>
        public void TranslateMesh(MeshData mesh, float scale, float yoffset, float yrotation)
        {
            mesh.Scale(center, scale, scale, scale);
            mesh.Rotate(center, 0, yrotation * GameMath.DEG2RAD, 0);
            mesh.Translate(0, yoffset, 0);
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

        public MeshData GenMesh(ItemStack stack)
        {
            IContainedMeshSource meshSource = stack.Collectible as IContainedMeshSource;
            MeshData meshData;

            if (renderedMeshRef != null) renderedMeshRef.Dispose();
            renderedMeshRef = null;

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

        public override void Dispose()
        {
            if (capi != null)
            {
                capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
                if (renderedMesh != null) renderedMesh.Dispose();
                if (renderedMeshRef != null) renderedMeshRef.Dispose();
            }
            //base.Dispose();
        }
        #endregion

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetFloat("rotation", rotator);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            inventory.AfterBlocksLoaded(worldForResolving);
            rotator = tree.GetFloat("rotation", 0);

            if (Api != null && Api.Side == EnumAppSide.Client) SetState(Electric.MachineState);
        }
    }
}
