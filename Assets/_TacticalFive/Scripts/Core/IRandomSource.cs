/// <summary>Fuente de aleatoriedad inyectable para simulaciones reproducibles.</summary>
public interface IRandomSource
{
    /// <summary>Valor uniforme en [0, 1).</summary>
    float Value { get; }

    /// <summary>Entero uniforme en [minInclusive, maxExclusive).</summary>
    int Range(int minInclusive, int maxExclusive);

    /// <summary>Valor uniforme en [minInclusive, maxInclusive].</summary>
    float Range(float minInclusive, float maxInclusive);
}

/// <summary>Adaptador del juego: conserva la fuente global de Unity.</summary>
public sealed class UnityRandomSource : IRandomSource
{
    public static readonly UnityRandomSource Instance = new();

    UnityRandomSource() { }

    public float Value => UnityEngine.Random.value;
    public int Range(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
    public float Range(float minInclusive, float maxInclusive) => UnityEngine.Random.Range(minInclusive, maxInclusive);
}

/// <summary>
/// Generador SplitMix64 con estado local. No depende de Unity ni de System.Random, por lo
/// que una semilla produce la misma secuencia entre ejecuciones y runtimes compatibles.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    ulong _state;

    public SeededRandomSource(ulong seed) => _state = seed;

    ulong NextUInt64()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public float Value => (NextUInt64() >> 40) * (1f / 16777216f);

    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        uint span = (uint)(maxExclusive - minInclusive);
        ulong bound = span;
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do value = NextUInt64(); while (value < threshold);
        return minInclusive + (int)(value % bound);
    }

    public float Range(float minInclusive, float maxInclusive)
        => minInclusive + (maxInclusive - minInclusive) * Value;
}
