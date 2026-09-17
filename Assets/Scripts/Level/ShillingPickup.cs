using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A shilling on the floor. It pops out, settles for a moment, then flies to the player and is
    /// collected. Whatever is still lying around when the player takes the exit is banked there, so a
    /// coin is never lost to the room being torn down.
    /// </summary>
    public class ShillingPickup : MonoBehaviour
    {
        /// <summary>How long a coin rests before flying to the player.</summary>
        public const float SettleSeconds = 1f;
        public const float FlySpeed = 18f;
        public const float CollectDistance = 0.9f;

        /// <summary>At most this many coins per drop; a large drop is shared between them.</summary>
        public const int MaxCoinsPerDrop = 5;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        private static readonly List<ShillingPickup> LiveList = new List<ShillingPickup>();

        public float Amount;
        public ShillingSource Source;

        private float _age;
        private Vector3 _velocity;
        private bool _collected;

        public static IReadOnlyList<ShillingPickup> Live
        {
            get
            {
                LiveList.RemoveAll(p => p == null);
                return LiveList;
            }
        }

        /// <summary>Drops an amount as a few coins around a point.</summary>
        public static void Scatter(Vector3 position, float amount, ShillingSource source)
        {
            if (amount <= 0f) return;

            int coins = Mathf.Clamp(Mathf.CeilToInt(amount), 1, MaxCoinsPerDrop);
            for (int i = 0; i < coins; i++)
            {
                Vector3 kick = Quaternion.Euler(0f, i * 360f / coins, 0f) * Vector3.forward * 2.2f + Vector3.up * 4f;
                Spawn(position, amount / coins, source, kick);
            }
        }

        public static ShillingPickup Spawn(Vector3 position, float amount, ShillingSource source, Vector3 kick)
        {
            GameObject go = Build.Sphere(null, "Shilling", position, 0.22f, MaterialLibrary.Emissive(Gold, 2.5f), collider: false);
            go.layer = Layers.Prop;

            var coin = go.AddComponent<ShillingPickup>();
            coin.Amount = amount;
            coin.Source = source;
            coin._velocity = kick;
            LiveList.Add(coin);
            return coin;
        }

        /// <summary>Banks every coin still out into a run. The exit calls this before the room goes.</summary>
        public static int BankAll(RunState run)
        {
            int total = 0;
            var coins = new List<ShillingPickup>(Live);
            for (int i = 0; i < coins.Count; i++) total += coins[i].Collect(run);
            return total;
        }

        private void OnDestroy() => LiveList.Remove(this);

        private void Update()
        {
            float dt = WorldClock.DeltaTime;
            _age += dt;

            PlayerRig rig = PlayerRig.Instance;
            if (_age < SettleSeconds || rig == null)
            {
                // A short hop that settles: gravity, and the ground as a floor.
                _velocity += Vector3.down * 18f * dt;
                Vector3 next = transform.position + _velocity * dt;
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit ground, 0.3f + Mathf.Max(0f, -_velocity.y * dt),
                        Layers.WorldMask, QueryTriggerInteraction.Ignore) && _velocity.y < 0f)
                {
                    next.y = ground.point.y + 0.25f;
                    _velocity = Vector3.zero;
                }
                transform.position = next;
                return;
            }

            Vector3 target = rig.transform.position + Vector3.up * 0.9f;
            Vector3 toTarget = target - transform.position;
            if (toTarget.magnitude <= CollectDistance)
            {
                Collect(RunState.Current);
                return;
            }

            transform.position += toTarget.normalized * Mathf.Min(toTarget.magnitude, FlySpeed * dt);
        }

        /// <summary>Adds this coin to a run and removes it. Returns the whole shillings that added.</summary>
        public int Collect(RunState run)
        {
            if (_collected) return 0;
            _collected = true;

            int earned = run != null ? run.EarnShillings(Amount, Source) : 0;
            LiveList.Remove(this);

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return earned;
        }
    }
}
