// HasInventoryPart.cs
// Red Frontier - Custom Contract Configurator VesselParameter
//
// Checks whether a vessel is carrying specific parts inside ModuleInventoryPart
// cargo containers, complementing PartValidation which only sees attached parts.
//
// CC discovers HasInventoryPartFactory by reflection at startup and maps the
// type name "HasInventoryPart" in .cfg files to this parameter class.
// No manual registration required — shipping the DLL in GameData/ is sufficient.

using System;
using System.Text;
using ContractConfigurator;
using ContractConfigurator.Parameters;

namespace RedFrontier.ContractParameters
{
    public class HasInventoryPart : VesselParameter
    {
        // All fields populated by HasInventoryPartFactory.Generate(),
        // then persisted/restored via OnParameterSave / OnParameterLoad.
        protected string partName = null;
        protected string partModule = null;
        protected int minCount = 1;
        protected int maxCount = int.MaxValue;

        // -----------------------------------------------------------------------
        // Constructors
        // -----------------------------------------------------------------------

        // No-arg: called by CC framework when loading from save file.
        public HasInventoryPart() : base() { }

        // Full constructor: called by HasInventoryPartFactory.Generate().
        public HasInventoryPart(
            string partName,
            string partModule,
            int minCount,
            int maxCount,
            string title)
            : base(title)
        {
            this.partName = partName;
            this.partModule = partModule;
            this.minCount = minCount;
            this.maxCount = maxCount;
        }

        // -----------------------------------------------------------------------
        // Persistence (save file round-trip — separate from config parsing)
        // Config parsing is handled entirely by HasInventoryPartFactory.Load().
        // -----------------------------------------------------------------------

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            if (partName != null) node.AddValue("part", partName);
            if (partModule != null) node.AddValue("partModule", partModule);
            node.AddValue("minCount", minCount);
            if (maxCount != int.MaxValue) node.AddValue("maxCount", maxCount);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            // Three-arg overload: (node, key, defaultValue) — safe for save-file
            // loading where missing keys must fall back gracefully.
            partName = ConfigNodeUtil.ParseValue<string>(node, "part", (string)null);
            partModule = ConfigNodeUtil.ParseValue<string>(node, "partModule", (string)null);
            minCount = ConfigNodeUtil.ParseValue<int>(node, "minCount", 1);
            maxCount = ConfigNodeUtil.ParseValue<int>(node, "maxCount", int.MaxValue);
        }

        // -----------------------------------------------------------------------
        // Title (auto-generated fallback when no 'title' field in .cfg)
        // -----------------------------------------------------------------------

        protected override string GetParameterTitle()
        {
            StringBuilder sb = new StringBuilder();

            if (partName != null)
            {
                AvailablePart ap = PartLoader.getPartInfoByName(partName);
                sb.Append(ap != null ? ap.title : partName);
            }
            else
            {
                sb.Append(partModule);
            }

            if (minCount == maxCount)
                sb.Append($" x{minCount} (in cargo)");
            else if (maxCount == int.MaxValue)
                sb.Append(minCount > 1 ? $" x{minCount}+ (in cargo)" : " (in cargo)");
            else
                sb.Append($" x{minCount}-{maxCount} (in cargo)");

            return sb.ToString();
        }

        // -----------------------------------------------------------------------
        // Condition check — pure predicate, no side effects
        // -----------------------------------------------------------------------

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (vessel == null) return false;
            int total = CountMatchingInventoryParts(vessel);
            return total >= minCount && total <= maxCount;
        }

        // -----------------------------------------------------------------------
        // Inventory scanning
        // -----------------------------------------------------------------------

        private int CountMatchingInventoryParts(Vessel vessel)
        {
            int total = 0;
            foreach (Part part in vessel.parts)
                foreach (ModuleInventoryPart inv in part.Modules.GetModules<ModuleInventoryPart>())
                    total += CountMatchingInInventory(inv);
            return total;
        }

        private int CountMatchingInInventory(ModuleInventoryPart inventory)
        {
            if (inventory.storedParts == null) return 0;

            int count = 0;
            foreach (StoredPart sp in inventory.storedParts.Values)
            {
                if (sp == null) continue;

                bool matches = (partName != null)
                    ? string.Equals(sp.partName, partName, StringComparison.OrdinalIgnoreCase)
                    : StoredPartHasModule(sp, partModule);

                if (matches) count += sp.quantity;
            }
            return count;
        }

        // Stored parts are serialized data records, not live GameObjects.
        // We check module membership via the AvailablePart prefab in PartLoader.
        // Accepts short class name ("ModuleGroundExpControl") or fully qualified.
        private static bool StoredPartHasModule(StoredPart sp, string moduleName)
        {
            AvailablePart ap = PartLoader.getPartInfoByName(sp.partName);
            if (ap?.partPrefab == null) return false;

            foreach (PartModule m in ap.partPrefab.Modules)
            {
                if (m == null) continue;
                Type t = m.GetType();
                if (t.Name == moduleName || t.FullName == moduleName)
                    return true;
            }
            return false;
        }
    }
}
