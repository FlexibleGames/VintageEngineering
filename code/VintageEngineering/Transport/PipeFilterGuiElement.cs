using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public class PipeFilterGuiElement : IFlatListItem, IEquatable<PipeFilterGuiElement>
    {
        private ICoreClientAPI _capi;
        private LoadedTexture _texture;
        private ElementBounds _scissorBounds;

        public LoadedTexture Texture => _texture;


        public string Code = string.Empty;
        public string TextCacheTitle = string.Empty;
        //public string TextCacheAll = string.Empty;
        public bool IsWildcard => Code.Contains('*');
        /// <summary>
        /// True if this element is a block, false if item.
        /// </summary>
        public bool IsBlock = false;

        //private InventoryBase _unspoilableInventory;
        public readonly ItemSlot _dummySlot;

        public PipeFilterGuiElement(ICoreClientAPI capi, string code, bool isblock = false)
        {
            _capi = capi;
            Code = code;
            //_unspoilableInventory = new DummyInventory(capi, 1);
            if (IsWildcard)
            {
                TextCacheTitle = Code;
            }
            else
            {
                try
                {
                    ItemStack stack;
                    if (IsBlock)
                    {
                        stack = new ItemStack(capi.World.GetBlock(new AssetLocation(code)));
                    }
                    else
                    {
                        stack = new ItemStack(capi.World.GetItem(new AssetLocation(code)));
                    }
                    TextCacheTitle = stack.GetName();//.RemoveDiacritics();
                    _dummySlot = new DummySlot(stack);//, _unspoilableInventory);
                }
                catch (Exception ex)
                {
                    _capi.Logger.Error(ex);
                }
            }
            IsBlock = isblock; 
        }    
        
        public PipeFilterGuiElement(ICoreClientAPI capi, ItemStack stack)
        {
            _capi = capi;
            Code = stack.Collectible.Code.ToString();
            IsBlock = stack.Collectible.ItemClass == EnumItemClass.Block;
            TextCacheTitle = stack.GetName();//.RemoveDiacritics();
            _dummySlot = new DummySlot(stack);
        }

        #region IFlatListItem
        public bool Visible { get; set; } = true;

        public void Dispose()
        {
            _texture?.Dispose();
            _texture = null;
        }

        public void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            float size = (float)GuiElement.scaled(25);
            float pad = (float)GuiElement.scaled(4);
            
            if (_texture == null) Recompose(capi);
            _scissorBounds.fixedX = ((double)pad + x - (double)(size / 2f)) / (double)RuntimeEnv.GUIScale;
            _scissorBounds.fixedY = (y - (double)(size / 2f)) / (double)RuntimeEnv.GUIScale;
            _scissorBounds.CalcWorldBounds();
            if (_scissorBounds.InnerWidth <= 0.0 || _scissorBounds.InnerHeight <= 0.0)
            {
                return;
            }
            if (!IsWildcard)
            {
                capi.Render.PushScissor(_scissorBounds, true);
                capi.Render.RenderItemstackToGui(_dummySlot,
                    x + (double)pad + (double)(size / 2f),
                    y + (double)(size / 2f),
                    100, size, -1, true, false, false);
                capi.Render.PopScissor();
            }
            capi.Render.Render2DTexturePremultipliedAlpha(
                _texture.TextureId,
                x + (double)size + GuiElement.scaled(25),
                y + (double)(size / 4f) - GuiElement.scaled(3),
                (double)_texture.Width, (double)_texture.Height, 50f, null);
        }
        #endregion

        public void Recompose(ICoreClientAPI capi)
        {
            _texture?.Dispose();
            _texture = new TextTextureUtil(capi).GenTextTexture(
                TextCacheTitle,
                CairoFont.WhiteSmallText(), null);
            _scissorBounds = ElementBounds.FixedSize(50, 50);
            _scissorBounds.ParentBounds = capi.Gui.WindowBounds;
        }

        public virtual float GetTextMatchWeight(string searchText)
        {
            string title = TextCacheTitle;
            //if (searchText.Contains('*'))
            //{
            //    // if the search is a wildcard search                
            //}
            if (title.Equals(searchText, StringComparison.InvariantCultureIgnoreCase)) return 3f;
            if (title.StartsWith(searchText + " ", StringComparison.InvariantCultureIgnoreCase)) return 2.75f + (float)Math.Max(0, 15 - title.Length) / 100f;
            if (title.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase)) return 2.5f + (float)Math.Max(0, 15 - title.Length) / 100f;
            if (title.CaseInsensitiveContains(searchText, StringComparison.CurrentCultureIgnoreCase)) return 2f;
            if (Code.CaseInsensitiveContains(searchText, StringComparison.CurrentCultureIgnoreCase)) return 1f;
            return 0f;
        }

        public bool Equals(PipeFilterGuiElement other)
        {
            return this.Code == other.Code;
        }
    }

    /// <summary>
    /// Used to properly filter and sort Pipe Filter search results
    /// </summary>
    public struct WeightedFilterEntry
    {
        public float Weight;
        public PipeFilterGuiElement Entry;
    }
}
