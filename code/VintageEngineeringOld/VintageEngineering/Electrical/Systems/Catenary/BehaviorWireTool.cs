using System;
using Vintagestory.API.Common;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    public class BehaviorWireTool : CollectibleBehavior
    {
        public WireAnchor wireNode;
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
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
            //if (handHandling == EnumHandHandling.PreventDefault) return;
            IWireAnchor anchor = byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as IWireAnchor;
            PlacedWire wire = anchor?.GetWireConnectionInBlock(blockSel.SelectionBoxIndex, byEntity, blockSel);
            if (wire == null) return;
            
            // They're RIGHT CLICKING on a wire selection box with wire cutters
            // time to remove the connection
            if (api.Side == EnumAppSide.Server)
            {
                cm.RemoveConnection(wire, byEntity, blockSel, slot);                
            } 

            handHandling = EnumHandHandling.PreventDefault;
        }
    }
}
