// ResourceTransferFactory.cs
// Red Frontier - Contract Configurator ParameterFactory
//
// Parses ResourceTransfer parameter config nodes and constructs ResourceTransfer
// instances. Follows the same Factory/Parameter split used by HasInventoryPart.
//
// CC maps this factory to: type = ResourceTransfer
// (class name minus "Factory" = cfg type name)

using System;
using ContractConfigurator;
using ContractConfigurator.Parameters;
using Contracts;

namespace RedFrontier.ContractParameters
{
    public class ResourceTransferFactory : ParameterFactory
    {
        // Parsed config values passed to ResourceTransfer constructor
        protected string resource    = null;
        protected float  deltaMin    = float.NegativeInfinity;
        protected float  deltaMax    = float.PositiveInfinity;
        protected float  fractionMin = float.NegativeInfinity;
        protected float  fractionMax = float.PositiveInfinity;

        // -----------------------------------------------------------------------
        // Load — parse and validate .cfg fields
        // 'this' satisfies IContractConfiguratorFactory here (not in the Parameter).
        // ParseValue's 4th argument must be 'this' in the Factory.
        // -----------------------------------------------------------------------

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            // resource is required
            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "resource", x => resource = x, this);

            // Delta fields — optional, default to unconstrained
            valid &= ConfigNodeUtil.ParseValue<float>(
                configNode, "deltaMin", x => deltaMin = x, this, float.NegativeInfinity);
            valid &= ConfigNodeUtil.ParseValue<float>(
                configNode, "deltaMax", x => deltaMax = x, this, float.PositiveInfinity);

            // Fraction fields — optional, validated to [0, 1] range
            valid &= ConfigNodeUtil.ParseValue<float>(
                configNode, "fractionMin", x => fractionMin = x, this, float.NegativeInfinity,
                x => Validation.Between(x, 0f, 1f));
            valid &= ConfigNodeUtil.ParseValue<float>(
                configNode, "fractionMax", x => fractionMax = x, this, float.PositiveInfinity,
                x => Validation.Between(x, 0f, 1f));

            // Sanity check: at least one constraint must be specified
            if (float.IsNegativeInfinity(deltaMin)    &&
                float.IsPositiveInfinity(deltaMax)    &&
                float.IsNegativeInfinity(fractionMin) &&
                float.IsPositiveInfinity(fractionMax))
            {
                LoggingUtil.LogError(this, "ResourceTransfer: at least one of deltaMin, deltaMax, " +
                    "fractionMin, or fractionMax must be specified.");
                valid = false;
            }

            // deltaMin <= deltaMax if both specified
            if (!float.IsNegativeInfinity(deltaMin) && !float.IsPositiveInfinity(deltaMax)
                && deltaMin > deltaMax)
            {
                LoggingUtil.LogError(this, $"ResourceTransfer: deltaMin ({deltaMin}) must be " +
                    $"<= deltaMax ({deltaMax}).");
                valid = false;
            }

            // fractionMin <= fractionMax if both specified
            if (!float.IsNegativeInfinity(fractionMin) && !float.IsPositiveInfinity(fractionMax)
                && fractionMin > fractionMax)
            {
                LoggingUtil.LogError(this, $"ResourceTransfer: fractionMin ({fractionMin}) must be " +
                    $"<= fractionMax ({fractionMax}).");
                valid = false;
            }

            return valid;
        }

        // -----------------------------------------------------------------------
        // Generate — construct parameter instance with loaded values
        // -----------------------------------------------------------------------

        public override ContractParameter Generate(Contract contract)
        {
            return new ResourceTransfer(
                resource,
                deltaMin,
                deltaMax,
                fractionMin,
                fractionMax,
                title
            );
        }
    }
}
