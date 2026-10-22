using Bonkadoo.Application;
using UnityEngine;

namespace Bonkadoo.Presentation
{
    /// <summary>Milliseconds, offset past the restored save so ticks stay monotonic across sessions.</summary>
    public sealed class UnityTickSource : ITickSource
    {
        private readonly long _offset;
        public UnityTickSource(long lastTick) => _offset = lastTick + 1;
        public long Now => _offset + (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}
