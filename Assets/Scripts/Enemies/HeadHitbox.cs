using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Marks a collider as an enemy's head. A gun round that strikes it deals <see cref="Combat.HeadshotMultiplier"/>
    /// times its damage. It sits on the hitbox layer, which collides with nothing and is left out of blasts and
    /// overlaps, so only shots ever find it.
    /// </summary>
    public class HeadHitbox : MonoBehaviour
    {
    }
}
