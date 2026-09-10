using System.Collections.Generic;
using Fragua.Imaging.ImageTrace.Vectorization.Points;

namespace Fragua.Imaging.ImageTrace.Vectorization.TraceTypes
{
    internal class InterpolationPointPath
    {
        public IReadOnlyList<InterpolationPoint> Points { get; set; }
    }
}
