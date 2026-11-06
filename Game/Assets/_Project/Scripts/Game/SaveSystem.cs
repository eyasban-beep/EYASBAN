using System;
using System.IO;
using Closet.Core;
using UnityEngine;

namespace Closet.Game
{
    /// <summary>
    /// 로컬 저장. 기기 안의 save.json 파일 하나에 PlayerState 전체를 저장한다.
    /// (온라인 단계(M3)에서 PlayFab 서버 저장으로 교체)
    /// </summary>
    public static class SaveSystem
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static PlayerState LoadOrNew(long nowUnix)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var state = JsonUtility.FromJson<PlayerState>(File.ReadAllText(FilePath));
                    if (state != null)
                    {
                        state.EnsureValid();
                        return state;
                    }
                }
            }
            catch (Exception e)
            {
                // 세이브가 깨졌으면 백업해두고 새로 시작
                Debug.LogWarning("세이브 읽기 실패, 새 게임으로 시작합니다: " + e.Message);
                try { File.Copy(FilePath, FilePath + ".broken", true); } catch { /* 무시 */ }
            }
            long seed = nowUnix ^ Environment.TickCount;
            return PlayerState.NewGame(nowUnix, seed);
        }

        public static void Save(PlayerState state)
        {
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(state));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError("저장 실패: " + e.Message);
            }
        }

        /// <summary>개발용: 세이브 삭제 (처음부터 다시).</summary>
        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
