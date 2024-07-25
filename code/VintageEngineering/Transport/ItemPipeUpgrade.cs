using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace VintageEngineering.Transport
{
    public class ItemPipeUpgrade: Item
    {
        protected int _delay;
        protected int _rate;
        protected bool _canFilter;
        protected bool _canChangeDistro;

        /// <summary>
        /// Delay in ms for the tick time for this upgrade.<br/>
        /// Minimum value is 100 (1/10th of a second)<br/>
        /// Maximum value is 5000 (5 seconds)
        /// </summary>
        public int Delay
        {
            get
            {
                if (_delay < 100) return 100;
                if (_delay > 5000) return 5000;
                return _delay;
            }
        }
        /// <summary>
        /// Rate of object movement per delay tick for this upgrade.<br/>
        /// A value of -1 means a full stack, regardless of stack-size.<br/>
        /// A value of -2 means everything of a single type in the container (not yet implemented)
        /// </summary>
        public int Rate
        { get { return _rate; } }

        /// <summary>
        /// Does this upgrade allow Filters to be used?
        /// </summary>
        public bool CanFilter
        { get { return _canFilter; } }

        /// <summary>
        /// Does this upgrade allow player to change Distribution method?
        /// </summary>
        public bool CanChangeDistro
        { get { return _canChangeDistro;} }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            // Load variables for upgrades
            _delay = this.Attributes["delay"].AsInt(1000);
            _rate = this.Attributes["rate"].AsInt(1);
            _canFilter = this.Attributes["canfilter"].AsBool(false);
            _canChangeDistro = this.Attributes["changedistro"].AsBool(false);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // TODO:            
            // if right clicking on a pipe extraction node, insert (or swap) the upgrade into the extraction node

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}
