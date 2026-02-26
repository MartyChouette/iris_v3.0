using UnityEngine;

/// <summary>
/// Static utility that generates procedural grayscale cookie textures for disco ball spotlights.
/// White = light passes through, black = blocked.
/// Uses seeded pseudo-random for deterministic results per pattern.
/// </summary>
public static class DiscoBallCookieGenerator
{
    public static Texture2D Generate(CookiePattern pattern, int resolution)
    {
        resolution = Mathf.Clamp(resolution, 64, 512);
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        // Start with all black (blocked)
        var pixels = new Color[resolution * resolution];

        switch (pattern)
        {
            case CookiePattern.ColorCircles:
                GenerateColorCircles(pixels, resolution);
                break;
            case CookiePattern.MirrorGrid:
                GenerateMirrorGrid(pixels, resolution);
                break;
            case CookiePattern.Pinpoints:
                GeneratePinpoints(pixels, resolution);
                break;
            case CookiePattern.Prism:
                GeneratePrism(pixels, resolution);
                break;
        }

        // Apply circular vignette so edges fade out (cookies need dark borders)
        ApplyVignette(pixels, resolution);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void GenerateColorCircles(Color[] pixels, int res)
    {
        // ~12 randomly-seeded circles with soft falloff edges
        var rng = new System.Random(42);
        int circleCount = Mathf.RoundToInt(12f * res / 256f);
        float minRadius = 15f * res / 256f;
        float maxRadius = 30f * res / 256f;

        var circles = new Vector3[circleCount]; // x, y, radius
        for (int i = 0; i < circleCount; i++)
        {
            float cx = (float)(rng.NextDouble() * 0.7 + 0.15) * res;
            float cy = (float)(rng.NextDouble() * 0.7 + 0.15) * res;
            float r = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
            circles[i] = new Vector3(cx, cy, r);
        }

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float brightness = 0f;
                for (int i = 0; i < circleCount; i++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(circles[i].x, circles[i].y));
                    float falloff = 1f - Mathf.Clamp01((dist - circles[i].z * 0.7f) / (circles[i].z * 0.3f));
                    brightness = Mathf.Max(brightness, falloff);
                }
                pixels[y * res + x] = new Color(brightness, brightness, brightness, 1f);
            }
        }
    }

    private static void GenerateMirrorGrid(Color[] pixels, int res)
    {
        // 8x8 grid of small squares, slightly jittered, sharp edges
        var rng = new System.Random(77);
        float cellSize = res / 8f;
        float squareSize = cellSize * 0.3f;

        for (int gy = 0; gy < 8; gy++)
        {
            for (int gx = 0; gx < 8; gx++)
            {
                float cx = (gx + 0.5f) * cellSize + (float)(rng.NextDouble() - 0.5) * cellSize * 0.2f;
                float cy = (gy + 0.5f) * cellSize + (float)(rng.NextDouble() - 0.5) * cellSize * 0.2f;
                float half = squareSize * 0.5f;

                int minX = Mathf.Max(0, Mathf.FloorToInt(cx - half));
                int maxX = Mathf.Min(res - 1, Mathf.CeilToInt(cx + half));
                int minY = Mathf.Max(0, Mathf.FloorToInt(cy - half));
                int maxY = Mathf.Min(res - 1, Mathf.CeilToInt(cy + half));

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        pixels[y * res + x] = Color.white;
                    }
                }
            }
        }
    }

    private static void GeneratePinpoints(Color[] pixels, int res)
    {
        // ~200 tiny dots, hash-scattered, sharp edges
        var rng = new System.Random(123);
        int dotCount = Mathf.RoundToInt(200f * res / 256f);
        float minRadius = 1f * res / 256f;
        float maxRadius = 3f * res / 256f;

        for (int i = 0; i < dotCount; i++)
        {
            float cx = (float)rng.NextDouble() * res;
            float cy = (float)rng.NextDouble() * res;
            float r = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);

            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r));
            int maxX = Mathf.Min(res - 1, Mathf.CeilToInt(cx + r));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r));
            int maxY = Mathf.Min(res - 1, Mathf.CeilToInt(cy + r));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (dist <= r)
                        pixels[y * res + x] = Color.white;
                }
            }
        }
    }

    private static void GeneratePrism(Color[] pixels, int res)
    {
        // 6-8 radial rays from center, varying width, soft gradient edges
        var rng = new System.Random(99);
        int rayCount = 6 + (int)(rng.NextDouble() * 3); // 6-8 rays
        float center = res * 0.5f;

        float[] angles = new float[rayCount];
        float[] widths = new float[rayCount];
        for (int i = 0; i < rayCount; i++)
        {
            angles[i] = (float)(i * 360.0 / rayCount + (rng.NextDouble() - 0.5) * 15.0);
            widths[i] = 8f + (float)rng.NextDouble() * 12f; // degrees of arc width
            widths[i] *= res / 256f;
        }

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float pixelAngle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float distFromCenter = Mathf.Sqrt(dx * dx + dy * dy);

                float brightness = 0f;
                for (int i = 0; i < rayCount; i++)
                {
                    float angleDiff = Mathf.DeltaAngle(pixelAngle, angles[i]);
                    float halfWidth = widths[i] * 0.5f;
                    float falloff = 1f - Mathf.Clamp01((Mathf.Abs(angleDiff) - halfWidth * 0.5f) / (halfWidth * 0.5f));
                    // Fade in from center
                    float radialFade = Mathf.Clamp01(distFromCenter / (res * 0.15f));
                    brightness = Mathf.Max(brightness, falloff * radialFade);
                }
                pixels[y * res + x] = new Color(brightness, brightness, brightness, 1f);
            }
        }
    }

    private static void ApplyVignette(Color[] pixels, int res)
    {
        float center = res * 0.5f;
        float radius = center * 0.9f;
        float fadeWidth = center * 0.1f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float vignette = 1f - Mathf.Clamp01((dist - radius) / fadeWidth);
                int idx = y * res + x;
                Color c = pixels[idx];
                pixels[idx] = new Color(c.r * vignette, c.g * vignette, c.b * vignette, 1f);
            }
        }
    }
}
