using System;
using UnityEngine;

namespace Gunspire
{
    /// <summary>Why shillings arrived. Only kills are scaled by <see cref="Attr.ShillingGain"/>.</summary>
    public enum ShillingSource { Kill, Elite, Boss, Prop, Pickpocket, Gilded, Interest, Other }

    /// <summary>
    /// The run's shillings. Whole coins only: a fractional amount, such as a bonus on a one-shilling drop,
    /// is carried over to the next earning rather than lost, so small bonuses still pay out over time.
    /// </summary>
    public class Wallet
    {
        public int Balance { get; private set; }

        /// <summary>Shillings earned over the run, for the summary.</summary>
        public int TotalEarned { get; private set; }

        private float _carry;

        /// <summary>Whole shillings added, and why.</summary>
        public event Action<int, ShillingSource> Earned;
        public event Action<int> Spent;
        public event Action Changed;

        /// <summary>Adds an amount, carrying the fraction. Returns the whole shillings added.</summary>
        public int Earn(float amount, ShillingSource source)
        {
            if (amount <= 0f) return 0;

            float total = amount + _carry;
            int whole = Mathf.FloorToInt(total + 0.0001f);
            _carry = Mathf.Max(0f, total - whole);
            if (whole <= 0) return 0;

            Balance += whole;
            TotalEarned += whole;
            Earned?.Invoke(whole, source);
            Changed?.Invoke();
            return whole;
        }

        /// <summary>Takes an amount if there is enough, for the shop. Refuses, taking nothing, otherwise.</summary>
        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (Balance < amount) return false;

            Balance -= amount;
            Spent?.Invoke(amount);
            Changed?.Invoke();
            return true;
        }

        public static bool IsKill(ShillingSource source)
            => source == ShillingSource.Kill || source == ShillingSource.Elite || source == ShillingSource.Boss;
    }
}
