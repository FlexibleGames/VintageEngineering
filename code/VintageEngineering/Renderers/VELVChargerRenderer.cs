using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Renderers
{
    public class VELVChargerRenderer : IRenderer, IDisposable, ITexPositionSource
    {
        private ICoreClientAPI _capi;
        private BlockPos _pos;
        private Block _block;
        private BELVCharger _becharger;
        private ItemStack _stack;

        protected Shape _nowTesselatingShape;
        protected CollectibleObject _nowTesselatingObj;
        
        protected MeshData _renderedMesh;
        protected MeshRef _renderedMeshRef;
        protected Matrixf _modelMat = new Matrixf();
        protected int _textureId;
        private Vec3f _center = new Vec3f(0.5f, 0, 0.5f);
        public float _rotator = 0f;
        private float _degpersecond = 10f;

        public double RenderOrder { get => 0.5; }
        public int RenderRange { get => 24; }
        public Size2i AtlasSize
        {
            get { return _capi.BlockTextureAtlas.Size; }
        }

        public VELVChargerRenderer(Block forBlock, BlockPos pos, ICoreClientAPI capi, BELVCharger be)
        {
            _capi = capi;
            _block = forBlock;
            _pos = pos;
            _becharger = be;
        }

        public void SetContents(ItemStack stack, bool regen)
        {
            _stack = stack;
            if (regen)
            {
                UpdateMesh(_becharger.Electric.GetRotation());
            }
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (_stack == null) return; // bounce, we have nothing to render

            if (_becharger != null && _becharger.Electric.MachineState == EnumBEState.On)
            {
                RotateMesh(_degpersecond * dt);
            }
            IRenderAPI rpi = _capi.Render;
            Vec3d camPos = _capi.World.Player.Entity.CameraPos;
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
            if (_stack != null && _renderedMeshRef != null)
            {
                //int num = (int)InputSlot.Itemstack.Collectible.GetTemperature(this._capi.World, InputSlot.Itemstack);
                //Vec4f lightrgbs = this._capi.World.BlockAccessor.GetLightRGBs(this.Pos.X, this.Pos.Y, this.Pos.Z);
                //float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(num);
                //int extraGlow = GameMath.Clamp((num - 550) / 2, 0, 255);
                //prog.NormalShaded = 1;
                //prog.RgbaLightIn = lightrgbs;
                //prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], (float)extraGlow / 255f);
                //prog.ExtraGlow = extraGlow;
                prog.Tex2D = _capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
                prog.ModelMatrix = this._modelMat.Identity().Translate((double)this._pos.X - camPos.X, (double)this._pos.Y - camPos.Y, (double)this._pos.Z - camPos.Z).Values;
                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                rpi.RenderMesh(this._renderedMeshRef);
            }
            prog.Stop();
        }
        /// <summary>
        /// Updates a mesh, use when item changes, being reset, but not on tick.
        /// </summary>
        /// <param name="rotation">Inital rotation in degrees.</param>
        public void UpdateMesh(float rotation)
        {
            if (_capi != null)
            {
                if (_stack == null)
                {
                    if (_renderedMeshRef != null) _renderedMeshRef.Dispose();
                    if (_renderedMesh != null) _renderedMesh.Dispose();
                    //if (originalMesh != null) originalMesh.Dispose();
                    _renderedMeshRef = null;
                    _renderedMesh = null;
                    //originalMesh = null;
                    //MarkDirty(true);
                    return;
                }
                if (_renderedMesh == null)
                {
                    _renderedMesh = GenMesh(_stack);
                }
                if (_renderedMesh != null)
                {
                    if (_stack.Class == EnumItemClass.Block)
                    {
                        TranslateMesh(_renderedMesh, 0.5f, 0.6875f, rotation);
                    }
                    else
                    {
                        TranslateMesh(_renderedMesh, 1f, 0.6875f, rotation);
                    }
                    // do we add more special cases here, or do we use custom attributes for charger translation?
                    // the Angel Belt is rendered way up off the charger as the item itself is worn on the waist
                    // but the shape is not high up, what translates the belt so high above the charger?
                    _renderedMeshRef = _capi.Render.UploadMesh(_renderedMesh);
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
            if (_capi == null || _renderedMesh == null || _renderedMeshRef == null) return;

            if (rotation != 0f)
            {
                _rotator += rotation;
                if (_rotator >= 360f) _rotator = 0f;
                TranslateMesh(_renderedMesh, 1f, 0f, rotation);
                _capi.Render.UpdateMesh(_renderedMeshRef, _renderedMesh);
            }
            else
            {
                UpdateMesh(_becharger.Electric.GetRotation());
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
            mesh.Scale(_center, scale, scale, scale);
            mesh.Rotate(_center, 0, yrotation * GameMath.DEG2RAD, 0);
            mesh.Translate(0, yoffset, 0);
        }
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                Item item = _nowTesselatingObj as Item;
                Dictionary<string, CompositeTexture> dictionary = (Dictionary<string, CompositeTexture>)((item != null) ? item.Textures : (_nowTesselatingObj as Block).Textures);
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
                    Shape shape = this._nowTesselatingShape;
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

            if (_renderedMeshRef != null) _renderedMeshRef.Dispose();
            _renderedMeshRef = null;

            if (meshSource != null)
            {
                DummySlot dslot = new() { Itemstack = stack };
                meshData = meshSource.GenMesh(dslot, _capi.BlockTextureAtlas, _pos);
                meshData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, _block.Shape.rotateY * 0.0174532924f, 0f);
            }
            else
            {
                if (stack.Class == EnumItemClass.Block)
                {
                    meshData = _capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
                }
                else
                {
                    _nowTesselatingObj = stack.Collectible;
                    _nowTesselatingShape = null;
                    if (stack.Item.Shape != null)
                    {
                        _nowTesselatingShape = _capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                    }
                    _capi.Tesselator.TesselateItem(stack.Item, out meshData, this);
                    meshData.RenderPassesAndExtraBits.Fill((short)2);
                }
            }
            return meshData;
        }
        private TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition textureAtlasPosition = this._capi.BlockTextureAtlas[texturePath];
            if (textureAtlasPosition == null)
            {
                IAsset asset = this._capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"), true);
                if (asset != null)
                {
                    BitmapRef bmp = asset.ToBitmap(this._capi);
                    int num;
                    //this._capi.BlockTextureAtlas.InsertTextureCached(texturePath, bmp, out num, out textureAtlasPosition, 0.005f);
                    this._capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out num, out textureAtlasPosition, null, 0.005f);
                }
                else
                {
                    ILogger logger = this._capi.World.Logger;
                    AssetLocation code = _block.Code;
                    logger.Warning($"For render in block {((code != null) ? code.ToString() : "null")}, item {this._nowTesselatingObj.Code} defined texture {texturePath}, no such texture found.");
                }
            }
            return textureAtlasPosition;
        }

        public void Dispose()
        {
            if (_capi != null)
            {
                _capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
                if (_renderedMesh != null) _renderedMesh.Dispose();
                if (_renderedMeshRef != null) _renderedMeshRef.Dispose();
            }
            //base.Dispose();
        }
    }
}
