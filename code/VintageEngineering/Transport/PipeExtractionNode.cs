using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private static ITransportHandler handler;

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
            inventory = new PipeInventory(null, null);
            inventory.SlotModified += OnSlotModified;            
        }

        public virtual void Initialize(ICoreAPI api, BlockPos pos, string face)
        {
            _api = api;
            _pos = pos;
            faceCode = face;

            inventory.LateInitialize(
                $"{InventoryClassName}/{_pos.X}/{_pos.Y}/{_pos.Z}",
                api
                );
        }
        /// <summary>
        /// Sets the Transport Handler for this extraction node.<br/>
        /// Called by the block entity of the pipe.
        /// </summary>
        /// <param name="_handler"></param>
        public void SetHandler( ITransportHandler _handler )
        {
            handler = _handler;
        }
        /// <summary>
        /// Sets the Distribution mode of this extraction node.
        /// </summary>
        /// <param name="distro"></param>
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
                // pipe upgrade changed
            }
            else
            {
                // pipe filter changed
            }
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
            handler.TransportTick(deltatime, _pos, _api.World, this);
        }

        /// <summary>
        /// Player right clicked this ExtractionNode, passed in from the block.
        /// </summary>
        /// <param name="player">Player who right clicked</param>
        /// <returns>True if event is handled.</returns>
        public virtual bool OnRightClick(IPlayer player)
        {
            // auto swap held item in player hotbarslot if valid.
            return true;
        }

        /// <summary>
        /// Drop upgrade and filter for this node.
        /// </summary>
        /// <param name="atPos">Position to drop at.</param>
        public virtual void DropContents(Vec3d atPos)
        {
            inventory.DropAll(atPos);
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
            if (_api != null && _api.Side == EnumAppSide.Server)
            {
                _upgradeRate = 1; // defaults to 1
                if (!Upgrade.Empty)
                {
                    // check upgrade attributes for rate and delay values.
                    // server side this means we were loaded from disk
                    // need to redo the tick listener
                    // get the upgrade, get the rate
                    if (Upgrade.Itemstack.Item != null && Upgrade.Itemstack.Collectible is ItemPipeUpgrade upgrade)
                    {
                        // we have an upgrade!
                        //ItemPipeUpgrade upgrade = (ItemPipeUpgrade)Upgrade.Itemstack.Collectible;
                        _upgradeRate = upgrade.Rate;
                        canFilter = upgrade.CanFilter;
                        canChangeDistro = upgrade.CanChangeDistro;
                        BEPipeBase bep = worldForResolving.BlockAccessor.GetBlockEntity(this._pos) as BEPipeBase;
                        if (bep != null && _api.Side == EnumAppSide.Server)
                        {
                            // pipe ticks on server only, if we're here we are loading from disk most likely.
                            if (listenerID != 0)
                            {                                
                                // this might require a new listener...
                                bep.RemoveExtractionTickEvent(listenerID);
                            }
                            listenerID = bep.AddExtractionTickEvent(upgrade.Delay, UpdateTick);
                        }
                    }
                }
            }
        }
    }
}
