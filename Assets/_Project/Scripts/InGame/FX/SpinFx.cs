using UnityEngine;

namespace MyGame
{
    /// <summary>Constant spin of a visual (tumbling boss rubble, the rotating ring of a landing marker). Visual only.</summary>
    public sealed class SpinFx : MonoBehaviour
    {
        [Tooltip("Degrees per second around each local axis.")]
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 90f, 0f);
        [Tooltip("Random start angle so copies do not spin in sync.")]
        [SerializeField] private bool randomStart = true;

        private void OnEnable()
        {
            if (randomStart)
                transform.localRotation *= Quaternion.Euler(degreesPerSecond.normalized * Random.Range(0f, 360f));
        }

        private void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
