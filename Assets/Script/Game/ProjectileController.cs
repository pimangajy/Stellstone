using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 원거리 하수인이 공격할 때 날아가는 투사체(화살, 마법 등)를 제어합니다.
/// 곡선(포물선) 비행과 목표 도달 시 폭발 효과를 담당합니다.
/// [업데이트] 위아래 포물선뿐만 아니라, 좌우(대각선) 포물선 비행 기능이 추가되었습니다.
/// </summary>
public class ProjectileController : MonoBehaviour
{
    [Header("비행 궤적 추가 설정")]
    [Tooltip("좌우로 휘어지는 정도. 양수(+)면 오른쪽, 음수(-)면 왼쪽으로 휘어집니다.")]
    public float horizontalArc = 0f;

    [Header("도착 연출")]
    [Tooltip("목표에 맞았을 때 터질 이펙트 (비워둬도 됨)")]
    public GameObject hitEffectPrefab;

    public void Fire(Vector3 startPos, Vector3 targetPos, float speed, float arcHeight, Action onHitCallback)
    {
        transform.position = startPos;
        StartCoroutine(FlyRoutine(startPos, targetPos, speed, arcHeight, onHitCallback));
    }

    private IEnumerator FlyRoutine(Vector3 start, Vector3 target, float speed, float arcHeight, Action onHit)
    {
        // 두 점 사이의 거리를 기반으로 총 비행 시간 계산
        float distance = Vector3.Distance(start, target);

        // (안전장치) 거리가 너무 가깝거나 속도가 0이면 즉시 도착 처리
        if (distance <= 0.01f || speed <= 0f)
        {
            CompleteFlight(target, onHit);
            yield break;
        }

        float duration = distance / speed;
        float elapsedTime = 0f;

        // 1. 투사체가 날아가는 정면(Forward) 방향 계산
        Vector3 forwardDir = (target - start).normalized;

        // 2. 정면을 기준으로 '오른쪽(Right)' 방향 계산 (수직 위 벡터와 외적)
        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

        // (안전장치) 만약 완벽하게 수직(위/아래)으로 날아가는 경우를 대비
        if (rightDir == Vector3.zero) rightDir = Vector3.right;

        // 이전 위치를 기억해서 투사체가 날아가는 방향을 바라보게 만듭니다.
        Vector3 previousPos = start;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // 1. 직선 위치 계산 (Lerp)
            Vector3 currentPos = Vector3.Lerp(start, target, t);

            // 2. 포물선 곡률 계산 (Mathf.Sin을 이용해 0 -> 1 -> 0 형태의 부드러운 곡선)
            float arcMultiplier = Mathf.Sin(t * Mathf.PI);

            // 3. 위/아래 포물선 적용 (기존 기능)
            currentPos.y += arcMultiplier * arcHeight;

            // 4. [신규] 좌/우 포물선 적용 (대각선 비행)
            currentPos += rightDir * (arcMultiplier * horizontalArc);

            // 5. 투사체가 날아가는 궤적 방향 바라보기
            Vector3 moveDirection = currentPos - previousPos;
            if (moveDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }

            // 위치 적용 및 이전 위치 갱신
            transform.position = currentPos;
            previousPos = currentPos;

            yield return null;
        }

        CompleteFlight(target, onHit);
    }

    /// <summary>
    /// 목표에 도달했을 때의 처리를 담당합니다.
    /// </summary>
    private void CompleteFlight(Vector3 target, Action onHit)
    {
        transform.position = target;

        // 적중 이펙트 소환
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, target, Quaternion.identity);
        }

        // 도달했음을 알림 (데미지 숫자 띄우기, 체력 깎기 등의 타이밍용)
        onHit?.Invoke();

        // 투사체 자신은 파괴
        Destroy(gameObject);
    }
}