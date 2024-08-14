using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VintageEngineering.Blocks
{
    public class BlockFluidTank: BlockLiquidContainerBase
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.capacityLitresFromAttributes = 16f;
        }
    }
}
