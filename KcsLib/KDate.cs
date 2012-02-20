using System;

namespace kcslib
{
    /// <summary>
    /// Date handling methods.
    /// </summary>
    public static class KDate
    {
        /// <summary>
        /// Translate a Teambox data timestamp to a DateTime object in UTC.
        /// </summary>
        public static DateTime KDateToDateTimeUTC(UInt64 _date)
        {
            DateTime date = new DateTime((long)_date * TimeSpan.TicksPerSecond);
            DateTime epochStartTime = Convert.ToDateTime("1/1/1970 00:00:00 AM");
            TimeSpan t = new TimeSpan(date.Ticks + epochStartTime.Ticks);
            return new DateTime(t.Ticks);
        }

        /// <summary>
        /// Translate a Teambox timestamp to a DateTime object in LocalTime.
        /// </summary>
        public static DateTime KDateToDateTime(UInt64 _date)
        {
            return KDateToDateTimeUTC(_date).ToLocalTime();
        }

        /// <summary>
        /// Translate a DateTime date to a Teambox date.
        /// </summary>
        public static UInt64 DateTimeToKDate(DateTime date)
        {
            DateTime epochStartTime = Convert.ToDateTime("1/1/1970 00:00:00 AM");
            TimeSpan t = new TimeSpan(date.Ticks - epochStartTime.Ticks);
            return (UInt64)t.TotalSeconds;
        }
    }
}