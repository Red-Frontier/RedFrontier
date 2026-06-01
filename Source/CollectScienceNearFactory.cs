// CollectScienceNearFactory.cs
// Red Frontier - ParameterFactory for CollectScienceNear
//
// CC discovers this factory by reflection at startup. Class name minus "Factory"
// becomes the 'type' value in .cfg files: type = CollectScienceNear
//
// .cfg usage:
//
//   PARAMETER
//   {
//       name          = ScanGreenSandstone
//       type          = CollectScienceNear
//       experimentID  = ROCScience_MinmusGreenSandstone
//       waypointIndex = 1        // zero-based; matches WaypointGenerator order
//       distance      = 1000     // metres; should match your VisitWaypoint distance
//       title         = Scan Green Sandstone formation
//       optional      = true
//   }
//
// Place inside an All container alongside VisitWaypoint and Duration.
// Do NOT place inside a VesselParameterGroup.

using ContractConfigurator;
using Contracts;

namespace RedFrontier.ContractParameters
{
    public class CollectScienceNearFactory : ParameterFactory
    {
        private string experiment;
        private int    waypointIndex;
        private double distance;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<string>(configNode, "experiment",
                x => experiment = x, this, string.Empty);

            if (string.IsNullOrEmpty(experiment))
            {
                LoggingUtil.LogError(this, "{0}: CollectScienceNear requires 'experiment'.",
                    new object[] { ErrorPrefix(configNode) });
                valid = false;
            }

            valid &= ConfigNodeUtil.ParseValue<int>(configNode, "waypointIndex",
                x => waypointIndex = x, this, 0,
                x => Validation.GE(x, 0));

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "distance",
                x => distance = x, this, 500.0,
                x => Validation.GT(x, 0.0));

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new CollectScienceNear(
                experiment:  experiment,
                waypointIndex: waypointIndex,
                distance:      distance,
                title:         title
            );
        }
    }
}
