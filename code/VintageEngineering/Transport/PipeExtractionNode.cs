using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    /// <summary>
    /// A node that extracts from whatever it's connected to.
    /// </summary>
    public class PipeExtractionNode : IBlockEntityContainer 
    {
        protected ICoreAPI _api;
        protected BlockPos _pos;
        protected string faceCode;
        protected PipeInventory inventory;
        protected long listenerID;
        protected EnumPipeDistribution pipeDistribution = EnumPipeDistribution.Nearest;
        protected bool canFilter = false;
        protected bool canChangeDistro = false;
        protected bool _isSleeping = false;

        public bool IsSleeping
        {
            get => _isSleeping; 
            set => _isSleeping = value;
        }

        private ITransportHandler Handler { get { return _api?.World?.BlockAccessor?.GetBlockEntity<BEPipeBase>(_pos)?.GetHandler(); } }

        /// <summary>
        /// The Enumerator set when Node is in RoundRobin mode.
        /// </summary>
        public List<PipeConnection>.Enumerator PushEnumerator;

        /// <summary>
        /// Block Position of this extraction node.
        /// </summary>
        public BlockPos BlockPosition { get { return _pos; } }
        /// <summary>
        /// The Face this Extraction node points out of (north, east, south, etc)
        /// </summary>
        public string FaceCode
        { get { return faceCode; } }

        /// <summary>
        /// ItemSlot for the pipe upgrade
        /// </summary>
        public ItemSlot Upgrade
        { get { return inventory[0]; } }
        /// <summary>
        /// Quick access to the stack-size move rate of the upgrade, to prevent checking the attributes of the itemstack every tick.<br/>
        /// If this is -1, do a whole stack, regardless of stack-size.
        /// </summary>
        private int _upgradeRate = 1;
        /// <summary>
        /// Quick access to the stack-size move rate of the upgrade, to prevent checking the attributes of the itemstack every tick.<br/>
        /// If this is -1, do a whole stack, regardless of stack-size.
        /// </summary>
        public int UpgradeRate => _upgradeRate;
        /// <summary>
        /// Itemslot for the filter
        /// </summary>
        public ItemSlot Filter
        { get { return inventory[1]; } }
        /// <summary>
        /// Distribution mode of this node, round-robin, nearest first, etc.<br/>
        /// Set via GUI when upgrade is installed that enables this feature, defaults to Nearest First.
        /// </summary>        
        public EnumPipeDistribution PipeDistribution { get { return pipeDistribution; } }
        /// <summary>
        /// Does the installed upgrade in this node allow filters to be installed?
        /// </summary>
        public bool CanFilter { get { return canFilter; } }
        /// <summary>
        /// Does the currently installed upgrade allow the player to change distribution mode?
        /// </summary>
        public bool CanChangeDistro {  get { return canChangeDistro; } }
        /// <summary>
        /// The ID of the Tick listener for this Extraction Node<br/>
        /// ID is provided when registering the tick listener by the game and is used to remove the listener.
        /// </summary>
        public long ListenerID { get => listenerID; set { listenerID = value; } }
        public IInventory Inventory => inventory;

        public string InventoryClassName => $"PipeInventory-{faceCode}";

        public PipeExtractionNode()
        {
            inventory = new PipeInventory(null, 0, null);
            inventory.SlotModified += OnSlotModified;
        }

        public virtual void Initialize(ICoreAPI api, BlockPos pos, string facecode)
        {
            _api = api;
            _pos = pos;
            faceCode = facecode;

            inventory.LateInitialize(
                $"{InventoryClassName}/{_pos.X}/{_pos.Y}/{_pos.Z}",
                api
                );
            inventory.FaceIndex = BlockFacing.FromCode(facecode).Index;

            if (api != null)
            {
                ApplyUpgrade();
            }

        }
        /// <summary>
        /// Called when the chunk a pipe block that contains this node is in is unloaded.
        /// </summary>
        public virtual void OnBlockUnloaded(IWorldAccessor world)
        {
        }

        public virtual void ResetEnumerator(List<PipeConnection> conlist)
        {
            PushEnumerator.Dispose();            
            PushEnumerator = conlist.GetEnumerator();
        }

        /// <summary>
        /// Sets the Distribution mode of this extraction node.<br/>
        /// String parameter reflects internal GUI Drop down option values.
        /// </summary>
        /// <param name="distro">Given string from GUI Dropdown option set.</param>
        public void SetDistroMode(string distro)
        {

            switch (distro)
            {
                case "nearest": pipeDistribution = EnumPipeDistribution.Nearest; break;
                case "farthest": pipeDistribution = EnumPipeDistribution.Farthest; break;
                case "robin": pipeDistribution = EnumPipeDistribution.RoundRobin; break;
                case "random": pipeDistribution = EnumPipeDistribution.Random; break;
                default: pipeDistribution = EnumPipeDistribution.Nearest; break;
            }
        }

        /// <summary>
        /// Extraction Node Inventory Slot Modified<br/>
        /// SlotID 0 = PipeUpgrade<br/>
        /// SlotID 1 = PipeFilter
        /// </summary>
        /// <param name="slotid">SlotId modified.</param>
        public virtual void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                ApplyUpgrade();
            }
        }

        public virtual void ApplyUpgrade()
        {
            BEPipeBase bep = _api.World.BlockAccessor.GetBlockEntity(_pos) as BEPipeBase;
            if (bep == null) return; // the BE we're apart of is invalid somehow
            if (listenerID != 0)
            {
                // remove the listener if we have it
                bep.RemoveExtractionTickEvent(ListenerID);
                listenerID = 0;
            }

            if (Upgrade.Empty) // it IS possible for someone to remove an upgrade.
            {
                _upgradeRate = 1;
                if (!Filter.Empty)
                {
                    inventory.DropSlots(this._pos.UpCopy(1).ToVec3d(), new int[] { 1 });
                }
                listenerID = bep.AddExtractionTickEvent(1000, UpdateTick);
                SetDistroMode("nearest");
                canChangeDistro = false;
                canFilter = false;
            }
            else
            {
                ItemPipeUpgrade upgradeitem = (ItemPipeUpgrade)Upgrade.Itemstack.Collectible;
                int msdelay = upgradeitem.Delay;
                canChangeDistro = upgradeitem.CanChangeDistro;
                if (!canChangeDistro) SetDistroMode("nearest");
                canFilter = upgradeitem.CanFilter;
                if (!canFilter && !Filter.Empty) inventory.DropSlots(this._pos.UpCopy(1).ToVec3d(), new int[] { 1 });
                _upgradeRate = upgradeitem.Rate;
                listenerID = bep.AddExtractionTickEvent(msdelay, UpdateTick);
            }
            if (bep.PipeExtractionGUIs != null &&
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index] != null &&
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index].IsOpened())
            {
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index].Update();
                bep.PipeExtractionGUIs[BlockFacing.FromCode(FaceCode).Index].Recompose();
            }
            bep.MarkDirty(true);
        }
        /// <summary>
        /// Called when removing the node, drops any upgrade and filter.
        /// </summary>
        public virtual void OnNodeRemoved()
        {
            inventory.SlotModified -= OnSlotModified;
            DropContents(_pos.ToVec3d());
        }

        /// <summary>
        /// Update Tick for this extraction node.<br/>
        /// Override to control update tick behavior.
        /// </summary>
        /// <param name="deltatime">Time (in seconds) since last update.</param>
        public virtual void UpdateTick(float deltatime)
        {
            if (_isSleeping || Handler == null || _api.Side == EnumAppSide.Client) return;
            Stopwatch ws = Stopwatch.StartNew();
            Handler.TransportTick(deltatime, _pos, _api.World, this);
            ws.Stop();
            if (ws.ElapsedMilliseconds > 100)
            {
                _api.World.Logger.Debug($"Transport Handler Tick Took {ws.ElapsedMilliseconds}ms");
            }
        }

        /// <summary>
        /// Player right clicked this ExtractionNode, passed in from the block entity.
        /// </summary>
        /// <param name="player">Player who right clicked</param>
        /// <returns>True if event is handled.</returns>
        public virtual bool OnRightClick(IWorldAccessor world, IPlayer player)
        {
            // auto swap held item in player hotbarslot if valid.
            if (player.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible is ItemPipeUpgrade)
            {
                if (Upgrade.Empty)
                {
                    player.InventoryManager.ActiveHotbarSlot.TryPutInto(world, Upgrade, 1);
                }
                else
                {
                    player.InventoryManager.ActiveHotbarSlot.TryFlipWith(Upgrade);
                }
            }
            else if (player.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible is ItemPipeFilter)
            {
                if (!CanFilter)
                {
                    return false;
                }
                if (Filter.Empty)
                {
                    player.InventoryManager.ActiveHotbarSlot.TryPutInto(world, Filter, 1);
                }
                else
                {
                    player.InventoryManager.ActiveHotbarSlot.TryFlipWith(Filter);
                }
            }
            else { return false; }
            return true;
        }

        /// <summary>
        /// Drop upgrade and filter for this node.
        /// </summary>
        /// <param name="atPos">Position to drop at.</param>
        public virtual void DropContents(Vec3d atPos)
        {
            try
            {
                if (inventory != null) inventory.DropAll(atPos);
            }
            catch (Exception ex)
            {
                _api.Logger.Error(ex);
            }
        }

        /// <summary>
        /// Converts the important object data into a TreeAttribute for saving and syncing.
        /// </summary>
        /// <param name="tree"></param>
        public virtual void ToTreeAttributes(ITreeAttribute tree)
        {
            TreeAttribute inventorytree = new TreeAttribute();
            inventory.ToTreeAttributes(inventorytree);
            tree["inventory"] = inventorytree;
            tree.SetBlockPos("position", _pos);
            tree.SetString("facecode", faceCode);
            // ListenerID is not needed on the client nor needs to be saved to disk.
            tree.SetString("distro", pipeDistribution.ToString());
        }
        /// <summary>
        /// Converts a TreeAttribute tree to object data for loading and syncing.
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="worldForResolving"></param>
        public virtual void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _pos = tree.GetBlockPos("position");
            faceCode = tree.GetString("facecode", "error");
            pipeDistribution = Enum.Parse<EnumPipeDistribution>(tree.GetString("distro", "Nearest"));
        }
    }
}
