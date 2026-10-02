using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 수면(ZZZ) 연출에서 스폰되어 날아가는 개별 3D 파티클 컴포넌트입니다.
/// 기본 큐브 메쉬로 시작하며, 필요 시 MeshFilter의 메쉬를 3D 모델로 교체할 수 있습니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ZAnimationParticle : MonoBehaviour
{
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;

    private Sequence _activeSequence;
    private Action<ZAnimationParticle> _onCompleteCallback;

    private void Awake()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// 파티클 애니메이션을 시작합니다.
    /// </summary>
    public void PlayAnimation(Vector3 startPos, Vector3 targetPos, float duration, Vector3 startScale, Vector3 endScale, Action<ZAnimationParticle> onComplete)
    {
        _onCompleteCallback = onComplete;

        StopAndReset();

        transform.localPosition = startPos;
        transform.localScale = startScale;
        gameObject.SetActive(true);

        _activeSequence = DOTween.Sequence();

        // 1. 이동 트윈 (startPos -> targetPos)
        _activeSequence.Join(transform.DOLocalMove(targetPos, duration).SetEase(Ease.OutSine));

        // 2. 크기 트윈: 점진적으로 커지다가 마지막 구간에서 소멸
        float growDuration = duration * 0.75f;
        float shrinkDuration = duration * 0.25f;

        _activeSequence.Join(transform.DOScale(endScale, growDuration).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                transform.DOScale(Vector3.zero, shrinkDuration).SetEase(Ease.InQuad);
            }));

        // 3. 종료 시 풀 반환
        _activeSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            _onCompleteCallback?.Invoke(this);
        });
    }

    /// <summary>
    /// 실행 중인 애니메이션을 즉시 중단하고 오브젝트를 비활성화합니다.
    /// </summary>
    public void StopAndReset()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
            _activeSequence = null;
        }

        transform.DOKill();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopAndReset();
    }
}
