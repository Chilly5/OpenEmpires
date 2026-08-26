namespace OpenEmpires.Village
{
    /// <summary>
    /// Pure integer conversion from simulation ticks to village time. The clock is derived
    /// from <see cref="GameSimulation.CurrentTick"/> so it is deterministic and never
    /// drifts from the sim; nothing here is affected by rendering or Time.deltaTime.
    /// </summary>
    public static class VillageClock
    {
        public const int MinutesPerDay = 24 * 60;

        /// <summary>Ticks in one full in-game day. Default 5 minutes at 30 TPS.</summary>
        public static int DayLengthTicks { get; private set; } = 300 * 30;

        /// <summary>Minute of day the simulation starts at (default 06:00).</summary>
        public static int StartMinute { get; private set; } = 6 * 60;

        public static void Configure(int dayLengthTicks, int startMinute)
        {
            DayLengthTicks = System.Math.Max(60, dayLengthTicks);
            StartMinute = ((startMinute % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
        }

        private static long TotalMinutes(int tick)
        {
            return (long)tick * MinutesPerDay / DayLengthTicks + StartMinute;
        }

        /// <summary>0..1439</summary>
        public static int MinuteOfDay(int tick) => (int)(TotalMinutes(tick) % MinutesPerDay);

        /// <summary>1-based day counter.</summary>
        public static int Day(int tick) => (int)(TotalMinutes(tick) / MinutesPerDay) + 1;

        // ------------------------------------------------------------------ seasons
        public enum Season { Spring, Summer, Autumn, Winter }

        /// <summary>Days per season; a year is four seasons.</summary>
        public static int DaysPerSeason { get; set; } = 3;

        public static Season SeasonOf(int tick) => (Season)(((Day(tick) - 1) / DaysPerSeason) % 4);
        public static int Year(int tick) => (Day(tick) - 1) / (DaysPerSeason * 4) + 1;
        /// <summary>0..1 progress through the current season.</summary>
        public static float SeasonFraction(int tick)
        {
            double days = (double)tick * 1 / DayLengthTicks + (double)StartMinute / MinutesPerDay; // fractional days since start
            double inSeason = days % DaysPerSeason;
            return (float)(inSeason / DaysPerSeason);
        }

        public static string SeasonIcon(Season s) => s == Season.Spring ? "✿" : s == Season.Summer ? "☀" : s == Season.Autumn ? "♨" : "❄";

        /// <summary>Fractional day (0..1) with sub-minute precision, for smooth lighting.</summary>
        public static float DayFraction(int tick, float interpolationAlpha = 0f)
        {
            double t = ((double)tick + interpolationAlpha) * MinutesPerDay / DayLengthTicks + StartMinute;
            double m = t % MinutesPerDay;
            return (float)(m / MinutesPerDay);
        }

        public static bool IsNight(int minuteOfDay) => minuteOfDay < 5 * 60 + 30 || minuteOfDay >= 20 * 60 + 30;

        public static string Format(int minuteOfDay)
        {
            int h = minuteOfDay / 60;
            int m = minuteOfDay % 60;
            return $"{h:00}:{m:00}";
        }
    }
}
