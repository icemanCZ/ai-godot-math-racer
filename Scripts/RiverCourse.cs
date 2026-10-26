using Godot;
using System;

namespace MathRacer;

/// <summary>Choose a smooth lowland route before roads or the riverbed modify the landscape.</summary>
public sealed class RiverCourse
{
    private const float MinX = 95;
    private const float CandidateStep = 4;
    private const int Candidates = 29;
    private readonly float[] centers;
    private readonly float step;

    public RiverCourse(Func<float, float, float> height, Func<float, float> preferred)
    {
        int rows = Mathf.CeilToInt(Settings.WorldRadius * 2 / 8) + 1;
        step = Settings.WorldRadius * 2 / (rows - 1);
        centers = new float[rows];
        var previous = new float[Candidates];
        var parents = new int[rows, Candidates];
        for (int row = 0; row < rows; row++)
        {
            float z = -Settings.WorldRadius + row * step;
            var next = new float[Candidates];
            for (int column = 0; column < Candidates; column++)
            {
                float x = MinX + column * CandidateStep;
                float elevation = Mathf.Max(0, height(x, z) - (-.8f - z * .008f));
                float deviation = x - preferred(z);
                float cost = elevation * elevation * .055f + deviation * deviation * .0007f;
                float best = float.MaxValue;
                for (int source = Math.Max(0, column - 1); source <= Math.Min(Candidates - 1, column + 1); source++)
                {
                    float bend = (source - column) * CandidateStep;
                    float candidate = previous[source] + bend * bend * .035f;
                    if (candidate >= best) continue;
                    best = candidate; parents[row, column] = source;
                }
                next[column] = cost + best;
            }
            previous = next;
        }
        int chosen = 0;
        for (int i = 1; i < Candidates; i++) if (previous[i] < previous[chosen]) chosen = i;
        for (int row = rows - 1; row >= 0; row--)
        {
            centers[row] = MinX + chosen * CandidateStep;
            chosen = parents[row, chosen];
        }
        for (int pass = 0; pass < 12; pass++)
        {
            var smooth = (float[])centers.Clone();
            for (int i = 1; i + 1 < rows; i++) smooth[i] = (centers[i - 1] + centers[i] * 2 + centers[i + 1]) / 4;
            Array.Copy(smooth, centers, rows);
        }
        // Keep bends gradual enough for both river banks and the generated road loops.
        float maxShift = step * .22f;
        for (int i = 1; i < rows; i++) centers[i] = Mathf.Clamp(centers[i], centers[i - 1] - maxShift, centers[i - 1] + maxShift);
        for (int i = rows - 2; i >= 0; i--) centers[i] = Mathf.Clamp(centers[i], centers[i + 1] - maxShift, centers[i + 1] + maxShift);
    }

    public float At(float z)
    {
        float offset = Mathf.Clamp((z + Settings.WorldRadius) / step, 0, centers.Length - 1);
        int index = Math.Min((int)offset, centers.Length - 2);
        return Mathf.Lerp(centers[index], centers[index + 1], offset - index);
    }
}
