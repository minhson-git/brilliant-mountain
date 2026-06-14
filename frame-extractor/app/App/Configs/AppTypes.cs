using IOCore.Base;
using IOCore.Gens;
using System.Collections.Generic;
using System.Linq;

namespace IOApp.Configs
{
    public class AppTypes
    {
        public enum IntervalType
        {
            Time,
            Frames,
        }

        public static readonly Dictionary<IntervalType, L> INTERVAL_TYPES = new()
        {
            { IntervalType.Time,        L.Time     },
            { IntervalType.Frames,      L.Frames   },
        };

        public static readonly IntervalType[] IntervalTypeArray = [.. INTERVAL_TYPES.Select(i => i.Key)];
    }
}