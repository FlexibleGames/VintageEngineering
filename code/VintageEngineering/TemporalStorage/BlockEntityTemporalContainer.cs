using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.TemporalStorage
{
    public class BlockEntityTemporalContainer : BlockEntityContainer
    {
        private int _temporalFrequency = 0;
        public int TemporalFrequency
        {
            get { return _temporalFrequency; }
            internal set { _temporalFrequency = value; }
        }

        private string _dialogTitleLangCode = "gui-title-tempchest";
        public string DialogTitle => Lang.Get("vinteng:" + _dialogTitleLangCode);

        private TemporalStorageMod _tempMod;
        protected GuiDialogBlockEntity _invDialog;
        protected GUITemporalFrequency _freqDialog;
        protected bool _freqDialogOpened = false;

        public override InventoryBase Inventory => _tempMod?.ChestStorages[_temporalFrequency];
        public override string InventoryClassName => "temporalchest";

        public SoundAttributes CloseSound = new SoundAttributes(AssetLocation.Create("sounds/block/chestclose", "game"), true);
        public SoundAttributes OpenSound = new SoundAttributes(AssetLocation.Create("sounds/block/chestopen", "game"), true);

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
            this.Api = api;
            
            if (api.World.FrameProfiler?.Enabled == true)
            {
                foreach (BlockEntityBehavior val in Behaviors)
                {
                    val.Initialize(api, val.properties);
                    api.World.FrameProfiler.Mark("initbehavior-", val.GetType());
                }
            }
            else
            {
                foreach (BlockEntityBehavior val in Behaviors) val.Initialize(api, val.properties);
            }
            if (stackForWorldgen != null)
            {
                try
                {
                    OnBlockPlaced(stackForWorldgen);
                }
                catch (Exception e)
                {
                    api.Logger.Error(e);
                }
                stackForWorldgen = null;
            }

            _tempMod = api.ModLoader.GetModSystem<TemporalStorageMod>(true);
            this.container.inventorySupplier = () => Inventory;
            this.container.Init(Api, () => Pos, delegate { this.MarkDirty(true, null); });
            JsonObject att = base.Block.Attributes;
            this.OpenSound = (((att != null) ? att["openSound"].AsObject<SoundAttributes?>(null, base.Block.Code.Domain, true) : null) ?? this.OpenSound);
            this.CloseSound = (((att != null) ? att["closeSound"].AsObject<SoundAttributes?>(null, base.Block.Code.Domain, true) : null) ?? this.CloseSound);
            RegisterGameTickListener(new Action<float>(OnContainerTick), 10000, 0);
            if (_animUtil != null && Api.Side == EnumAppSide.Client)
            {
                _animUtil.InitializeAnimator("vetempchest", null, null, new Vec3f(0, GetRotation(), 0f));
            }
        }
        public int GetRotation()
        {
            string side = Block.Variant["side"];
            // The BlockFacing horiztonal index goes counter-clockwise from east. That needs to be converted so that
            // it goes counter-clockwise from north instead.
            int adjustedIndex = ((BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3) & 3;
            return adjustedIndex * 90;
        }

        protected virtual void OnContainerTick(float dt)
        {
            this.container.OnTick(dt);
        }        

        public virtual bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api is ICoreServerAPI sapi)
            {
                // 2 packets sent, 5000, 5001
                // open inventory
                // is sneaking?

                // TODO Security validation check

                if (byPlayer.Entity.Controls.Sneak)
                {
                    // if sneaking, trigger Freq Dialog
                    byte[] data = SerializerUtil.Serialize<int>(_temporalFrequency);
                    sapi.Network.SendBlockEntityPacket((IServerPlayer)byPlayer, Pos, 5007, data);
                    _freqDialogOpened = true;
                }
                else
                {
                    // else, trigger inventory dialog
                    byte[] data = BlockEntityContainerOpen.ToBytes("BlockEntityInventory", DialogTitle, 6, Inventory);
                    sapi.Network.SendBlockEntityPacket((IServerPlayer)byPlayer, Pos, 5000, data);
                    byPlayer.InventoryManager.OpenInventory(Inventory);
                    if (_tempMod.LidOpenEntityIDByFreqChest.ContainsKey(_temporalFrequency))
                    {
                        data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(byPlayer.Entity.EntityId, _tempMod.LidOpenEntityIDByFreqChest[_temporalFrequency].Count > 0));
                    }
                    else
                    {
                        data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(byPlayer.Entity.EntityId, false));
                    }
                    sapi.Network.BroadcastBlockEntityPacket(Pos, 5001, data, new IServerPlayer[]
                    {
                            (IServerPlayer)byPlayer
                    });
                }
            }
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

        public virtual void ToggleFreqencyClientDialog(IPlayer byPlayer, CreateDialogDelegate onCreateDialog)
        {
            if (this._freqDialog == null)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                _freqDialog = (GUITemporalFrequency)onCreateDialog();
                _freqDialog.OnClosed += delegate ()
                {
                    _freqDialog.Dispose();
                    _freqDialog = null;
                    capi.Network.SendBlockEntityPacket(Pos, 1008, null);
                };
                _freqDialog.TryOpen();
                //capi.Network.SendPacketClient(Inventory.Open(byPlayer));
                capi.Network.SendBlockEntityPacket(Pos, 1007, null);
                return;
            }
            _invDialog.TryClose();
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid >= 1000) 
            {
                if (packetid == 1001) // client is trying to close the inventory dialog
                {
                    if (fromPlayer.InventoryManager != null)
                    {
                        fromPlayer.InventoryManager.CloseInventory(Inventory);
                    }
                    _tempMod.ChestInventoryClosed(_temporalFrequency, fromPlayer);

                    data = SerializerUtil.Serialize<OpenContainerLidPacket>(new OpenContainerLidPacket(fromPlayer.Entity.EntityId, false));
                    (Api as ICoreServerAPI).Network.BroadcastBlockEntityPacket(Pos, 5001, data, [fromPlayer as IServerPlayer]);
                }
                if (packetid == 1000) // client is trying to open the inventory dialog
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
                if (packetid == 1006)
                {
                    // GUI Frequency Save button was clicked
                    int newfreq = SerializerUtil.Deserialize<int>(data);
                    int oldfreq = _temporalFrequency;
                    if (_temporalFrequency != newfreq)
                    {
                        _temporalFrequency = newfreq;
                        TemporalStorageMod tsm = _tempMod;
                        if (oldfreq == 0) // need to clean up old freq data first
                        {
                            tsm.ChestPositions[0].Remove(Pos.Copy());
                        }
                        else
                        {
                            if (tsm.ChestPositions[oldfreq].Count == 1) // this is the last position for this frequency
                            {
                                tsm.ChestStorages[oldfreq].DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 0);
                                tsm.ChestPositions.Remove(oldfreq);
                                tsm.ChestFrequencyUsers.Remove(oldfreq); // TODO Security
                                tsm.ChestStorages.Remove(oldfreq);
                            }
                            else
                            {
                                tsm.ChestPositions[oldfreq].Remove(Pos.Copy());
                            }
                        }
                        // clean and build the new frequency objects as needed
                        // newfreq might already be used
                        if (tsm.ChestStorages.ContainsKey(_temporalFrequency))
                        {
                            // freq already in use
                            if (tsm.ChestFrequencyUsers[_temporalFrequency] == fromPlayer.PlayerUID)
                            {
                                // freq is the players
                                tsm.ChestPositions[_temporalFrequency].Add(Pos.Copy());
                            }
                            else
                            {
                                // freq is NOT used by player, check groups?
                                PlayerGroupMembership[] freqgroups = Api.World.PlayerByUid(tsm.ChestFrequencyUsers[_temporalFrequency]).Groups;
                                PlayerGroupMembership[] playergroups = fromPlayer.Groups;
                                if (freqgroups.Length == 0 || playergroups.Length == 0)
                                {
                                    _temporalFrequency = oldfreq; // reset freq, no groups to check                                    
                                }
                                else
                                {
                                    bool groupmatch = false;
                                    for (int i = 0; i < freqgroups.Length; i++)
                                    {
                                        PlayerGroupMembership fgroup = freqgroups[i];
                                        if (playergroups.Contains<PlayerGroupMembership>(fgroup))
                                        {
                                            groupmatch = true;
                                            break;
                                        }
                                    }
                                    if (!groupmatch) _temporalFrequency = oldfreq; // no group matches
                                    else
                                    {
                                        tsm.ChestPositions[_temporalFrequency].Add(Pos.Copy());
                                    }
                                }
                            }
                        }
                        else
                        {
                            // freq is new and unused
                            tsm.ChestFrequencyUsers.Add(_temporalFrequency, fromPlayer.PlayerUID);
                            tsm.ChestPositions.Add(_temporalFrequency, new List<BlockPos> { Pos });
                            tsm.NewTemporalInventory(_temporalFrequency, Api, true);
                        }
                    }
                    _freqDialogOpened = false;
                }
                if (packetid == 1008) // closed freq dialog
                {
                    _freqDialogOpened = false;
                }
                this.MarkDirty(true);
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
            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (packetid == 5000)
            {
                if (_freqDialog != null)
                {
                    if (_freqDialog != null && _freqDialog.IsOpened())
                    {
                        _freqDialog.TryClose();
                    }
                    if (_freqDialog != null) _freqDialog.Dispose();
                    _freqDialog = null;
                }
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

                if (OpenSound != null) _invDialog.OpenSound = OpenSound;
                if (CloseSound != null) _invDialog.CloseSound = CloseSound;

                _invDialog.OnClosed += delegate ()
                {
                    _invDialog = null;
                    CloseLid();
                };
                OpenLid();
                _invDialog.TryOpen();
            }
            if (packetid == 5001)
            {
                OpenContainerLidPacket contPacket = SerializerUtil.Deserialize<OpenContainerLidPacket>(data);
                if (contPacket != null)
                {
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
            }
            if (packetid == 5007) // custom temporal frequency dialog
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
                }
                if (_freqDialog != null)
                {                    
                    if (_freqDialog != null && _freqDialog.IsOpened())
                    {
                        _freqDialog.TryClose();
                    }
                    if (_freqDialog != null) _freqDialog.Dispose();
                    _freqDialog = null;
                    return;
                }
                _freqDialog = new GUITemporalFrequency(DialogTitle, Pos, Api as ICoreClientAPI, this, true);
                _freqDialog.OnClosed += delegate () 
                {
                    _freqDialog = null;
                };
                _freqDialog.TryOpen();
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

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            // _temporalFrequency defaults to 0 on newly placed
            // 0 freq will always exist
            // 0 freq does not have associated users
            _tempMod.ChestPositions[_temporalFrequency].Add(Pos.Copy());
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            //base.OnBlockBroken(byPlayer);
            if (Api is ICoreServerAPI sapi)
            {
                if (_temporalFrequency == 0)
                {
                    _tempMod.ChestPositions[_temporalFrequency].Remove(Pos.Copy());
                }
                else
                {
                    if (_tempMod.ChestPositions[_temporalFrequency].Count == 1) // this is the last position for this frequency
                    {
                        _tempMod.ChestStorages[_temporalFrequency].DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 0);
                        _tempMod.ChestPositions.Remove(_temporalFrequency);
                        _tempMod.ChestFrequencyUsers.Remove(_temporalFrequency); // TODO Security
                        _tempMod.ChestStorages.Remove(_temporalFrequency);
                    }
                    else
                    {
                        _tempMod.ChestPositions[_temporalFrequency].Remove(Pos.Copy());
                    }
                }
                DefaultInterpolatedStringHandler dish = new DefaultInterpolatedStringHandler(31, 3);
                dish.AppendFormatted((byPlayer != null) ? byPlayer.PlayerName : null);
                dish.AppendLiteral(" broke temporal container ");
                dish.AppendFormatted<AssetLocation>(base.Block.Code);
                dish.AppendLiteral(" at ");
                dish.AppendFormatted<BlockPos>(Pos);
                dish.AppendLiteral($" frequency: {_temporalFrequency}.");
                Api.Logger.Audit(dish.ToStringAndClear());
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
            if (_freqDialog != null && _freqDialog.IsOpened())
            {
                _freqDialog.TryClose();
            }
            if (_freqDialog != null) _freqDialog.Dispose();

            _invDialog = null;
            _freqDialog = null;

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
            if (_temporalFrequency == 0) return;
            OnBlockBroken(null);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"Frequency: {_temporalFrequency}");
            dsc.AppendLine($"FreqDialog: {_freqDialogOpened}");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            //base.ToTreeAttributes(tree);
            tree.SetInt("posx", this.Pos.X);
            tree.SetInt("posy", this.Pos.InternalY);
            tree.SetInt("posz", this.Pos.Z);
            if (Block != null)
            {
                tree.SetString("blockCode", Block.Code.ToShortString());
            }
            foreach (BlockEntityBehavior beh in Behaviors)
            {
                beh.ToTreeAttributes(tree);
            }
            tree.SetInt("tempfreq", _temporalFrequency);
            tree.SetBool("freqdialogopen", _freqDialogOpened);
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            //base.FromTreeAttributes(tree, worldForResolving);            
            this.Pos = new BlockPos(tree.GetInt("posx", 0), tree.GetInt("posy", 0), tree.GetInt("posz", 0));
            foreach (BlockEntityBehavior beh in Behaviors)
            {
                beh.FromTreeAttributes(tree, worldForResolving);
            }
            _temporalFrequency = tree.GetInt("tempfreq", 0);
            _freqDialogOpened = tree.GetBool("freqdialogopen", false);
        }

    }
}
