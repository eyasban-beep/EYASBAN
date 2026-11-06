using UnityEngine;

namespace Closet.Game
{
    /// <summary>
    /// 아무 씬에서나 Play 버튼을 누르면 게임이 자동으로 시작되게 해주는 장치.
    /// 씬에 따로 오브젝트를 배치할 필요가 없다.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<GameRunner>() != null) return;

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.1f, 0.16f);
            }

            var go = new GameObject("GameRunner");
            go.AddComponent<GameRunner>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
