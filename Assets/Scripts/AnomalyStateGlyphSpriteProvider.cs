using System.Collections.Generic;
using UnityEngine;

// Iconos procedurales de una sola tinta. Se dibujan una vez y se reutilizan;
// sirven como firma del estado, no como aviso anticipado de sus ataques.
public static class AnomalyStateGlyphSpriteProvider
{
    private readonly struct Segment
    {
        public readonly Vector2 a;
        public readonly Vector2 b;
        public readonly float width;

        public Segment(float ax, float ay, float bx, float by, float width = 0.105f)
        {
            a = new Vector2(ax, ay);
            b = new Vector2(bx, by);
            this.width = width;
        }
    }

    private static readonly Dictionary<EnemyStatePresentationCatalog.Glyph, Sprite> Cache =
        new Dictionary<EnemyStatePresentationCatalog.Glyph, Sprite>();

    public static Sprite Get(EnemyStatePresentationCatalog.Glyph glyph)
    {
        if (Cache.TryGetValue(glyph, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite sprite = Build(glyph);
        Cache[glyph] = sprite;
        return sprite;
    }

    public static void Warmup()
    {
        foreach (EnemyStatePresentationCatalog.Glyph glyph in
                 System.Enum.GetValues(typeof(EnemyStatePresentationCatalog.Glyph)))
        {
            if (glyph != EnemyStatePresentationCatalog.Glyph.None)
            {
                Get(glyph);
            }
        }
    }

    private static Sprite Build(EnemyStatePresentationCatalog.Glyph glyph)
    {
        const int size = 48;
        List<Segment> segments = new List<Segment>(24);
        BuildSegments(glyph, segments);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = $"AnomalyGlyphTexture_{glyph}"
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(
                    ((x + 0.5f) / size) * 2f - 1f,
                    ((y + 0.5f) / size) * 2f - 1f);
                float coverage = 0f;
                for (int i = 0; i < segments.Count; i++)
                {
                    Segment segment = segments[i];
                    float distance = DistanceToSegment(point, segment.a, segment.b);
                    float edge = 2.6f / size;
                    coverage = Mathf.Max(coverage, 1f - Mathf.SmoothStep(segment.width - edge, segment.width + edge, distance));
                }

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(coverage)));
            }
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.Tight);
        sprite.name = $"AnomalyGlyph_{glyph}";
        return sprite;
    }

    private static void BuildSegments(EnemyStatePresentationCatalog.Glyph glyph, List<Segment> s)
    {
        switch (glyph)
        {
            case EnemyStatePresentationCatalog.Glyph.Split:
                Diamond(s, new Vector2(-0.36f, 0f), 0.28f);
                Diamond(s, new Vector2(0.36f, 0f), 0.28f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Expansion:
                Circle(s, Vector2.zero, 0.25f, 12);
                for (int i = 0; i < 8; i++)
                {
                    Vector2 d = Direction(i, 8);
                    Add(s, d * 0.42f, d * 0.78f);
                }
                break;
            case EnemyStatePresentationCatalog.Glyph.Speed:
                Chevron(s, new Vector2(-0.28f, 0f), 0.48f);
                Chevron(s, new Vector2(0.16f, 0f), 0.48f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Weave:
                Add(s, -0.78f, -0.42f, -0.40f, 0.42f);
                Add(s, -0.40f, 0.42f, 0f, -0.42f);
                Add(s, 0f, -0.42f, 0.40f, 0.42f);
                Add(s, 0.40f, 0.42f, 0.78f, -0.42f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Destroyer:
                Add(s, -0.62f, -0.62f, 0.62f, 0.62f, 0.14f);
                Add(s, -0.62f, 0.62f, 0.62f, -0.62f, 0.14f);
                break;
            case EnemyStatePresentationCatalog.Glyph.PhaseBlink:
                Brackets(s, 0.68f, 0.62f);
                Add(s, -0.18f, 0f, 0.18f, 0f, 0.15f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Pincer:
                ChevronFacing(s, -0.60f, 1f);
                ChevronFacing(s, 0.60f, -1f);
                Add(s, -0.12f, -0.52f, -0.12f, 0.52f);
                Add(s, 0.12f, -0.52f, 0.12f, 0.52f);
                break;
            case EnemyStatePresentationCatalog.Glyph.SignalJam:
                Add(s, -0.72f, 0.42f, -0.18f, 0.42f);
                Add(s, 0.10f, 0.42f, 0.66f, 0.42f);
                Add(s, -0.55f, 0f, 0.08f, 0f);
                Add(s, 0.35f, 0f, 0.72f, 0f);
                Add(s, -0.72f, -0.42f, -0.38f, -0.42f);
                Add(s, -0.10f, -0.42f, 0.58f, -0.42f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Orbit:
                Circle(s, Vector2.zero, 0.55f, 16);
                Circle(s, new Vector2(0.57f, 0f), 0.13f, 8, 0.09f);
                Circle(s, new Vector2(-0.57f, 0f), 0.13f, 8, 0.09f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Replay:
                Arc(s, Vector2.zero, 0.55f, 0.2f, 5.5f, 14);
                Add(s, -0.66f, 0.08f, -0.38f, 0.34f);
                Add(s, -0.66f, 0.08f, -0.30f, 0.02f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Lattice:
                for (int i = -1; i <= 1; i++)
                {
                    float p = i * 0.42f;
                    Add(s, p, -0.65f, p, 0.65f, 0.075f);
                    Add(s, -0.65f, p, 0.65f, p, 0.075f);
                }
                break;
            case EnemyStatePresentationCatalog.Glyph.Desync:
                Brackets(s, 0.62f, 0.54f);
                Add(s, -0.48f, 0.17f, 0.18f, 0.17f);
                Add(s, -0.18f, -0.17f, 0.48f, -0.17f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Recompile:
                Corner(s, -1f, -1f); Corner(s, -1f, 1f); Corner(s, 1f, -1f); Corner(s, 1f, 1f);
                Diamond(s, Vector2.zero, 0.22f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Possession:
                Add(s, -0.72f, 0f, -0.36f, 0.34f);
                Add(s, -0.36f, 0.34f, 0f, 0.46f);
                Add(s, 0f, 0.46f, 0.36f, 0.34f);
                Add(s, 0.36f, 0.34f, 0.72f, 0f);
                Add(s, 0.72f, 0f, 0.36f, -0.34f);
                Add(s, 0.36f, -0.34f, 0f, -0.46f);
                Add(s, 0f, -0.46f, -0.36f, -0.34f);
                Add(s, -0.36f, -0.34f, -0.72f, 0f);
                Circle(s, Vector2.zero, 0.20f, 10, 0.13f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Contract:
                ChevronFacing(s, -0.60f, 1f);
                ChevronFacing(s, 0.60f, -1f);
                Add(s, -0.16f, -0.18f, 0.16f, -0.18f, 0.13f);
                Add(s, 0.16f, -0.18f, 0.16f, 0.18f, 0.13f);
                Add(s, 0.16f, 0.18f, -0.16f, 0.18f, 0.13f);
                Add(s, -0.16f, 0.18f, -0.16f, -0.18f, 0.13f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Adaptive:
                Hexagon(s, 0.64f);
                Add(s, -0.40f, -0.18f, -0.06f, 0.22f, 0.12f);
                Add(s, -0.06f, 0.22f, 0.12f, -0.02f, 0.12f);
                Add(s, 0.12f, -0.02f, 0.44f, 0.34f, 0.12f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Tether:
                Circle(s, new Vector2(-0.42f, 0f), 0.25f, 10);
                Circle(s, new Vector2(0.42f, 0f), 0.25f, 10);
                Add(s, -0.18f, 0f, 0.18f, 0f, 0.14f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Blindspot:
                Add(s, -0.72f, 0f, -0.30f, 0.34f);
                Add(s, -0.30f, 0.34f, 0.30f, 0.34f);
                Add(s, 0.30f, 0.34f, 0.72f, 0f);
                Add(s, 0.72f, 0f, 0.30f, -0.34f);
                Add(s, 0.30f, -0.34f, -0.30f, -0.34f);
                Add(s, -0.30f, -0.34f, -0.72f, 0f);
                Circle(s, Vector2.zero, 0.18f, 10);
                Add(s, -0.68f, -0.64f, 0.68f, 0.64f, 0.14f);
                break;
            case EnemyStatePresentationCatalog.Glyph.Pursuit:
                Chevron(s, Vector2.zero, 0.62f);
                break;
        }
    }

    private static void Add(List<Segment> s, Vector2 a, Vector2 b, float width = 0.105f) => s.Add(new Segment(a.x, a.y, b.x, b.y, width));
    private static void Add(List<Segment> s, float ax, float ay, float bx, float by, float width = 0.105f) => s.Add(new Segment(ax, ay, bx, by, width));

    private static void Chevron(List<Segment> s, Vector2 center, float size)
    {
        Add(s, center + new Vector2(-size * 0.55f, size), center + new Vector2(size * 0.55f, 0f));
        Add(s, center + new Vector2(size * 0.55f, 0f), center + new Vector2(-size * 0.55f, -size));
    }

    private static void ChevronFacing(List<Segment> s, float x, float direction)
    {
        Add(s, x - direction * 0.30f, 0.48f, x + direction * 0.18f, 0f);
        Add(s, x + direction * 0.18f, 0f, x - direction * 0.30f, -0.48f);
    }

    private static void Brackets(List<Segment> s, float x, float y)
    {
        Add(s, -x, -y, -x, y); Add(s, -x, y, -x * 0.55f, y); Add(s, -x, -y, -x * 0.55f, -y);
        Add(s, x, -y, x, y); Add(s, x, y, x * 0.55f, y); Add(s, x, -y, x * 0.55f, -y);
    }

    private static void Diamond(List<Segment> s, Vector2 c, float radius)
    {
        Add(s, c + Vector2.up * radius, c + Vector2.right * radius);
        Add(s, c + Vector2.right * radius, c + Vector2.down * radius);
        Add(s, c + Vector2.down * radius, c + Vector2.left * radius);
        Add(s, c + Vector2.left * radius, c + Vector2.up * radius);
    }

    private static void Hexagon(List<Segment> s, float radius)
    {
        for (int i = 0; i < 6; i++) Add(s, Direction(i, 6) * radius, Direction(i + 1, 6) * radius);
    }

    private static void Circle(List<Segment> s, Vector2 center, float radius, int count, float width = 0.095f)
    {
        for (int i = 0; i < count; i++) Add(s, center + Direction(i, count) * radius, center + Direction(i + 1, count) * radius, width);
    }

    private static void Arc(List<Segment> s, Vector2 center, float radius, float start, float end, int count)
    {
        Vector2 previous = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
        for (int i = 1; i <= count; i++)
        {
            float angle = Mathf.Lerp(start, end, i / (float)count);
            Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Add(s, previous, next);
            previous = next;
        }
    }

    private static void Corner(List<Segment> s, float xSign, float ySign)
    {
        Vector2 corner = new Vector2(xSign * 0.68f, ySign * 0.68f);
        Add(s, corner, new Vector2(xSign * 0.30f, ySign * 0.68f));
        Add(s, corner, new Vector2(xSign * 0.68f, ySign * 0.30f));
    }

    private static Vector2 Direction(int index, int count)
    {
        float angle = index / (float)count * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared <= 0.000001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
        return Vector2.Distance(p, a + ab * t);
    }
}
