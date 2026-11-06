using UnityEngine;

namespace Walden
{
    public enum InteractionKind { Branch, Water, Stove, Tea, Seat, Bed, Storage, GardenPlot, Fishing, Cooking, Meal }

    public sealed class WaldenInteractable : MonoBehaviour
    {
        public InteractionKind kind;
        public string label;
        public int resourceId;
        public Vector3 seatPosition;
        public float seatYaw;
    }
}
