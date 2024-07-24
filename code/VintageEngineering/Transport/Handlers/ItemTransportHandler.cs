using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport.Handlers
{
    public class ItemTransportHandler : ITransportHandler
    {
        public EnumPipeUse PipeType => EnumPipeUse.item;

        public void TransportTick(float deltatime, BlockPos pos, IWorldAccessor world, PipeExtractionNode node)
        {
            BEPipeBase us = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            if (us == null) return; // sanity check

            BlockPos connectedto = pos.AddCopy(BlockFacing.FromCode(node.FaceCode));
            InventoryBase inv = (InventoryBase)((world.BlockAccessor.GetBlock(connectedto).GetInterface<IBlockEntityContainer>(world, connectedto)).Inventory);
            if (inv == null) return; // sanity check 2
            int stacksize = node.UpgradeRate;
            ItemSlot pull;
            if (world.BlockAccessor.GetBlockEntity(connectedto) is BlockEntityGenericTypedContainer)
            {
                pull = GetGenericInventoryPullSlot(inv);
            }
            else
            { 
                pull = GetPullSlot(inv, node); 
            }
            if (pull == null) return;
            if (stacksize == -1)
            {
                stacksize = pull.Itemstack.Collectible.MaxStackSize;
            }
            ItemStackMoveOperation ismo = new ItemStackMoveOperation(world, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, stacksize);            

            ItemSlot push = GetPushSlot(world, node, us.PushConnections, pull);

            if (push == null) return; // sanity check 3

            int moved = pull.TryPutInto(push, ref ismo);
            if (moved == 0) return;
        }

        public ItemSlot GetPullSlot(InventoryBase inventory, PipeExtractionNode node)
        {
            if (inventory.Empty || inventory.Count == 0) return null;
            // TODO all the things
            // filter stuff, etc
            return inventory.GetAutoPullFromSlot(BlockFacing.FromCode(node.FaceCode).Opposite);
        }
        /// <summary>
        /// Special case for vanilla inventories that only allow pulling from the DOWN direction.
        /// </summary>
        /// <param name="inv"></param>        
        /// <returns></returns>
        public ItemSlot GetGenericInventoryPullSlot(InventoryBase inv)
        {
            if (inv.Empty || inv.Count == 0) { return null; }
            return inv.GetAutoPullFromSlot(BlockFacing.DOWN);
        }

        /// <summary>
        /// Returns the first valid ItemSlot for the given slot "pullfrom"<br/>
        /// Returns null if no valid slot is found.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="node">PipeExtractionNode being ticked</param>
        /// <param name="pushcons">PipeConnection list to check for valid push slots.</param>
        /// <param name="pullfrom">ItemSlot that is providing the ItemStack to move.</param>
        /// <returns>Valid ItemSlot to push into, or null if no slot is found.</returns>
        public ItemSlot GetPushSlot(IWorldAccessor world, PipeExtractionNode node, List<PipeConnection> pushcons, ItemSlot pullfrom)
        {
            if (node.PipeDistribution == EnumPipeDistribution.Nearest)
            {
                // what is the cost of this call?
                PipeConnection[] conarray = pushcons.ToArray();
                Array.Sort(conarray, (x, y) => x.Distance.CompareTo(y.Distance));

                for (int x = 0; x < conarray.Length; x++)
                {
                    IBlockEntityContainer contain = world.BlockAccessor.GetBlock(conarray[x].Position).GetInterface<IBlockEntityContainer>(world, conarray[x].Position);
                    if (contain.Inventory is InventoryBase inv)
                    {
                        ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);
                        if (push == null) continue;
                        return push;
                    }
                    else continue;
                }
            }
            else if (node.PipeDistribution == EnumPipeDistribution.Farthest)
            {
                PipeConnection[] conarray = pushcons.ToArray();
                Array.Sort(conarray, (x, y) => y.Distance.CompareTo(x.Distance));
                for (int x = 0; x < conarray.Length; x++)
                {
                    IBlockEntityContainer contain = world.BlockAccessor.GetBlock(conarray[x].Position).GetInterface<IBlockEntityContainer>(world, conarray[x].Position);
                    if (contain is InventoryBase inv)
                    {
                        ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);
                        if (push == null) continue;
                        return push;
                    }
                    else continue;
                }
            }
            else if (node.PipeDistribution == EnumPipeDistribution.RoundRobin)
            {
                if (node.PushEnumerator.Current == null)
                {
                    node.PushEnumerator = pushcons.GetEnumerator();
                }
                else node.PushEnumerator.MoveNext();

                PipeConnection current = node.PushEnumerator.Current;
                IBlockEntityContainer contain = world.BlockAccessor.GetBlock(current.Position).GetInterface<IBlockEntityContainer>(world, current.Position);
                if (contain is InventoryBase inv)
                {
                    ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);                    
                    return push;
                }
            }
            else
            {
                // this is Random
                int randomcon = world.Rand.Next(pushcons.Count);                
                PipeConnection current = pushcons[randomcon];
                IBlockEntityContainer contain = world.BlockAccessor.GetBlock(current.Position).GetInterface<IBlockEntityContainer>(world, current.Position);
                if (contain is InventoryBase inv)
                {
                    ItemSlot push = inv.GetAutoPushIntoSlot(BlockFacing.FromCode(node.FaceCode).Opposite, pullfrom);
                    return push;
                }
            }
            return null;
        }
    }
}
