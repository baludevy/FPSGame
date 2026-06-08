// Decompiled with JetBrains decompiler
// Type: EZCameraShake.CameraShakeInstance
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F4DD6ABB-CD77-4768-BBF0-6F1CFA0BDA0E
// Assembly location: C:\Users\laham\AppData\Local\Temp\Xeniloq\55002bfe4c\Karlson_Data\Managed\Assembly-CSharp.dll

using UnityEngine;

namespace EZCameraShake
{
    public class CameraShakeInstance
    {
        public float Magnitude;
        public float Roughness;
        public Vector3 PositionInfluence;
        public Vector3 RotationInfluence;
        public bool DeleteOnInactive = true;
        private float roughMod = 1f;
        private float magnMod = 1f;
        private float fadeOutDuration;
        private float fadeInDuration;
        private bool sustain;
        private float currentFadeTime;
        private float tick;
        private Vector3 amt;

        public CameraShakeInstance(
          float magnitude,
          float roughness,
          float fadeInTime,
          float fadeOutTime)
        {
            Magnitude = magnitude;
            fadeOutDuration = fadeOutTime;
            fadeInDuration = fadeInTime;
            Roughness = roughness;
            if ((double)fadeInTime > 0.0)
            {
                sustain = true;
                currentFadeTime = 0.0f;
            }
            else
            {
                sustain = false;
                currentFadeTime = 1f;
            }
            tick = (float)Random.Range(-100, 100);
        }

        public CameraShakeInstance(float magnitude, float roughness)
        {
            Magnitude = magnitude;
            Roughness = roughness;
            sustain = true;
            tick = (float)Random.Range(-100, 100);
        }

        public Vector3 UpdateShake()
        {
            amt.x = Mathf.PerlinNoise(tick, 0.0f) - 0.5f;
            amt.y = Mathf.PerlinNoise(0.0f, tick) - 0.5f;
            amt.z = Mathf.PerlinNoise(tick, tick) - 0.5f;
            if ((double)fadeInDuration > 0.0 && sustain)
            {
                if ((double)currentFadeTime < 1.0)
                    currentFadeTime += Time.deltaTime / fadeInDuration;
                else if ((double)fadeOutDuration > 0.0)
                    sustain = false;
            }
            if (!sustain)
                currentFadeTime -= Time.deltaTime / fadeOutDuration;
            if (sustain)
                tick += Time.deltaTime * Roughness * roughMod;
            else
                tick += Time.deltaTime * Roughness * roughMod * currentFadeTime;
            return amt * Magnitude * magnMod * currentFadeTime;
        }

        public void StartFadeOut(float fadeOutTime)
        {
            if ((double)fadeOutTime == 0.0)
                currentFadeTime = 0.0f;
            fadeOutDuration = fadeOutTime;
            fadeInDuration = 0.0f;
            sustain = false;
        }

        public void StartFadeIn(float fadeInTime)
        {
            if ((double)fadeInTime == 0.0)
                currentFadeTime = 1f;
            fadeInDuration = fadeInTime;
            fadeOutDuration = 0.0f;
            sustain = true;
        }

        public float ScaleRoughness
        {
            get => roughMod;
            set => roughMod = value;
        }

        public float ScaleMagnitude
        {
            get => magnMod;
            set => magnMod = value;
        }

        public float NormalizedFadeTime => currentFadeTime;

        private bool IsShaking => (double)currentFadeTime > 0.0 || sustain;

        private bool IsFadingOut => !sustain && (double)currentFadeTime > 0.0;

        private bool IsFadingIn => (double)currentFadeTime < 1.0 && sustain && (double)fadeInDuration > 0.0;

        public CameraShakeState CurrentState
        {
            get
            {
                if (IsFadingIn)
                    return CameraShakeState.FadingIn;
                if (IsFadingOut)
                    return CameraShakeState.FadingOut;
                return IsShaking ? CameraShakeState.Sustained : CameraShakeState.Inactive;
            }
        }
    }
}
