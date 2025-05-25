using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Core.Constants
{
    public static class ResilienceConstants
    {
        public const int MaxDatabaseRetries = 3;
        public const int MaxHttpRetries = 3;
        public const int CircuitBreakerThreshold = 5;
    }
}
