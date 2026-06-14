using UnityEngine;

public class ArenaAmbientParticleFx : MonoBehaviour
{
    // Particulas ambientales de arena: solo decoran el mapa y no afectan colisiones ni reglas.
    public enum ParticleStyle
    {
        Lab,
        Storage,
        Rupture
    }

    private struct AmbientParticle
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public Vector2 BaseScale;
        public Color BaseColor;
        public float Phase;
        public float BlinkSpeed;
        public float Jitter;
    }

    private AmbientParticle[] particles;
    private float width;
    private float height;
    private ParticleStyle style;

    public void Configure(float arenaWidth, float arenaHeight, int count, Color primary, Color secondary, ParticleStyle particleStyle)
    {
        width = Mathf.Max(1f, arenaWidth);
        height = Mathf.Max(1f, arenaHeight);
        style = particleStyle;
        count = Mathf.Clamp(count, 0, 72);
        particles = new AmbientParticle[count];

        for (int i = 0; i < count; i++)
        {
            particles[i] = CreateParticle(i, primary, secondary);
        }
    }

    private AmbientParticle CreateParticle(int index, Color primary, Color secondary)
    {
        GameObject particle = new GameObject($"AmbientParticle_{index}");
        particle.transform.SetParent(transform, false);
        particle.transform.localPosition = GetRandomLocalPosition();

        SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
        renderer.sprite = style == ParticleStyle.Storage && index % 5 == 0 ? CircleSpriteProvider.Get() : SquareSpriteProvider.Get();
        renderer.sortingOrder = GetSortingOrder();

        Color color = Color.Lerp(primary, secondary, Random.Range(0f, 1f));
        color.a = GetBaseAlpha();
        Vector2 scale = GetParticleScale(index);
        Vector2 velocity = GetParticleVelocity(index);

        particle.transform.localScale = scale;
        particle.transform.localRotation = Quaternion.Euler(0f, 0f, GetParticleRotation(index));
        renderer.color = color;

        return new AmbientParticle
        {
            Transform = particle.transform,
            Renderer = renderer,
            Velocity = velocity,
            BaseScale = scale,
            BaseColor = color,
            Phase = Random.Range(0f, 12f),
            BlinkSpeed = GetBlinkSpeed(),
            Jitter = style == ParticleStyle.Rupture ? Random.Range(0.006f, 0.032f) : Random.Range(0f, 0.016f)
        };
    }

    private void Update()
    {
        if (particles == null)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            AnimateParticle(ref particles[i], i);
        }
    }

    private void AnimateParticle(ref AmbientParticle particle, int index)
    {
        if (particle.Transform == null || particle.Renderer == null)
        {
            return;
        }

        Vector3 pos = particle.Transform.localPosition;
        pos += (Vector3)(particle.Velocity * Time.deltaTime);

        if (style == ParticleStyle.Rupture)
        {
            float glitchStep = Mathf.Floor((Time.time + particle.Phase) * 7f);
            pos.x += Mathf.Sin(glitchStep * 3.17f + index) * particle.Jitter;
            pos.y += Mathf.Cos(glitchStep * 2.41f + index) * particle.Jitter;
        }

        particle.Transform.localPosition = WrapPosition(pos);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * particle.BlinkSpeed + particle.Phase);
        Color color = particle.BaseColor;
        float maxPulseAlpha = style == ParticleStyle.Rupture ? 1.08f : 1.22f;
        color.a = Mathf.Lerp(particle.BaseColor.a * 0.32f, particle.BaseColor.a * maxPulseAlpha, pulse);
        if (style == ParticleStyle.Rupture && pulse > 0.88f)
        {
            color.a = Mathf.Min(0.34f, color.a * 1.18f);
        }

        particle.Renderer.color = color;

        float scalePulse = style == ParticleStyle.Rupture
            ? Mathf.Lerp(0.86f, 1.10f, pulse)
            : Mathf.Lerp(0.88f, 1.08f, pulse);
        particle.Transform.localScale = new Vector3(particle.BaseScale.x * scalePulse, particle.BaseScale.y, 1f);
    }

    private Vector3 WrapPosition(Vector3 position)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        if (position.x > halfWidth)
        {
            position.x = -halfWidth;
        }
        else if (position.x < -halfWidth)
        {
            position.x = halfWidth;
        }

        if (position.y > halfHeight)
        {
            position.y = -halfHeight;
        }
        else if (position.y < -halfHeight)
        {
            position.y = halfHeight;
        }

        return position;
    }

    private Vector3 GetRandomLocalPosition()
    {
        return new Vector3(
            Random.Range(width * -0.48f, width * 0.48f),
            Random.Range(height * -0.48f, height * 0.48f),
            0f);
    }

    private Vector2 GetParticleScale(int index)
    {
        switch (style)
        {
            case ParticleStyle.Storage:
                return index % 5 == 0
                    ? Vector2.one * Random.Range(0.035f, 0.07f)
                    : new Vector2(Random.Range(0.09f, 0.34f), Random.Range(0.025f, 0.06f));
            case ParticleStyle.Rupture:
                return index % 4 == 0
                    ? new Vector2(Random.Range(0.22f, 0.62f), Random.Range(0.025f, 0.055f))
                    : Vector2.one * Random.Range(0.035f, 0.085f);
            default:
                return index % 3 == 0
                    ? new Vector2(Random.Range(0.24f, 0.58f), Random.Range(0.018f, 0.045f))
                    : Vector2.one * Random.Range(0.035f, 0.07f);
        }
    }

    private Vector2 GetParticleVelocity(int index)
    {
        switch (style)
        {
            case ParticleStyle.Storage:
                return new Vector2(Random.Range(-0.045f, 0.045f), Random.Range(-0.12f, -0.035f));
            case ParticleStyle.Rupture:
                return Random.insideUnitCircle.normalized * Random.Range(0.025f, 0.09f);
            default:
                return index % 2 == 0
                    ? new Vector2(Random.Range(0.08f, 0.22f), 0f)
                    : new Vector2(0f, Random.Range(-0.10f, 0.10f));
        }
    }

    private float GetParticleRotation(int index)
    {
        switch (style)
        {
            case ParticleStyle.Storage:
                return Random.Range(-8f, 8f);
            case ParticleStyle.Rupture:
                return Random.Range(0f, 180f);
            default:
                return index % 2 == 0 ? 0f : 90f;
        }
    }

    private float GetBaseAlpha()
    {
        switch (style)
        {
            case ParticleStyle.Storage:
                return Random.Range(0.08f, 0.18f);
            case ParticleStyle.Rupture:
                return Random.Range(0.07f, 0.18f);
            default:
                return Random.Range(0.07f, 0.17f);
        }
    }

    private float GetBlinkSpeed()
    {
        switch (style)
        {
            case ParticleStyle.Rupture:
                return Random.Range(0.45f, 1.15f);
            case ParticleStyle.Storage:
                return Random.Range(0.55f, 1.35f);
            default:
                return Random.Range(0.6f, 1.5f);
        }
    }

    private int GetSortingOrder()
    {
        return style == ParticleStyle.Rupture ? -3 : -5;
    }
}
