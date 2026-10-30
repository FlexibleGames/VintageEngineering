using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.TemporalStorage
{
    public class BlockBehTemporalContainer : BlockBehavior
    {        
        public BlockBehTemporalContainer(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventSubsequent;
            BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (entity is BlockEntityTemporalContainer betc)
            {
                return betc.OnPlayerRightClick(byPlayer, blockSel);
            }
            else if (entity is BETemporalTank bett)
            {
                if (byPlayer.Entity.Controls.Sneak) bett.OnPlayerRightClick(byPlayer, blockSel);
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);          
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;
            BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
            if (entity is BlockEntityTemporalContainer betc)
            {
                IPlayer[] players = world.AllOnlinePlayers;
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] is IServerPlayer isp)
                    {
                        if (isp.ConnectionState != EnumClientState.Playing) continue;
                    }
                    if (players[i].InventoryManager.HasInventory(betc.Inventory))
                    {
                        players[i].InventoryManager.CloseInventoryAndSync(betc.Inventory);
                    }
                }
            }
        }

        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs, ref EnumHandling handled)
        {
            base.Activate(world, caller, blockSel, activationArgs, ref handled);
            BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            int timeout = (int)activationArgs.TryGetLong("close").GetValueOrDefault(2000L);
            if (entity != null && entity is BlockEntityTemporalContainer betc)
            {
                byte[] open = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(caller.Entity.EntityId, true));
                ((ICoreServerAPI)world.Api).Network.BroadcastBlockEntityPacket(blockSel.Position, 5001, open);
                (world.Api as ICoreServerAPI).World.PlaySoundAt(betc.OpenSound, blockSel.Position, 0.0, null, 1f);
                world.Api.Event.RegisterCallback(delegate (float d)
                {
                    byte[] close = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(caller.Entity.EntityId, false));
                    ((ICoreServerAPI)world.Api).Network.BroadcastBlockEntityPacket(blockSel.Position, 5001, close);
                    (world.Api as ICoreServerAPI).World.PlaySoundAt(betc.CloseSound, blockSel.Position, 0.0, null, 1f);
                }, timeout);
            }
        }
    }
}
