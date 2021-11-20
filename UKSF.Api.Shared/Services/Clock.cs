using System;

namespace UKSF.Api.Shared.Services
{
    public interface IClock
    {
        public DateTime Now();
        public DateTime Today();
        public DateTime UtcNow();
    }

    public class Clock : IClock
    {
        public DateTime Now()
        {
            return DateTime.Now;
        }

        public DateTime Today()
        {
            return UtcNow().Date;
        }

        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }
    }
}
