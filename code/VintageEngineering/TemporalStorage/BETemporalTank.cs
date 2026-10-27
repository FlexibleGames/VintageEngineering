using System;
using System.Collections.Generic;
using System.Text;
using VintageEngineering.API;
using VintageEngineering.blockentity;
using VintageEngineering.Blocks;
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
    public class BETemporalTank : BEFluidTank, IVELiquidInterface
    {
        private int _temporalFrequency = 0;
        public int TemporalFrequency
        {
            get { return _temporalFrequency; }
            internal set { _temporalFrequency = value; }
        }
        private TemporalStorageMod _tempMod;
        public override InventoryBase Inventory {
            get 
            { 
                return _tempMod?.TankStorages[_temporalFrequency];
            }
        }
        protected GUITemporalFrequency _freqDialog;
        protected bool _freqDialogOpened = false;
        private string _dialogTitleLangCode = "gui-title-temptank";
        public string DialogTitle => Lang.Get("vinteng:" + _dialogTitleLangCode);

        public BETemporalTank()
        {
            
        }

        public override void Initialize(ICoreAPI api)
        {
            this.Api = api;
            _tempMod = api.ModLoader.GetModSystem<TemporalStorageMod>(true);
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
            
            this.container.inventorySupplier = () => Inventory;
            this.container.Init(Api, () => Pos, delegate { this.MarkDirty(true, null); });
            RegisterGameTickListener(new Action<float>(OnContainerTick), 10000, 0);
            _ownTankBlock = this.Block as BlockFluidTank;
            _capacityLitres = base.Block.Attributes["capacity"].AsInt(10000);
            Inventory.SlotModified += TankSlotModified;

            if (Api.Side == EnumAppSide.Client)
            {
                if (_liquidmesh != null)
                {
                    _liquidmesh = GenFluidMesh();
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"Frequency: {_temporalFrequency}");
            
        }

        public virtual bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api is ICoreServerAPI sapi)
            {
                if (byPlayer.Entity.Controls.Sneak && !_freqDialogOpened)
                {
                    byte[] data = SerializerUtil.Serialize<int>(_temporalFrequency);
                    sapi.Network.SendBlockEntityPacket((IServerPlayer)byPlayer, Pos, 5007, data);
                    _freqDialogOpened = true;
                }
            }
            return true;
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (packetid == 5007)
            {
                if (_freqDialog != null)
                {
                    if (_freqDialog.IsOpened()) _freqDialog.TryClose();
                    if (_freqDialog != null) _freqDialog.Dispose();
                    _freqDialog = null;
                    return;
                }
                _freqDialog = new GUITemporalFrequency(DialogTitle, Pos, capi, this, false);
                _freqDialog.OnClosed += delegate ()
                {
                    _freqDialog = null;
                    RegenFluidMesh();
                };
                _freqDialog.TryOpen();
            }            
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid >= 1000)
            {
                if (packetid == 1006)
                {
                    // GUI Frequency Dialog Save Button
                    int newfreq = SerializerUtil.Deserialize<int>(data);
                    int oldfreq = _temporalFrequency;
                    if (newfreq != _temporalFrequency)
                    {
                        _temporalFrequency = newfreq;
                        _tempMod.TankStorages[oldfreq].SlotModified -= TankSlotModified;

                        if (oldfreq == 0)
                        {
                            _tempMod.TankPositions[0].Remove(Pos.Copy());
                        }
                        else
                        {
                            if (_tempMod.TankPositions[oldfreq].Count == 1)
                            {                                
                                _tempMod.TankStorages[oldfreq].DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 0);
                                _tempMod.TankPositions.Remove(oldfreq);
                                _tempMod.TankStorages.Remove(oldfreq);                                
                            }
                            else
                            {
                                _tempMod.TankPositions[oldfreq].Remove(Pos.Copy());
                            }
                        }
                        if (_tempMod.TankStorages.ContainsKey(_temporalFrequency))
                        {
                            if (_tempMod.TankFrequencyUsers[_temporalFrequency] == fromPlayer.PlayerUID)
                            {
                                // freq is player owned
                                _tempMod.TankPositions[_temporalFrequency].Add(Pos.Copy());
                                _tempMod.TankStorages[_temporalFrequency].SlotModified += TankSlotModified;
                            }
                            else
                            {
                                // freq is not the players, check groups?
                                PlayerGroupMembership[] freqgroups = Api.World.PlayerByUid(_tempMod.TankFrequencyUsers[_temporalFrequency]).Groups;
                                PlayerGroupMembership[] playergroups = fromPlayer.Groups;
                                if (freqgroups.Length == 0 || playergroups.Length == 0)
                                {
                                    _temporalFrequency = oldfreq;
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
                                    if (!groupmatch) _temporalFrequency = oldfreq;
                                    else
                                    {
                                        _tempMod.TankPositions[_temporalFrequency].Add(Pos.Copy());
                                        _tempMod.TankStorages[_temporalFrequency].SlotModified += TankSlotModified;
                                    }
                                }
                            }
                        }
                        else
                        {
                            _tempMod.TankFrequencyUsers.Add(_temporalFrequency, fromPlayer.PlayerUID);
                            _tempMod.TankPositions.Add(_temporalFrequency, new List<BlockPos> { Pos });
                            _tempMod.NewTemporalInventory(_temporalFrequency, Api, false);
                            _tempMod.TankStorages[_temporalFrequency].SlotModified += TankSlotModified;
                        }
                    }                    
                    _freqDialogOpened = false;
                }
                if (packetid == 1008)
                {
                    // GUI Frequency closed without saving
                    _freqDialogOpened = false;
                }
            }
            base.OnReceivedClientPacket(fromPlayer, packetid, data);
        }

        protected virtual void TankSlotModified(int slotid)
        {
            // slotid should only ever be 0
            if (slotid != 0) return;
            _tempMod.TankModified(_temporalFrequency, Pos.Copy());
            RegenFluidMesh();
            MarkDirty(true);
        }

        protected virtual void OnContainerTick(float dt)
        {
            this.container.OnTick(dt);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
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
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            this.Pos = new BlockPos(tree.GetInt("posx", 0), tree.GetInt("posy", 0), tree.GetInt("posz", 0));
            foreach (BlockEntityBehavior beh in Behaviors)
            {
                beh.FromTreeAttributes(tree, worldForResolving);
            }
            _temporalFrequency = tree.GetInt("tempfreq", 0);
            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                _liquidmesh = GenFluidMesh();
                if (_liquidmesh == null) return;
                MarkDirty(true);
            }
        }

    }
}
