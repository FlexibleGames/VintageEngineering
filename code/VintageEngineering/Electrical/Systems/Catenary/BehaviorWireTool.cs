using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    public class BehaviorWireTool : CollectibleBehavior
    {
        public WireNode wireNode;
        public CatenaryMod cm;
        ICoreAPI api;

        public BehaviorWireTool(CollectibleObject collObj) : base(collObj) { }

        public override void OnLoaded(ICoreAPI bapi)
        {
            api = bapi;
            cm = bapi.ModLoader.GetModSystem<CatenaryMod>();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            //if (handHandling == EnumHandHandling.PreventDefault) return;
            if (blockSel == null) return;
            IWireAnchor anchor = byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as IWireAnchor;
            if (anchor == null) { return; }
            int consat = cm.GetNumberConnectionsAt(blockSel);
            // we either don't have any connections here to interact with, or selection is invalid (or not an IWireAnchor)
            if (consat <= 0) { return; } 
                
            List<WireConnection> wireConnections = anchor.GetWireConnectionsInBlock(blockSel.SelectionBoxIndex, byEntity, blockSel).ToList<WireConnection>();
            
            if (wireConnections == null || wireConnections.Count == 0) return; // no connections, just another sanity check

            // They're RIGHT CLICKING on a wire selection box with wire cutters
            // time to remove all the connections at this spot

            WireConnectionData wcd = new WireConnectionData()
                {
                    opcode = WireConnectionOpCode.RemoveAll,
                    playerUID = (byEntity as EntityPlayer).PlayerUID,
                    _pos = blockSel.Position
                };
            cm.clientChannel.SendPacket(wcd);
            //cm.RemoveAllConnectionsAtPos(blockSel.Position);            

            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventSubsequent;
        }
    }
}
