using UnityEngine;

namespace HighwaySurfing.Output
{
    /// <summary>Enable this GameObject to replace the Spout feed with a solid projection guide.</summary>
    [DisallowMultipleComponent]
    public sealed class ProjectionDebugView : MonoBehaviour
    {
        [SerializeField, ColorUsage(false)]
        [Tooltip("Solid output color while this GameObject and component are enabled.")]
        private Color color = Color.green;

        public Color OutputColor => new Color(color.r, color.g, color.b, 1f);
    }
}
