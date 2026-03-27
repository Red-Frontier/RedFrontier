using System;
using Contracts;
using ContractConfigurator;

namespace RedFrontier
{
    /// <summary>
    /// Factory for <see cref="DeployedScienceStation"/>.
    ///
    /// CC discovers this class automatically at startup by scanning all loaded assemblies
    /// for ParameterFactory subclasses. No registration required. The class name minus
    /// "Factory" maps to the 'type' field used in .cfg files:
    ///     type = DeployedScienceStation
    ///
    /// CONFIG FIELDS:
    ///
    ///   waypointIndex  int     Zero-based index into the contract's WaypointGenerator
    ///                          waypoint list. Must match the order waypoints appear in
    ///                          the BEHAVIOUR block. Default: 0.
    ///
    ///   distance       float   Maximum surface distance in metres from the waypoint
    ///                          centre within which a matching vessel must be found.
    ///                          Default: 500.
    ///
    ///   partName       string  Optional. Internal part name of the deployed instrument
    ///                          to detect (e.g. DeployedSeismicSensor, DeployedSolarPanel).
    ///                          When omitted, the parameter detects the Central Station
    ///                          (VesselType.DeployedScienceController).
    ///                          When set, detects VesselType.DeployedSciencePart vessels
    ///                          whose proto snapshot matches this part name.
    ///                          Use the 'name' field from the part's PART{} config node.
    ///
    ///   define         string  Optional. When set, the matched vessel is registered with
    ///                          ContractVesselTracker under this key on completion.
    ///                          The vessel becomes addressable as @/defineName in DATA
    ///                          blocks and as vessel = defineName in Rendezvous parameters.
    ///                          Omit when vessel identity is not needed downstream.
    ///
    ///   title          string  Optional. Display string in Mission Control.
    ///                          Auto-generated from waypointIndex and partName if omitted.
    ///
    ///   hideChildren   bool    Standard CC field. Use true to suppress sub-parameter
    ///                          display. Recommended for this parameter type.
    ///
    /// EXAMPLE — Central Station detection (standard usage, backward compatible):
    ///
    ///   PARAMETER
    ///   {
    ///       name          = StationAlphaDetected
    ///       type          = DeployedScienceStation
    ///       waypointIndex = 0
    ///       distance      = 2500
    ///       title         = Deploy Experiment Control Station
    ///       hidden        = true
    ///   }
    ///
    /// EXAMPLE — Seismic sensor detection with vessel binding:
    ///
    ///   PARAMETER
    ///   {
    ///       name          = SeismicAlphaDetected
    ///       type          = DeployedScienceStation
    ///       waypointIndex = 0
    ///       distance      = 2500
    ///       partName      = DeployedSeismicSensor
    ///       define        = alphaSeismicSensor
    ///       title         = Deploy Seismic Sensor
    ///       hidden        = true
    ///   }
    ///
    /// EXAMPLE — Multi-instrument outpost constitution check (two parameters, same site):
    ///
    ///   PARAMETER
    ///   {
    ///       name          = ControllerAlpha
    ///       type          = DeployedScienceStation
    ///       waypointIndex = 0
    ///       distance      = 2500
    ///       title         = Deploy Experiment Control Station
    ///       hidden        = true
    ///   }
    ///
    ///   PARAMETER
    ///   {
    ///       name          = SeismicAlpha
    ///       type          = DeployedScienceStation
    ///       waypointIndex = 0
    ///       distance      = 2500
    ///       partName      = DeployedSeismicSensor
    ///       title         = Deploy Seismic Sensor
    ///       hidden        = true
    ///   }
    /// </summary>
    public class DeployedScienceStationFactory : ParameterFactory
    {
        // ── Parsed config values ─────────────────────────────────────────────

        protected int    waypointIndex;
        protected float  distance;
        protected string partName;
        protected string defineVessel;
        // title is inherited from ParameterFactory

        // ── Config loading ───────────────────────────────────────────────────

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            // waypointIndex — which WaypointGenerator waypoint to check against.
            valid = ConfigNodeUtil.ParseValue<int>(
                configNode, "waypointIndex",
                x => waypointIndex = x,
                this, 0) && valid;

            // distance — proximity threshold in metres.
            valid = ConfigNodeUtil.ParseValue<float>(
                configNode, "distance",
                x => distance = x,
                this, 500f) && valid;

            // partName — optional instrument filter. Null means detect Central Station.
            valid = ConfigNodeUtil.ParseValue<string>(
                configNode, "partName",
                x => partName = x,
                this, (string)null) && valid;

            // define — optional vessel binding name for downstream references.
            valid = ConfigNodeUtil.ParseValue<string>(
                configNode, "define",
                x => defineVessel = x,
                this, (string)null) && valid;

            // Validation
            if (distance <= 0f)
            {
                LoggingUtil.LogError(this,
                    $"{ErrorPrefix(configNode)}: distance must be > 0 (got {distance}).");
                valid = false;
            }

            if (waypointIndex < 0)
            {
                LoggingUtil.LogError(this,
                    $"{ErrorPrefix(configNode)}: waypointIndex must be >= 0 (got {waypointIndex}).");
                valid = false;
            }

            // Warn if the named part isn't loaded — catches typos at contract load time.
            // A warning (not error) because the part mod might not be installed in all
            // configurations; the parameter will simply never complete rather than
            // preventing the contract from loading.
            if (!string.IsNullOrEmpty(partName) &&
                PartLoader.getPartInfoByName(partName) == null)
            {
                LoggingUtil.LogWarning(this,
                    $"{ErrorPrefix(configNode)}: partName '{partName}' not found in PartLoader. " +
                    $"Verify the part name matches the 'name' field in the part's PART{{}} config node.");
            }

            return valid;
        }

        // ── Parameter generation ─────────────────────────────────────────────

        public override ContractParameter Generate(Contract contract)
        {
            return new DeployedScienceStation(
                waypointIndex: waypointIndex,
                distance:      distance,
                partName:      partName,
                defineVessel:  defineVessel,
                title:         title);
        }
    }
}
