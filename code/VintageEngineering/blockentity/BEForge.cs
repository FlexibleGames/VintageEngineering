using System;
using System.Collections.Generic;
using System.Text;
using VintageEngineering.Electrical;
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
    public class BEForge : ElectricContainerBE, IRenderer, IDisposable, ITexPositionSource
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
                capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "veforge");
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("veforge", null, null, new Vec3f(0, Electric.GetRotation(), 0f));
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
                UpdateMesh(0);
                FindMatchingRecipe();
                MarkDirty(true, null);

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.Update(RecipeProgress, Electric.CurrentPower, CurrentTemp, _currentTempGoal, tempGoal);
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
            if (Electric.MachineState == EnumBEState.Off) // if the machine is off, bounce.
            {
                isHeating = false;
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
            float basechange = 0f;
            if (fromTemp < 450) basechange = HeatPerSecondBase * deltatime;
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
                            if (basintemp > 20) heatable.SetTemperature(ChangeTemperature(basintemp, 20, dt));
                            else heatable.SetTemperature(20);
                            return;
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
                    else return; // this shouldn't ever fire... but just in case
                }
            }
            this.MarkDirty(true, null);
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
            if (this.Api != null && Api.Side == EnumAppSide.Client)
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

        #region MoldMeshAndRenderingStuff
        protected Shape nowTesselatingShape;
        protected CollectibleObject nowTesselatingObj;
        protected MeshData heatableMesh;
        protected MeshRef heatableMeshRef;
        protected Matrixf ModelMat = new Matrixf();
        protected int textureId;
        private Vec3f center = new Vec3f(0.5f, 0, 0.5f);

        public double RenderOrder { get => 0.5; }
        public int RenderRange { get => 24; }

        public void Dispose()
        {
            if (capi != null)
            {
                capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
                if (heatableMesh != null) heatableMesh.Dispose();
                if (heatableMeshRef != null) heatableMeshRef.Dispose();
            }
        }

        public void OnRenderFrame(float delta, EnumRenderStage stage)
        {
            if (InputSlot.Empty) return; // do nothing if we're not heating an object
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = this.capi.World.Player.Entity.CameraPos;
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
            if (!InputSlot.Empty && heatableMeshRef != null)
            {
                int num = (int)InputSlot.Itemstack.Collectible.GetTemperature(this.capi.World, InputSlot.Itemstack);
                Vec4f lightrgbs = this.capi.World.BlockAccessor.GetLightRGBs(this.Pos.X, this.Pos.Y, this.Pos.Z);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(num);
                int extraGlow = GameMath.Clamp((num - 550) / 2, 0, 255);
                prog.NormalShaded = 1;
                prog.RgbaLightIn = lightrgbs;
                prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], (float)extraGlow / 255f);
                prog.ExtraGlow = extraGlow;
                prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
                prog.ModelMatrix = this.ModelMat.Identity().Translate((double)this.Pos.X - camPos.X, (double)this.Pos.Y - camPos.Y, (double)this.Pos.Z - camPos.Z).Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                rpi.RenderMesh(this.heatableMeshRef);
            }
            prog.Stop();
        }

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
                    if (heatableMeshRef != null) heatableMeshRef.Dispose();
                    if (heatableMesh != null) heatableMesh.Dispose();
                    heatableMeshRef = null;
                    heatableMesh = null;
                    MarkDirty(true, null);
                    return;
                }
                MeshData meshData = GenMesh(inv[slotid].Itemstack);
                if (meshData != null)
                {
                    if (inv[slotid].Itemstack.Class == EnumItemClass.Block)
                    {
                        TranslateMesh(meshData, 0.5f, 0.6875f); // shrink a block down to half size
                    }
                    else
                    {
                        if (inv[slotid].Itemstack.Collectible is ItemWorkItem)
                        {
                            // workitem already seems to be 'at' the height of the anvil, so just need to bump them up one voxel
                            TranslateMesh(meshData, 1f, 0.0625f);
                        }
                        else { TranslateMesh(meshData, 1f, 0.6875f); }
                    }
                    heatableMesh = meshData;
                    heatableMeshRef = capi.Render.UploadMesh(meshData);
                }
            }
        }

        public void TranslateMesh(MeshData meshData, float scale, float yoffset)
        {
            meshData.Scale(center, scale, scale, scale);
            meshData.Translate(0, yoffset, 0);
        }

        public MeshData GenMesh(ItemStack stack)
        {
            IContainedMeshSource meshSource = stack.Collectible as IContainedMeshSource;
            MeshData meshData;

            if (heatableMeshRef != null) heatableMeshRef.Dispose();
            heatableMeshRef = null;

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
                    if (stack.Collectible is ItemWorkItem)
                    {
                        byte[,,] voxels = BlockEntityAnvil.deserializeVoxels(stack.Attributes.GetBytes("voxels", null));
                        meshData = ItemWorkItem.GenMesh(capi, stack, voxels, out textureId);
                        return meshData;
                    }
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

        //public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        //{
        //    base.OnTesselation(mesher, tessThreadTesselator); // renders an ACTIVE animation

        //    if (heatableMesh != null)
        //    {
        //        mesher.AddMeshData(heatableMesh, 1); // add item if we have one
        //    }
        //    if (AnimUtil == null) return false;
        //    if (AnimUtil.activeAnimationsByAnimCode.Count == 0 &&
        //        (AnimUtil.animator != null && AnimUtil.animator.ActiveAnimationCount == 0))
        //    {
        //        return false; // add base-machine mesh if we're NOT animating
        //    }
        //    return true; // do not add base mesh if we're animating
        //}

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
                InputSlot.Itemstack.Collectible.SetTemperature(worldForResolving,
                    InputSlot.Itemstack, currentItemTemp, true);
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
