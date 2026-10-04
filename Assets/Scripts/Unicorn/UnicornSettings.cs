using System;
using Gtec.Chain.Common.Nodes.FilterNodes;
using Gtec.UnityInterface;
using UnityEngine;

namespace BciChess.Unicorn
{
    /// <summary>Unicorn Hybrid Black / g.tec ERP configuration, tunable in the inspector.</summary>
    [Serializable]
    public sealed class UnicornSettings
    {
        [Tooltip("The g.tec 'BCI Visual ERP 2D' prefab (Assets/g.tec/Unity Interface/Prefabs/BCI). " +
                 "Menu: BCI Chess > Assign Unicorn BCI Prefab.")]
        public GameObject erpPrefab;

        [Tooltip("Unicorn for the real headset; UnicornSimulator to test the pipeline without hardware.")]
        public Device.DeviceType deviceType = Device.DeviceType.Unicorn;

        [Tooltip("How confident the g.tec classifier must be before it reports a selection.")]
        public ERPScoreStatistics.SelectionThreshold selectionThreshold = ERPScoreStatistics.SelectionThreshold.Confidence95;

        [Tooltip("Ignore selections for this long after new targets appear, so evidence gathered for the " +
                 "previous targets cannot select one of the new ones.")]
        [Min(0f)] public float ignoreSelectionsAfterChangeSeconds = 1f;

        [Tooltip("If the Unicorn setup fails (e.g. prefab missing), continue with the simulated BCI.")]
        public bool fallBackToSimulated = true;
    }
}
