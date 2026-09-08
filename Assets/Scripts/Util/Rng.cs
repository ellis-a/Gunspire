using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Seeded random. A run keeps one instance so a seed reproduces the same tower,
    /// the same boon offers and the same room layouts.
    /// </summary>
    public class Rng
    {
        private System.Random _random;

        public int Seed { get; private set; }

        public Rng(int seed)
        {
            Seed = seed;
            _random = new System.Random(seed);
        }

        public void Reseed(int seed)
        {
            Seed = seed;
            _random = new System.Random(seed);
        }

        /// <summary>Uniform in [0,1).</summary>
        public float Value => (float)_random.NextDouble();

        public float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>Uniform integer in [min,max).</summary>
        public int Range(int min, int max) => max <= min ? min : _random.Next(min, max);

        public bool Chance(float probability) => Value < probability;

        public int Sign() => Value < 0.5f ? -1 : 1;

        public T Pick<T>(IList<T> list) => list == null || list.Count == 0 ? default : list[Range(0, list.Count)];

        public Vector3 OnUnitCircle()
        {
            float a = Range(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        public Vector3 InsideCircle(float radius)
        {
            return OnUnitCircle() * (Mathf.Sqrt(Value) * radius);
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>Takes up to <paramref name="count"/> distinct entries without disturbing the source list.</summary>
        public List<T> TakeDistinct<T>(IList<T> source, int count)
        {
            var pool = new List<T>(source);
            Shuffle(pool);
            if (pool.Count > count) pool.RemoveRange(count, pool.Count - count);
            return pool;
        }
    }
}
