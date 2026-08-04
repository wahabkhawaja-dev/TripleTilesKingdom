namespace LevelSystem.Generation
{
    /// <summary>
    /// Finds pyramid dimensions whose total tile count is an exact multiple of
    /// MatchCount, so the board never has partial rows / trimmed notches (see
    /// <see cref="LevelDefinitionBuilder"/> for why divisibility matters:
    /// every tile type's count MUST be a multiple of MatchCount or a tile can
    /// end up permanently unmatchable).
    ///
    /// Search strategy: from the requested (baseW, baseH, layers, shrink), try
    /// dimensions within a ±3 window on each axis and layer counts from
    /// <c>layers</c> down to 1. Pick the config with the smallest squared
    /// distance from the request that produces a divisible total — so the final
    /// pyramid stays as close as possible to the designer's intent while still
    /// forming complete grids.
    /// </summary>
    public static class PyramidSizeSolver
    {
        public static bool TrySolve(
            int baseW, int baseH, int layers, int shrink, int matchCount,
            out int outW, out int outH, out int outLayers)
        {
            const int search = 3;

            outW = baseW; outH = baseH; outLayers = layers;
            if (matchCount < 2) matchCount = 2;
            if (shrink < 1) shrink = 1;

            var found = false;
            var bestScore = int.MaxValue;

            for (var dw = 0; dw <= search; dw++)
            {
                for (var dh = 0; dh <= search; dh++)
                {
                    // Prefer growing (positive delta) before shrinking, per the user
                    // rule "25 not divisible by 3 → change to 27, not 24".
                    foreach (var signW in new[] { +1, -1 })
                    {
                        foreach (var signH in new[] { +1, -1 })
                        {
                            var w = baseW + signW * dw;
                            var h = baseH + signH * dh;
                            if (w < 2 || h < 2) continue;

                            for (var l = layers; l >= 1; l--)
                            {
                                var total = PyramidTotal(w, h, l, shrink);
                                if (total <= 0) continue;
                                if (total % matchCount != 0) continue;

                                var layerLoss = layers - l;
                                var growPenalty = (dw + dh) * 2 + (signW < 0 ? 1 : 0) + (signH < 0 ? 1 : 0);
                                var score = dw * dw + dh * dh + layerLoss * layerLoss + growPenalty;
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    outW = w; outH = h; outLayers = l;
                                    found = true;
                                }
                            }
                        }
                    }
                }
            }

            return found;
        }

        public static int PyramidTotal(int w, int h, int layers, int shrink)
        {
            var total = 0;
            for (var i = 0; i < layers; i++)
            {
                var lw = w - i * shrink;
                var lh = h - i * shrink;
                if (lw < 1 || lh < 1) break;
                total += lw * lh;
            }
            return total;
        }
    }
}
