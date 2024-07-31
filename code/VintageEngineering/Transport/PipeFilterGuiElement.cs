using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public class PipeFilterGuiElement : IFlatListItem
    {
        private ICoreClientAPI _capi;
        private LoadedTexture _texture;

        public LoadedTexture Texture => _texture;

        public string Code = string.Empty;
        public string TextCacheTitle = string.Empty;
        public string TextCacheAll = string.Empty;
        public bool IsWildcard => Code.Contains('*');

        public PipeFilterGuiElement(ICoreClientAPI capi, string code)
        {
            _capi = capi;
            Code = code;
            if (!IsWildcard)
            {

            }
        }

        #region IFlatListItem
        public bool Visible => throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            throw new NotImplementedException();
        }
        #endregion

    }
}
