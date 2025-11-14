using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Multiblock
{
    public class MBBEInteractable : BlockEntity
    {
        private BlockPos corePosition;        

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (corePosition == null)
            {
                FindMBCore();
            }
        }

        public void FindMBCore()
        {

        
        }        

        public bool TriggerValidation(IWorldAccessor world, IPlayer byPlayer, BlockSelection sel)
        {
            if (corePosition == null) FindMBCore();

            if (corePosition != null)
            {
                VEMultiblockBeh beh = world.BlockAccessor.GetBlockEntity(corePosition)?.GetBehavior<VEMultiblockBeh>();
                if (beh != null)
                {
                    beh.TriggerValidation(world, byPlayer, sel, corePosition);
                }
            }
            return true;
        }

        internal void SetMBCore(BlockPos core)
        {
            corePosition = core.Copy();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            corePosition = tree.GetBlockPos("corepos");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (corePosition != null) tree.SetBlockPos("corepos", corePosition);
        }
    }
}
