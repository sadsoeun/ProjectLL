namespace MyGame
{
    public static class TimeFormat
    {
        /// <summary>Formats seconds as m:ss (or m:ss.t when withTenths).</summary>
        public static string Format(float seconds, bool withTenths = false)
        {
            if (seconds < 0f || float.IsNaN(seconds))
                seconds = 0f;
            int total = (int)seconds;
            int m = total / 60;
            int s = total % 60;
            if (!withTenths)
                return $"{m}:{s:00}";
            int tenth = (int)((seconds - total) * 10f);
            return $"{m}:{s:00}.{tenth}";
        }
    }
}
