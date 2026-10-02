using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코루틴에서 빈번하게 발생하는 new WaitForSeconds()의 힙 메모리 할당(GC Alloc)을
/// 방지하기 위해 생성된 인스턴스를 캐싱하고 재활용하는 정적 캐시 유틸리티입니다.
/// </summary>
public static class YieldInstructionCache
{
    private class FloatComparer : IEqualityComparer<float>
    {
        public bool Equals(float x, float y)
        {
            return Mathf.Abs(x - y) < 0.0001f;
        }

        public int GetHashCode(float obj)
        {
            return Mathf.RoundToInt(obj * 10000f);
        }
    }

    private static readonly Dictionary<float, WaitForSeconds> _timeInterval = new Dictionary<float, WaitForSeconds>(new FloatComparer());

    /// <summary>
    /// 지정한 시간(초)의 WaitForSeconds 인스턴스를 반환합니다.
    /// 이미 생성된 적이 있다면 캐시된 인스턴스를 재사용하여 GC 할당을 0으로 만듭니다.
    /// </summary>
    public static WaitForSeconds WaitForSeconds(float seconds)
    {
        if (seconds <= 0f) seconds = 0f;

        if (!_timeInterval.TryGetValue(seconds, out var wfs))
        {
            _timeInterval[seconds] = wfs = new WaitForSeconds(seconds);
        }
        return wfs;
    }
}
