using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>Deterministic daily-schedule generation shared by the generator (initial villagers) and the routine (newborns, career changes).</summary>
    public static class VillageSchedules
    {
        public static uint Next(ref uint rng)
        {
            // xorshift32
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return rng;
        }

        public static int Rand(ref uint rng, int minInclusive, int maxInclusive)
        {
            return minInclusive + (int)(Next(ref rng) % (uint)(maxInclusive - minInclusive + 1));
        }

        public static bool Chance(ref uint rng, int percent) => (int)(Next(ref rng) % 100) < percent;

        public static void Assign(VillagerProfile p, ref uint rng)
        {
            int wake, start, lunchChance, lunchAt, lunchLen, end, sleep, leisureChance;
            switch (p.Job)
            {
                case VillageJob.Monk:
                    wake = Rand(ref rng, 4 * 60 + 45, 5 * 60 + 15); start = wake + 30; lunchChance = 0; lunchAt = 12 * 60; lunchLen = 45;
                    end = Rand(ref rng, 18 * 60, 19 * 60); sleep = Rand(ref rng, 20 * 60, 20 * 60 + 30); leisureChance = 20; break;
                case VillageJob.Student:
                    wake = Rand(ref rng, 7 * 60, 8 * 60); start = wake + Rand(ref rng, 45, 60); lunchChance = 50; lunchAt = Rand(ref rng, 12 * 60, 12 * 60 + 30); lunchLen = 45;
                    end = Rand(ref rng, 15 * 60 + 30, 16 * 60 + 30); sleep = Rand(ref rng, 21 * 60, 22 * 60); leisureChance = 90; break;
                case VillageJob.Blacksmith:
                case VillageJob.Merchant:
                    wake = Rand(ref rng, 6 * 60 + 30, 7 * 60 + 30); start = wake + Rand(ref rng, 45, 75); lunchChance = 70; lunchAt = Rand(ref rng, 12 * 60, 13 * 60); lunchLen = Rand(ref rng, 45, 75);
                    end = Rand(ref rng, 17 * 60 + 30, 18 * 60 + 30); sleep = Rand(ref rng, 21 * 60 + 30, 22 * 60 + 30); leisureChance = 60; break;
                case VillageJob.Cook:
                    wake = Rand(ref rng, 4 * 60 + 30, 5 * 60); start = wake + 30; lunchChance = 0; lunchAt = 12 * 60; lunchLen = 30;
                    end = Rand(ref rng, 20 * 60 + 30, 21 * 60); sleep = Rand(ref rng, 21 * 60 + 30, 22 * 60); leisureChance = 10; break;
                case VillageJob.Guard:
                    wake = Rand(ref rng, 6 * 60, 7 * 60); start = wake + 30; lunchChance = 50; lunchAt = Rand(ref rng, 12 * 60, 13 * 60); lunchLen = 45;
                    end = Rand(ref rng, 19 * 60, 20 * 60); sleep = Rand(ref rng, 22 * 60, 23 * 60); leisureChance = 30; break;
                default: // outdoor gatherers
                    wake = Rand(ref rng, 5 * 60 + 30, 6 * 60 + 30); start = wake + Rand(ref rng, 30, 60); lunchChance = 60; lunchAt = Rand(ref rng, 11 * 60 + 45, 12 * 60 + 30); lunchLen = Rand(ref rng, 45, 60);
                    end = Rand(ref rng, 17 * 60, 18 * 60); sleep = Rand(ref rng, 20 * 60 + 30, 21 * 60 + 30); leisureChance = 40; break;
            }

            // Trait offsets: night owls shift everything an hour later, early birds an hour earlier.
            int shift = p.Has(Trait.NightOwl) ? 60 : p.Has(Trait.EarlyBird) ? -60 : 0;
            wake = Mathf.Clamp(wake + shift, 3 * 60, 10 * 60);
            start = Mathf.Max(start + shift, wake + 20);
            sleep = Mathf.Clamp(sleep + shift, 19 * 60, VillageClock.MinutesPerDay - 1);
            if (end + shift > sleep - 60) end = sleep - 60; else end += shift;

            p.WakeMinute = wake;
            p.WorkStartMinute = start;
            p.TakesLunch = Chance(ref rng, lunchChance);
            p.LunchStartMinute = Mathf.Max(lunchAt, start + 60);
            p.LunchEndMinute = Mathf.Min(p.LunchStartMinute + lunchLen, end - 60);
            if (p.LunchEndMinute <= p.LunchStartMinute) p.TakesLunch = false;
            p.WorkEndMinute = end;
            p.SleepMinute = Mathf.Min(sleep, VillageClock.MinutesPerDay - 1);
            p.EveningLeisure = Chance(ref rng, leisureChance);
        }
    }
}
