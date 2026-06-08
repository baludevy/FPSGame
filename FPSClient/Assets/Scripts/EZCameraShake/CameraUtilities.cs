// Decompiled with JetBrains decompiler
// Type: EZCameraShake.CameraUtilities
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F4DD6ABB-CD77-4768-BBF0-6F1CFA0BDA0E
// Assembly location: C:\Users\laham\AppData\Local\Temp\Xeniloq\55002bfe4c\Karlson_Data\Managed\Assembly-CSharp.dll

using UnityEngine;

namespace EZCameraShake
{
    public class CameraUtilities
    {
        public static Vector3 SmoothDampEuler(
          Vector3 current,
          Vector3 target,
          ref Vector3 velocity,
          float smoothTime)
        {
            Vector3 vector3;
            vector3.x = Mathf.SmoothDampAngle(current.x, target.x, ref velocity.x, smoothTime);
            vector3.y = Mathf.SmoothDampAngle(current.y, target.y, ref velocity.y, smoothTime);
            vector3.z = Mathf.SmoothDampAngle(current.z, target.z, ref velocity.z, smoothTime);
            return vector3;
        }

        public static Vector3 MultiplyVectors(Vector3 v, Vector3 w)
        {
            v.x *= w.x;
            v.y *= w.y;
            v.z *= w.z;
            return v;
        }
    }
}
