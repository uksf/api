using System;

namespace UKSF.Api.Base.Services {
    public interface IClock {
        public DateTime Now();
        public DateTime Today();
        public DateTime UtcNow();
    }

    public class Clock : IClock {
        public DateTime Now() => DateTime.Now;
        public DateTime Today() => DateTime.Today;
        public DateTime UtcNow() => DateTime.UtcNow;
    }
}
