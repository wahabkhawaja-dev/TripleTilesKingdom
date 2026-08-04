using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// World-space, ParticleSystem-based sparkle burst. Replaces the uGUI-only
    /// <see cref="UIParticleBurst"/> for the sprite gameplay layer: takes a world
    /// position and emits N particles outward in a small radial burst using the
    /// theme's <c>ParticleSprites</c> for variety. Singleton — one persistent
    /// ParticleSystem is created on first use and reused for every burst.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldParticleBurst : MonoBehaviour
    {
        private const int SortingOrder = 4000; // Above flying tiles (3000) and tray (2000).

        private static WorldParticleBurst _instance;

        private ParticleSystem _system;
        private ParticleSystem.EmitParams _emitParams;

        public static void BurstSparkle(
            Vector3 worldPos,
            int count = 12,
            float duration = 0.5f,
            float distance = 0.8f,
            Color? tint = null)
        {
            var inst = GetOrCreate();
            inst.Burst(worldPos, count, duration, distance, tint ?? Color.white);
        }

        private static WorldParticleBurst GetOrCreate()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("WorldParticleBurst");
            _instance = go.AddComponent<WorldParticleBurst>();
            _instance.BuildSystem();
            return _instance;
        }

        private void BuildSystem()
        {
            _system = gameObject.AddComponent<ParticleSystem>();
            _system.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);

            var main = _system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 2f;
            main.startSize = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = 512;

            var emission = _system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var sizeOverLifetime = _system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = _system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var velocityOverLifetime = _system.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(0f, 2f);

            var renderer = _system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = SortingOrder;
            renderer.material = BuildSpriteMaterial();

            var theme = Resources.Load<TileThemeSO>("TileTheme_Default");
            if (theme != null && theme.ParticleSprites != null && theme.ParticleSprites.Length > 0)
            {
                var tsa = _system.textureSheetAnimation;
                tsa.enabled = true;
                tsa.mode = ParticleSystemAnimationMode.Sprites;
                for (var i = 0; i < theme.ParticleSprites.Length; i++)
                {
                    tsa.AddSprite(theme.ParticleSprites[i]);
                }
            }
        }

        /// <summary>
        /// Default Sprites/Default is safe on URP and Built-in for a
        /// billboarded particle sprite — no shader compile step, no keyword
        /// setup, no material asset needed on disk.
        /// </summary>
        private static Material BuildSpriteMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            return new Material(shader) { name = "WorldParticleBurst_Sprites" };
        }

        private void Burst(Vector3 worldPos, int count, float duration, float distance, Color color)
        {
            if (_system == null) return;

            var main = _system.main;
            main.startLifetime = duration;
            main.startColor = color;
            // startSpeed × lifetime is roughly the max travel radius; tune startSpeed
            // to hit the requested distance so the burst reads at the intended scale.
            main.startSpeed = Mathf.Max(0.1f, distance / Mathf.Max(0.01f, duration));

            _emitParams = new ParticleSystem.EmitParams
            {
                position = worldPos,
                applyShapeToPosition = true,
            };

            _system.Emit(_emitParams, count);
        }
    }
}
