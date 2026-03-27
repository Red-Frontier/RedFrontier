// HasInventoryPartFactory.cs
// Red Frontier - Contract Configurator ParameterFactory
//
// CC's architecture splits each parameter type into two classes:
//   Factory (this file): parses .cfg, validates config, constructs the Parameter
//   Parameter (HasInventoryPart.cs): holds runtime state, handles save/load, evaluates condition
//
// CC discovers factories by scanning all loaded assemblies for ParameterFactory
// subclasses at startup. The class name minus "Factory" becomes the 'type' value
// used in .cfg files — so this class maps to: type = HasInventoryPart
//
// .cfg usage:
//
//   PARAMETER
//   {
//       name     = HasCentralStations
//       type     = HasInventoryPart
//       part     = DeployedCentralStation   // exact internal part name
//       minCount = 4
//       title    = Cargo: 4x Experiment Control Station
//   }
//
//   PARAMETER
//   {
//       name       = HasGroundControlHardware
//       type       = HasInventoryPart
//       partModule = ModuleGroundExpControl   // PartModule class name on prefab
//       minCount   = 1
//   }
//
// Fields:
//   part       - Exact internal part name (the 'name' field in PART{} config).
//                Mutually exclusive with partModule.
//   partModule - PartModule class name. Matches any stored part whose prefab has
//                this module. Accepts short or fully-qualified class names.
//                Mutually exclusive with part.
//   minCount   - Minimum total matching parts across all inventories. Default: 1.
//   maxCount   - Maximum total matching parts. Default: unlimited.
//   title      - Optional display override. Auto-generated if omitted.
//
// Complement: PartValidation checks physically attached parts.
//             HasInventoryPart checks parts packed in cargo containers.
//             Use both together for complete pre-departure manifests.

using System;
using ContractConfigurator;
using ContractConfigurator.Parameters;
using Contracts;

namespace RedFrontier.ContractParameters
{
    public class HasInventoryPartFactory : ParameterFactory
    {
        // Fields loaded from config, passed to HasInventoryPart constructor
        protected string partName = null;
        protected string partModule = null;
        protected int minCount = 1;
        protected int maxCount = int.MaxValue;

        // -----------------------------------------------------------------------
        // Load — parse .cfg values and validate
        // 'this' implements IContractConfiguratorFactory (ParameterFactory does),
        // so ParseValue's 4th argument can be 'this' here — not in the Parameter.
        // LoggingUtil.LogError/LogWarning take (object, string, object[]) —
        // the object[] is NOT params/variadic; pass new object[]{...} explicitly.
        // -----------------------------------------------------------------------
        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "part",
                x => partName = x, this, (string)null);

            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "partModule",
                x => partModule = x, this, (string)null);

            // Exactly one identification method required
            if (partName == null && partModule == null)
            {
                LoggingUtil.LogError(this,
                    "{0}: HasInventoryPart requires either 'part' or 'partModule'.",
                    new object[] { ErrorPrefix(configNode) });
                valid = false;
            }
            if (partName != null && partModule != null)
            {
                LoggingUtil.LogError(this,
                    "{0}: HasInventoryPart: specify 'part' or 'partModule', not both.",
                    new object[] { ErrorPrefix(configNode) });
                valid = false;
            }

            // Warn if the named part isn't in the database — typo guard.
            // A warning (not error) because the part mod might not be installed
            // in all configurations; the parameter will simply never complete.
            if (partName != null && PartLoader.getPartInfoByName(partName) == null)
            {
                LoggingUtil.LogWarning(this,
                    "{0}: HasInventoryPart: part '{1}' not found in PartLoader. " +
                    "Verify the part name is correct and the part mod is installed.",
                    new object[] { ErrorPrefix(configNode), partName });
            }

            // Validation.ValidatePartModule checks whether the named class exists
            // as a loaded PartModule type — catches module name typos at load time.
            if (partModule != null && !Validation.ValidatePartModule(partModule))
            {
                LoggingUtil.LogWarning(this,
                    "{0}: HasInventoryPart: partModule '{1}' not found in loaded assemblies. " +
                    "Verify the class name. Will never match if the module mod is not installed.",
                    new object[] { ErrorPrefix(configNode), partModule });
            }

            valid &= ConfigNodeUtil.ParseValue<int>(configNode, "minCount",
                x => minCount = x, this, 1, x => Validation.GE(x, 0));

            valid &= ConfigNodeUtil.ParseValue<int>(configNode, "maxCount",
                x => maxCount = x, this, int.MaxValue, x => Validation.GE(x, 0));

            if (minCount > maxCount)
            {
                LoggingUtil.LogError(this,
                    "{0}: HasInventoryPart: minCount ({1}) cannot exceed maxCount ({2}).",
                    new object[] { ErrorPrefix(configNode), minCount, maxCount });
                valid = false;
            }

            return valid;
        }

        // -----------------------------------------------------------------------
        // Generate — construct the runtime Parameter instance
        // Called once per contract generation; fields from Load() are passed in.
        // -----------------------------------------------------------------------
        public override ContractParameter Generate(Contract contract)
        {
            return new HasInventoryPart(
                partName: partName,
                partModule: partModule,
                minCount: minCount,
                maxCount: maxCount,
                title: title    // 'title' is inherited from ParameterFactory.Load()
            );
        }
    }
}
