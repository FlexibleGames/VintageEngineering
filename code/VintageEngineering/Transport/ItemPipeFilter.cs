using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    public delegate GUIPipeFilter CreateFilterDialogDelegate();
    public class ItemPipeFilter : Item
    {
        private GUIPipeFilter _filterGUI;
        public bool _isBlacklist = false;
        private ICoreClientAPI capi;        

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            _isBlacklist = this.Attributes["isblacklist"].AsBool(false);

            if (api.Side == EnumAppSide.Client) capi = api as ICoreClientAPI;
        }

        public void ToggleFilterGUI(EntityPlayer player, CreateFilterDialogDelegate onCreateDialog)
        {
            if (_filterGUI == null)
            {
                _filterGUI = onCreateDialog();
            }
            else
            {
                if (_filterGUI.IsOpened()) return;
            }
            _filterGUI.OnClosed += delegate ()
            {
                _filterGUI.Dispose();
                _filterGUI = null;

                if (capi != null)
                {
                    if (!player.Player.InventoryManager.ActiveHotbarSlot.Empty)
                    {
                        ItemSlot pslot = player.Player.InventoryManager.ActiveHotbarSlot;
                        if (pslot.Itemstack.Attributes != null)
                        {
                            capi.Network.GetChannel("vepipefiltersync").SendPacket<PipeFilterPacket>(new PipeFilterPacket { SyncedStack = pslot.Itemstack.ToBytes() });
                        }
                    }
                }
            };
            _filterGUI.TryOpen();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (slot.Empty || blockSel == null || api.World.BlockAccessor.GetBlock(blockSel.Position) is not BlockPipeBase)
            {
                if (api.Side == EnumAppSide.Server) return;
                if (byEntity is not EntityPlayer) return;

                ToggleFilterGUI(byEntity as EntityPlayer, delegate
                {
                    _filterGUI = new GUIPipeFilter(capi, slot.Itemstack);
                    return _filterGUI;
                });
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }    
}
