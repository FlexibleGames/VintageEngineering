using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Renderers
{
    public class VEForgeItemRenderer : IRenderer, IDisposable, ITexPositionSource
    {
        private ICoreClientAPI _capi;
        private BlockPos _pos;        
        private Block _block;
        protected Shape _nowTesselatingShape;
        protected CollectibleObject _nowTesselatingObj;
        //private MeshData _heatableMesh;
        private MultiTextureMeshRef _heatableMeshRef;
        private ItemStack _stack;
        private string _tmpMetal;
        //private ITexPositionSource _tmpTextureSource;
        private Matrixf _modelMat = new Matrixf();
        private Vec3f _rotationRad;

        private Vec3f _center = new Vec3f(0.5f, 0, 0.5f);

        public VEForgeItemRenderer(Block forBlock, BlockPos pos, ICoreClientAPI capi, Vec3f rotationRad) 
        {
            _capi = capi;
            _pos = pos;
            _block = forBlock;
            _rotationRad = rotationRad;
        }

        public void SetContents(ItemStack stack, bool regen)
        {
            _stack = stack;
            if (regen)
            {
                RegenMesh();
            }
        }

        public void RegenMesh()
        {
            if (_heatableMeshRef != null) _heatableMeshRef.Dispose();
            _heatableMeshRef = null;
            if (_stack == null) return;

            _tmpMetal = _stack.Collectible.LastCodePart(0);

            IContainedMeshSource meshSource = _stack.Collectible as IContainedMeshSource;
            MeshData meshData = null;

            string firstCodePart = _stack.Collectible.FirstCodePart(0);
            
            if (meshSource != null)
            {
                DummySlot dslot = new() { Itemstack = _stack };
                meshData = meshSource.GenMesh(dslot, _capi.BlockTextureAtlas, _pos);
                meshData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, _block.Shape.rotateY * 0.0174532924f, 0f);
            }
            else
            {
                if (_stack.Class == EnumItemClass.Block)
                {
                    meshData = _capi.TesselatorManager.GetDefaultBlockMesh(_stack.Block).Clone();
                }
                else
                {
                    if (_stack.Collectible is ItemWorkItem || firstCodePart == "workitem" || (firstCodePart == "ironbloom" && _stack.Attributes.HasAttribute("voxels")))
                    {
                        MeshData workItemMesh = ItemWorkItem.GenMesh(_capi, _stack, ItemWorkItem.GetVoxels(_stack));
                        if (workItemMesh != null)
                        {
                            TranslateMesh(workItemMesh, 1f, 0.0625f);
                            workItemMesh.Rotate(new Vec3f(0.5f, 0, 0.5f), _rotationRad.X, _rotationRad.Y, _rotationRad.Z);
                            _heatableMeshRef = _capi.Render.UploadMultiTextureMesh(workItemMesh);
                        }
                    }
                    else
                    {
                        _capi.Tesselator.TesselateItem(_stack.Item, out meshData);
                        //_nowTesselatingObj = islot.Itemstack.Collectible;
                        //_nowTesselatingShape = null;
                        //if (islot.Itemstack.Item.Shape != null)
                        //{
                        //    _nowTesselatingShape = _capi.TesselatorManager.GetCachedShape(islot.Itemstack.Item.Shape.Base);
                        //}
                        //_capi.Tesselator.TesselateItem(islot.Itemstack.Item, out meshData, this);
                        //meshData.RenderPassesAndExtraBits.Fill((short)2);
                    }
                }
            }
            if (meshData != null)
            {
                meshData.Rotate(new Vec3f(0.5f, 0, 0.5f), _rotationRad.X, _rotationRad.Y, _rotationRad.Z);
                TranslateMesh(meshData, 1f, 0.6875f);
//                _heatableMesh = meshData;
                _heatableMeshRef = _capi.Render.UploadMultiTextureMesh(meshData);
            }
        }
        public void TranslateMesh(MeshData meshData, float scale, float yoffset)
        {
            meshData.Scale(_center, scale, scale, scale);
            meshData.Translate(0, yoffset, 0);
        }

        public double RenderOrder { get => 0.5; }
        public int RenderRange { get => 24; }
        public Size2i AtlasSize
        {
            get { return this._capi.BlockTextureAtlas.Size; }
        }
        public void OnRenderFrame(float delta, EnumRenderStage stage)
        {
            if (_stack == null) return; // do nothing if we're not heating an object
            IRenderAPI rpi = _capi.Render;
            Vec3d camPos = this._capi.World.Player.Entity.CameraPos;
            rpi.GlDisableCullFace();

            if (_stack != null && _heatableMeshRef != null)
            {
                int num = (int)_stack.Collectible.GetTemperature(this._capi.World, _stack);
                Vec4f lightrgbs = this._capi.World.BlockAccessor.GetLightRGBs(this._pos.X, this._pos.Y, this._pos.Z);
                float[] glowColor = ColorUtil.GetIncandescenceColorAsColor4f(num);
                int extraGlow = GameMath.Clamp((num - 550) / 2, 0, 255);
                Vec4f glowRgb = new()
                {
                    R = glowColor[0],
                    G = glowColor[1],
                    B = glowColor[2],
                    A = (float)extraGlow / 255f
                };
                IShaderProgram workItemShader = _capi.ModLoader.GetModSystem<SurvivalCoreSystem>(true).smithingWorkItemShader;
                workItemShader.Use();

                workItemShader.Uniform("rgbaAmbientIn", rpi.AmbientColor);
                workItemShader.Uniform("rgbaFogIn", rpi.FogColor);
                workItemShader.Uniform("fogMinIn", rpi.FogMin);
                workItemShader.Uniform("dontWarpVertices", 0);
                workItemShader.Uniform("addRenderFlags", 0);
                workItemShader.Uniform("fogDensityIn", rpi.FogDensity);
                workItemShader.Uniform("rgbaTint", ColorUtil.WhiteArgbVec);
                workItemShader.Uniform("rgbaLightIn", lightrgbs);
                workItemShader.Uniform("rgbaGlowIn", glowRgb);
                workItemShader.Uniform("extraGlow", extraGlow);
                workItemShader.Uniform("tempGlowMode", 0);
                workItemShader.UniformMatrix("modelMatrix", this._modelMat.Identity().Translate((double)this._pos.X - camPos.X, (double)this._pos.Y - camPos.Y, (double)this._pos.Z - camPos.Z).Values);
                workItemShader.UniformMatrix("viewMatrix", rpi.CameraMatrixOriginf);
                workItemShader.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
                rpi.RenderMultiTextureMesh(_heatableMeshRef, "tex", 0);
                workItemShader.Stop();
                //prog.Tex2D = _capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
            }
        }
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                //return _tmpTextureSource[_tmpMetal];                
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
            _capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            if (_heatableMeshRef != null) _heatableMeshRef.Dispose();
            //if (_heatableMesh != null) _heatableMesh.Dispose();
        }
    }
}
