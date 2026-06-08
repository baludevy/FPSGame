// Decompiled with JetBrains decompiler
// Type: EZCameraShake.CameraShaker
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F4DD6ABB-CD77-4768-BBF0-6F1CFA0BDA0E
// Assembly location: C:\Users\laham\AppData\Local\Temp\Xeniloq\55002bfe4c\Karlson_Data\Managed\Assembly-CSharp.dll

using System.Collections.Generic;
using UnityEngine;

namespace EZCameraShake
{
    [AddComponentMenu("EZ Camera Shake/Camera Shaker")]
    public class CameraShaker : MonoBehaviour
    {
        public static CameraShaker Instance;
        private static Dictionary<string, CameraShaker> instanceList = new Dictionary<string, CameraShaker>();
        public Vector3 DefaultPosInfluence = new Vector3(0.15f, 0.15f, 0.15f);
        public Vector3 DefaultRotInfluence = new Vector3(1f, 1f, 1f);
        private Vector3 posAddShake;
        private Vector3 rotAddShake;
        private List<CameraShakeInstance> cameraShakeInstances = new List<CameraShakeInstance>();

        private void Awake()
        {
            CameraShaker.Instance = this;
            CameraShaker.instanceList.Add(gameObject.name, this);
        }

        private void Update()
        {
            posAddShake = Vector3.zero;
            rotAddShake = Vector3.zero;
            for (int index = 0; index < cameraShakeInstances.Count && index < cameraShakeInstances.Count; ++index)
            {
                CameraShakeInstance cameraShakeInstance = cameraShakeInstances[index];
                if (cameraShakeInstance.CurrentState == CameraShakeState.Inactive && cameraShakeInstance.DeleteOnInactive)
                {
                    cameraShakeInstances.RemoveAt(index);
                    --index;
                }
                else if (cameraShakeInstance.CurrentState != CameraShakeState.Inactive)
                {
                    posAddShake += CameraUtilities.MultiplyVectors(cameraShakeInstance.UpdateShake(), cameraShakeInstance.PositionInfluence);
                    rotAddShake += CameraUtilities.MultiplyVectors(cameraShakeInstance.UpdateShake(), cameraShakeInstance.RotationInfluence);
                }
            }
            transform.localPosition = posAddShake;
            transform.localEulerAngles = rotAddShake;
        }

        public static CameraShaker GetInstance(string name)
        {
            CameraShaker cameraShaker;
            return CameraShaker.instanceList.TryGetValue(name, out cameraShaker) ? cameraShaker : (CameraShaker)null;
        }

        public CameraShakeInstance Shake(CameraShakeInstance shake)
        {
            cameraShakeInstances.Add(shake);
            return shake;
        }

        public CameraShakeInstance ShakeOnce(
          float magnitude,
          float roughness,
          float fadeInTime,
          float fadeOutTime)
        {
            CameraShakeInstance cameraShakeInstance = new CameraShakeInstance(magnitude, roughness, fadeInTime, fadeOutTime);
            cameraShakeInstance.PositionInfluence = DefaultPosInfluence;
            cameraShakeInstance.RotationInfluence = DefaultRotInfluence;
            cameraShakeInstances.Add(cameraShakeInstance);
            return cameraShakeInstance;
        }

        public CameraShakeInstance ShakeOnce(
          float magnitude,
          float roughness,
          float fadeInTime,
          float fadeOutTime,
          Vector3 posInfluence,
          Vector3 rotInfluence)
        {

            CameraShakeInstance cameraShakeInstance = new CameraShakeInstance(magnitude, roughness, fadeInTime, fadeOutTime);
            cameraShakeInstance.PositionInfluence = posInfluence;
            cameraShakeInstance.RotationInfluence = rotInfluence;
            cameraShakeInstances.Add(cameraShakeInstance);
            return cameraShakeInstance;
        }

        public CameraShakeInstance StartShake(
          float magnitude,
          float roughness,
          float fadeInTime)
        {
            CameraShakeInstance cameraShakeInstance = new CameraShakeInstance(magnitude, roughness);
            cameraShakeInstance.PositionInfluence = DefaultPosInfluence;
            cameraShakeInstance.RotationInfluence = DefaultRotInfluence;
            cameraShakeInstance.StartFadeIn(fadeInTime);
            cameraShakeInstances.Add(cameraShakeInstance);
            return cameraShakeInstance;
        }

        public CameraShakeInstance StartShake(
          float magnitude,
          float roughness,
          float fadeInTime,
          Vector3 posInfluence,
          Vector3 rotInfluence)
        {
            CameraShakeInstance cameraShakeInstance = new CameraShakeInstance(magnitude, roughness);
            cameraShakeInstance.PositionInfluence = posInfluence;
            cameraShakeInstance.RotationInfluence = rotInfluence;
            cameraShakeInstance.StartFadeIn(fadeInTime);
            cameraShakeInstances.Add(cameraShakeInstance);
            return cameraShakeInstance;
        }

        public List<CameraShakeInstance> ShakeInstances => new List<CameraShakeInstance>((IEnumerable<CameraShakeInstance>)cameraShakeInstances);

        private void OnDestroy() => CameraShaker.instanceList.Remove(gameObject.name);
    }
}
