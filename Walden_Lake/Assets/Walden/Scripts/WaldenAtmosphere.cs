using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Walden
{
    /// <summary>Self-contained sky, daylight and quiet, generated nature sounds.</summary>
    public sealed class WaldenAtmosphere : MonoBehaviour
    {
        private const int SampleRate = 22050;

        private readonly List<AudioClip> generatedClips = new List<AudioClip>();
        private readonly Dictionary<InteractionKind, AudioClip> cues = new Dictionary<InteractionKind, AudioClip>();
        private readonly AudioClip[] birdCalls = new AudioClip[3];
        private Camera view;
        private Light sun;
        private Light moon;
        private Material sky;
        private AudioSource natureSource;
        private AudioSource fireSource;
        private AudioSource birdSource;
        private AudioSource cueSource;
        private float volume = .72f;
        private float birdTimer = 5f;
        private float fireLevel;
        private float daylight;
        private int birdIndex;
        private bool initialized;
        private bool audioPaused;

        public void Initialize(Camera camera)
        {
            if (initialized) return;
            initialized = true;
            view = camera;

            sun = MakeLight("Walden sunlight", new Color(1f, .88f, .65f), 1.25f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .68f;
            sun.shadowBias = .055f;
            sun.shadowNormalBias = .35f;
            sun.shadowResolution = LightShadowResolution.Medium;
            RenderSettings.sun = sun;

            moon = MakeLight("Quiet evening fill", new Color(.48f, .65f, .86f), .12f);
            moon.transform.rotation = Quaternion.Euler(42f, -75f, 0f);
            moon.shadows = LightShadows.None;

            Material skyTemplate = Resources.Load<Material>("WaldenSky");
            Shader skyShader = skyTemplate != null ? skyTemplate.shader : Shader.Find("Skybox/Procedural");
            if (skyShader != null && skyShader.isSupported)
            {
                // The Resources asset explicitly retains the procedural sky shader in player builds.
                sky = skyTemplate != null ? new Material(skyTemplate) : new Material(skyShader);
                sky.name = "Walden changing sky";
                sky.SetFloat("_SunDisk", 2f);
                sky.DisableKeyword("_SUNDISK_NONE");
                sky.DisableKeyword("_SUNDISK_SIMPLE");
                sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
                sky.SetFloat("_SunSize", .035f);
                sky.SetFloat("_SunSizeConvergence", 5f);
                sky.SetFloat("_AtmosphereThickness", 1.12f);
                RenderSettings.skybox = sky;
                if (view != null) view.clearFlags = CameraClearFlags.Skybox;
            }
            else if (view != null)
            {
                view.clearFlags = CameraClearFlags.SolidColor;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 52f;
            RenderSettings.fogEndDistance = 200f;

            natureSource = MakeSource("Wind and lakeshore", false);
            natureSource.clip = MakeNature();
            natureSource.loop = true;
            natureSource.Play();

            fireSource = MakeSource("Stove crackle", true);
            fireSource.clip = MakeFire();
            fireSource.loop = true;
            fireSource.minDistance = 1.5f;
            fireSource.maxDistance = 19f;
            fireSource.Play();

            birdSource = MakeSource("Woodland birds", true);
            birdSource.spatialBlend = .4f;
            birdSource.minDistance = 5f;
            birdSource.maxDistance = 38f;
            for (int i = 0; i < birdCalls.Length; i++) birdCalls[i] = MakeBird(i);

            cueSource = MakeSource("Small daily sounds", false);
            foreach (InteractionKind kind in Enum.GetValues(typeof(InteractionKind)))
                cues[kind] = MakeCue(kind);

            SetVolume(volume);
            Apply(8f, false, Vector3.zero);
        }

        public void Apply(float hour, bool fireBurning, Vector3 stovePosition)
        {
            if (!initialized) return;

            hour = Mathf.Clamp(hour, 6f, 22f);
            float sunProgress = (hour - 6f) / 14f;
            daylight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(sunProgress * Mathf.PI) * 1.2f));
            float warm = 1f - Mathf.Clamp01(Mathf.Abs(hour - 17.8f) / 3.5f);
            float morningWarm = 1f - Mathf.Clamp01(Mathf.Abs(hour - 6.6f) / 2.6f);
            float gold = Mathf.Max(warm, morningWarm * .65f);
            Color daySky = Color.Lerp(new Color(.59f, .72f, .77f), new Color(.83f, .66f, .47f), gold * .5f);
            Color nightSky = new Color(.13f, .21f, .29f);
            Color horizon = Color.Lerp(nightSky, daySky, daylight);

            sun.transform.rotation = Quaternion.Euler(sunProgress * 180f, -32f, 0f);
            sun.intensity = 1.45f * Mathf.Sqrt(daylight);
            sun.color = Color.Lerp(new Color(1f, .96f, .85f), new Color(1f, .64f, .33f), gold * .78f);
            moon.intensity = Mathf.Lerp(.16f, .025f, daylight);

            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.19f, .27f, .36f), new Color(.66f, .77f, .83f), daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.15f, .20f, .24f), new Color(.57f, .62f, .55f), daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.13f, .17f, .14f), new Color(.39f, .43f, .30f), daylight);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = Mathf.Lerp(.2f, .65f, daylight);
            RenderSettings.fogColor = horizon;
            if (view != null) view.backgroundColor = horizon;

            if (sky != null)
            {
                sky.SetColor("_SkyTint", Color.Lerp(new Color(.2f, .29f, .44f), new Color(.54f, .63f, .70f), daylight));
                sky.SetColor("_GroundColor", Color.Lerp(new Color(.14f, .18f, .19f), new Color(.47f, .49f, .39f), daylight));
                sky.SetFloat("_Exposure", Mathf.Lerp(.24f, 1.15f, daylight));
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.04f, 1.24f, gold));
            }

            fireSource.transform.position = stovePosition;
            fireLevel = Mathf.MoveTowards(fireLevel, fireBurning ? 1f : 0f, Time.unscaledDeltaTime * 1.8f);
            fireSource.volume = volume * .42f * fireLevel;
            birdSource.volume = volume * .34f * Mathf.Lerp(.2f, 1f, daylight);
        }

        public void PlayCue(InteractionKind kind)
        {
            if (initialized && cues.TryGetValue(kind, out AudioClip clip))
                cueSource.PlayOneShot(clip);
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (!initialized || natureSource == null) return;
            natureSource.volume = volume * .58f;
            fireSource.volume = volume * .42f * fireLevel;
            birdSource.volume = volume * .34f;
            cueSource.volume = volume * .48f;
        }

        private void Update()
        {
            if (!initialized) return;
            bool pause = Time.timeScale <= 0f;
            if (pause != audioPaused)
            {
                audioPaused = pause;
                PauseSource(natureSource, pause);
                PauseSource(fireSource, pause);
                PauseSource(birdSource, pause);
                PauseSource(cueSource, pause);
            }
            if (pause) return;

            birdTimer -= Time.deltaTime;
            if (birdTimer > 0f) return;
            birdTimer = UnityEngine.Random.Range(7f, 15f);
            if (daylight < .08f || view == null) return;
            float angle = birdIndex * 2.399963f;
            birdSource.transform.position = view.transform.position + new Vector3(Mathf.Cos(angle) * 11f, 5f, Mathf.Sin(angle) * 11f);
            birdSource.pitch = UnityEngine.Random.Range(.94f, 1.08f);
            birdSource.PlayOneShot(birdCalls[birdIndex % birdCalls.Length]);
            birdIndex++;
        }

        private static void PauseSource(AudioSource source, bool pause)
        {
            if (pause) source.Pause();
            else source.UnPause();
        }

        private Light MakeLight(string title, Color color, float intensity)
        {
            GameObject obj = new GameObject(title);
            obj.transform.SetParent(transform, false);
            Light light = obj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            return light;
        }

        private AudioSource MakeSource(string title, bool spatial)
        {
            GameObject obj = new GameObject(title);
            obj.transform.SetParent(transform, false);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.priority = spatial ? 180 : 128;
            return source;
        }

        private AudioClip Clip(string title, float[] samples, int channels)
        {
            AudioClip clip = AudioClip.Create(title, samples.Length / channels, channels, SampleRate, false);
            clip.SetData(samples, 0);
            generatedClips.Add(clip);
            return clip;
        }

        private AudioClip MakeNature()
        {
            const float duration = 18f;
            int frames = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[frames * 2];
            var random = new System.Random(71321);
            float wind = 0f;
            float water = 0f;
            float ripple = 0f;
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                wind += (noise - wind) * .008f;
                water += (noise - water) * .075f;
                ripple += (noise - ripple) * .28f;
                float gust = .65f + .17f * Mathf.Sin(t * .47f) + .12f * Mathf.Sin(t * 1.2f);
                float shore = .55f + .3f * Mathf.Sin(t * 1.05f + .6f) + .12f * Mathf.Sin(t * 2.45f);
                float fade = Mathf.Clamp01(Mathf.Min(t, duration - t) / .22f);
                float broad = wind * .48f * gust + water * .15f * shore;
                data[i * 2] = (broad + ripple * .025f * (.6f + .4f * Mathf.Sin(t * .81f))) * fade;
                data[i * 2 + 1] = (broad + ripple * .025f * (.6f + .4f * Mathf.Cos(t * .93f))) * fade;
            }
            return Clip("Wind in leaves and small lake waves", data, 2);
        }

        private AudioClip MakeFire()
        {
            const float duration = 9f;
            float[] data = new float[(int)(duration * SampleRate)];
            var random = new System.Random(9182);
            float rumble = 0f;
            float crackle = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                rumble += (noise - rumble) * .035f;
                if (random.NextDouble() < .00075) crackle = (float)random.NextDouble() * .28f;
                crackle *= .975f;
                float fade = Mathf.Clamp01(Mathf.Min(t, duration - t) / .12f);
                data[i] = (rumble * .32f + noise * crackle) * fade;
            }
            return Clip("Small wood stove", data, 1);
        }

        private AudioClip MakeBird(int variant)
        {
            float[] data = new float[(int)(1.8f * SampleRate)];
            int notes = variant == 1 ? 4 : 3;
            for (int note = 0; note < notes; note++)
            {
                float start = .08f + note * (.27f + variant * .03f);
                float length = .13f + (note % 2) * .035f;
                float phase = 0f;
                int first = (int)(start * SampleRate);
                int last = Mathf.Min(data.Length, first + (int)(length * SampleRate));
                for (int i = first; i < last; i++)
                {
                    float u = (i - first) / (length * SampleRate);
                    float frequency = 2100f + variant * 360f + note * 120f + 760f * Mathf.Sin(u * Mathf.PI);
                    phase += frequency * (2f * Mathf.PI / SampleRate);
                    float envelope = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.4f);
                    data[i] += (Mathf.Sin(phase) + .18f * Mathf.Sin(phase * 2.02f)) * envelope * .13f;
                }
            }
            return Clip("Distant bird " + variant, data, 1);
        }

        private AudioClip MakeCue(InteractionKind kind)
        {
            float[] data = new float[(int)(.8f * SampleRate)];
            var random = new System.Random(2300 + (int)kind);
            float low = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += (noise - low) * .12f;
                float sample;
                switch (kind)
                {
                    case InteractionKind.Branch:
                        sample = noise * Mathf.Exp(-t * 43f) * .3f + low * Mathf.Exp(-Mathf.Abs(t - .11f) * 50f) * .17f;
                        break;
                    case InteractionKind.Water:
                        sample = Mathf.Sin(2f * Mathf.PI * (430f * t - 180f * t * t)) * Mathf.Exp(-t * 13f) * .15f;
                        sample += low * Mathf.Exp(-Mathf.Abs(t - .19f) * 9f) * .3f;
                        break;
                    case InteractionKind.Stove:
                        sample = low * Mathf.Sin(Mathf.Min(1f, t * 2f) * Mathf.PI) * Mathf.Exp(-t * 3f) * .55f;
                        break;
                    case InteractionKind.Tea:
                        sample = (Mathf.Sin(t * 2f * Mathf.PI * 1680f) + .3f * Mathf.Sin(t * 2f * Mathf.PI * 2370f)) * Mathf.Exp(-t * 19f) * .10f;
                        sample += low * Mathf.Exp(-Mathf.Abs(t - .30f) * 20f) * .10f;
                        break;
                    case InteractionKind.Bed:
                        sample = low * Mathf.Sin(Mathf.Min(1f, t / .8f) * Mathf.PI) * .17f;
                        break;
                    case InteractionKind.Seat:
                        sample = low * Mathf.Exp(-t * 10f) * .26f;
                        break;
                    default:
                        sample = (Mathf.Sin(t * 2f * Mathf.PI * 190f) + low) * Mathf.Exp(-t * 28f) * .15f;
                        break;
                }
                data[i] = sample * Mathf.Clamp01(t * 900f);
            }
            return Clip("Daily action " + kind, data, 1);
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in generatedClips)
                if (clip != null) Destroy(clip);
            if (sky != null)
            {
                if (RenderSettings.skybox == sky) RenderSettings.skybox = null;
                Destroy(sky);
            }
        }
    }
}
