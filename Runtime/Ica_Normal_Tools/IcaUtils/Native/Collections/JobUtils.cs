using System.Runtime.CompilerServices;

namespace Ica.Utils
{
    public static class JobUtils
    {
        /// <summary>
        /// Returns a batch count that makes sense to author.
        /// </summary>
        /// <param name="iterationCount"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBatchCountThatMakesSense(int iterationCount)
        {
            // best gaming cpu's thread count  = 32
            float num = (float)iterationCount / 32;
            return (int)System.Math.Ceiling(num);
        }
    }
}