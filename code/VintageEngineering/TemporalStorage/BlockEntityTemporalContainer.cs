using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.TemporalStorage
{
    public class BlockEntityTemporalContainer : BlockEntityContainer
    {
        private int _temporalFrequency;
        public int TemporalFrequency => _temporalFrequency;

        private TemporalStorageMod _tempMod;
        protected GuiDialogBlockEntity _invDialog;

        public override InventoryBase Inventory => _tempMod?.ChestStorages[_temporalFrequency];
        public override string InventoryClassName => "temporalchest";

        private BlockEntityAnimationUtil _animUtil
        {
            get
            {
                BEBehaviorAnimatable bea = base.GetBehavior<BEBehaviorAnimatable>();
                if (bea == null) return null;
                return bea.animUtil;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            _tempMod = api.ModLoader.GetModSystem<TemporalStorageMod>(true);            
            base.Initialize(api);
        }

        public virtual bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }

        public virtual void ToggleInventoryClientDialog(IPlayer byPlayer, CreateDialogDelegate onCreateDialog)
        {
            if (this._invDialog == null)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                _invDialog = onCreateDialog();
                _invDialog.OnClosed += delegate ()
                {
                    _invDialog = null;
                    capi.Network.SendBlockEntityPacket(Pos, 1001, null);
                };
                _invDialog.TryOpen();
                capi.Network.SendPacketClient(Inventory.Open(byPlayer));
                capi.Network.SendBlockEntityPacket(Pos, 1000, null);
                return;
            }
            _invDialog.TryClose();
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid == 1001) // client is trying to close the dialog
            {
                IPlayerInventoryManager inventoryManager = fromPlayer.InventoryManager;
                if (inventoryManager != null)
                {
                    inventoryManager.CloseInventory(Inventory);
                }
                _tempMod.ChestInventoryClosed(_temporalFrequency, fromPlayer);
                data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(fromPlayer.Entity.EntityId, false));
                (Api as ICoreServerAPI).Network.BroadcastBlockEntityPacket(Pos, 5001, data, [fromPlayer as IServerPlayer]);                
            }
            if (packetid >= 1000) 
            {
                if (packetid == 1000) // client is trying to open the dialog
                {
                    if (!Api.World.Claims.TryAccess(fromPlayer, Pos, EnumBlockAccessFlags.Use))
                    {
                        Inventory.InvNetworkUtil.SendInventoryRollback((IServerPlayer)fromPlayer, packetid, data);
                        return; // only block if we can't interact via permissions with this block
                    }
                    if (_tempMod.LidOpenEntityIDByFreqChest.ContainsKey(_temporalFrequency))
                    {
                        if (_tempMod.LidOpenEntityIDByFreqChest[_temporalFrequency].Contains(fromPlayer.Entity.EntityId))
                        {
                            // If this inventory is already open, do not open again
                            Inventory.InvNetworkUtil.SendInventoryRollback((IServerPlayer)fromPlayer, packetid, data);
                            return;
                        }
                        else
                        {
                            _tempMod.ChestInventoryOpened(_temporalFrequency, fromPlayer);
                        }
                    }
                    else
                    {
                        _tempMod.ChestInventoryOpened(_temporalFrequency, fromPlayer);
                    }
                    IPlayerInventoryManager invMan = fromPlayer.InventoryManager;
                    if (invMan != null)
                    {
                        invMan.OpenInventory(Inventory);
                        data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(fromPlayer.Entity.EntityId, true));
                        (Api as ICoreServerAPI).Network.BroadcastBlockEntityPacket(Pos, 5001, data, [fromPlayer as IServerPlayer]);
                    }
                }
                return;
            }
            if (!Api.World.Claims.TryAccess(fromPlayer, Pos, EnumBlockAccessFlags.Use))
            {
                Inventory.InvNetworkUtil.SendInventoryRollback((IServerPlayer)fromPlayer, packetid, data);
                return; // only block if we can't interact via permissions with this block
            }
            Inventory.InvNetworkUtil.HandleClientPacket(fromPlayer, packetid, data);
            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            IClientWorldAccessor cworld = (IClientWorldAccessor)Api.World;
            if (packetid == 5000)
            {
                if (_invDialog != null)
                {
                    GuiDialogBlockEntity guidialog = _invDialog;
                    if (guidialog != null && guidialog.IsOpened())
                    {
                        _invDialog.TryClose();
                    }
                    if (guidialog != null) guidialog.Dispose();
                    _invDialog = null;
                    return;
                }
                BlockEntityContainerOpen bcontain = BlockEntityContainerOpen.FromBytes(data);
                Inventory.FromTreeAttributes(bcontain.Tree);
                Inventory.ResolveBlocksOrItems();
                _invDialog = new GuiDialogBlockEntityInventory(bcontain.DialogTitle, Inventory, Pos, bcontain.Columns, Api as ICoreClientAPI);
                Block block = Api.World.BlockAccessor.GetBlock(Pos);
                // Sound Stuff
                // block.Attributes["closeSound"]
                // block.Attributes["openSound"]

                _invDialog.TryOpen();
            }
            if (packetid == 5001)
            {
                OpenContainerLidPacket contPacket = SerializerUtil.Deserialize<OpenContainerLidPacket>(data);
                if (contPacket.Opened)
                {
                    _tempMod.ChestInventoryOpened(_temporalFrequency, contPacket.EntityId);
                    OpenLid();
                }
                else
                {
                    _tempMod.ChestInventoryClosed(_temporalFrequency, contPacket.EntityId);
                    CloseLid();
                }
            }
            if (packetid == 1001)
            {
                cworld.Player.InventoryManager.CloseInventory(Inventory);
                if (_invDialog != null && _invDialog.IsOpened())
                {
                    _invDialog.TryClose();
                }
                if (_invDialog != null) _invDialog.Dispose();
                _invDialog = null;
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            Dispose();
        }

        protected override void Dispose()
        {
            base.Dispose();
            if (_invDialog != null && _invDialog.IsOpened())
            {
                _invDialog.TryClose();
            }
            if (_invDialog != null) _invDialog.Dispose();
            if (Api.Side == EnumAppSide.Server)
            {
                if (Inventory.openedByPlayerGUIds != null)
                {
                    Inventory.openedByPlayerGUIds.Clear();
                }
            }
        }

        public virtual void OpenLid()
        {
            string openanim = base.Block.Attributes["openanim"].Exists ? base.Block.Attributes["openanim"].AsString(string.Empty) : string.Empty;
            if (openanim != string.Empty)
            {
                if (_animUtil != null && !_animUtil.activeAnimationsByAnimCode.ContainsKey(openanim))
                {
                    _animUtil.StartAnimation(new AnimationMetaData
                    {
                        Animation = openanim,
                        Code = openanim,
                        AnimationSpeed = 1.8f,
                        EaseOutSpeed = 6f,
                        EaseInSpeed = 15f
                    });

                }
            }
        }

        public virtual void CloseLid()
        {
            string openanim = base.Block.Attributes["openanim"].Exists ? base.Block.Attributes["openanim"].AsString(string.Empty) : string.Empty;
            if (openanim != string.Empty)
            {
                if (_animUtil != null && _animUtil.activeAnimationsByAnimCode.ContainsKey(openanim))
                {
                    _animUtil.StopAnimation(openanim);
                }
            }
        }

        public override void DropContents(Vec3d atPos)
        {            
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"Frequency: {_temporalFrequency}");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("tempfreq", _temporalFrequency);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _temporalFrequency = tree.GetInt("tempfreq", 0);
        }

    }
}
