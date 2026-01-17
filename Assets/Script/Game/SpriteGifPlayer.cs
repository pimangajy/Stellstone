using UnityEngine;
using System.Collections;
using System.Linq;

public class SpriteGifPlayer : MonoBehaviour
{
    public enum ScaleType
    {
        Stretch,    // 1. 강제로 늘려서 꽉 채움 (비율 깨짐)

        FitInside,  // 2. 비율 유지하며 안에 쏙 들어감 (여백 생김) 
                    // -> [필드 카드용]: 이미지가 잘리면 안 되고 전체가 다 보여야 할 때 사용

        Cover       // 3. 비율 유지하며 꽉 채움 (넘치는 부분 발생) 
                    // -> [손패 카드용]: 프레임을 꽉 채우고 넘치는 부분은 마스크로 자를 때 사용
    }

    [Header("설정")]
    public Sprite[] gifFrames;
    public float framesPerSecond = 10.0f;

    [Header("크기 자동 조절")]
    public bool autoFitSize = true;
    public ScaleType scaleType = ScaleType.Cover; // 기본값을 손패용(Cover)으로 변경

    [Tooltip("맞추고 싶은 영역의 크기 (Quad의 크기)")]
    public Vector2 targetSize = new Vector2(1.0f, 1.5f);

    public SpriteRenderer spriteRenderer;
    private Coroutine playCoroutine;

    void Awake()
    {
        // spriteRenderer = GetComponent<SpriteRenderer>();
    }


    void OnEnable()
    {
        if (gifFrames != null && gifFrames.Length > 0)
        {
            framesPerSecond = gifFrames.Count();
            if (autoFitSize) FitSpriteToSize();
            PlayAnimation();
        }
    }

    void OnDisable()
    {
        StopAnimation();
    }

    // 핵심: 스케일 계산 로직
    void FitSpriteToSize()
    {
        if (spriteRenderer == null || gifFrames.Length == 0) return;

        spriteRenderer.sprite = gifFrames[0];
        // 로컬 스케일을 초기화해야 정확한 사이즈 계산이 가능
        transform.localScale = Vector3.one;

        Vector3 spriteSize = spriteRenderer.bounds.size;

        // 현재 이미지와 목표 크기의 비율 계산
        float ratioX = targetSize.x / spriteSize.x;
        float ratioY = targetSize.y / spriteSize.y;

        float finalScaleX = 1f;
        float finalScaleY = 1f;

        switch (scaleType)
        {
            case ScaleType.Stretch:
                // 무조건 목표 크기에 맞춤 (비율 깨짐)
                finalScaleX = ratioX;
                finalScaleY = ratioY;
                break;

            case ScaleType.FitInside:
                // [필드용] 둘 중 더 작은 비율 선택 (이미지가 잘리지 않고 전체가 보임)
                float minRatio = Mathf.Min(ratioX, ratioY);
                finalScaleX = minRatio;
                finalScaleY = minRatio;
                break;

            case ScaleType.Cover:
                // [손패용] 둘 중 더 큰 비율 선택 (빈 공간 없이 꽉 채우고, 튀어나간 부분은 마스크로 자름)
                float maxRatio = Mathf.Max(ratioX, ratioY);
                finalScaleX = maxRatio;
                finalScaleY = maxRatio;
                break;
        }

        transform.localScale = new Vector3(finalScaleX, finalScaleY, 1f);
    }

    // ... (애니메이션 재생 로직은 동일)
    void PlayAnimation()
    {
        StopAnimation();
        playCoroutine = StartCoroutine(PlayGifRoutine());
    }

    void StopAnimation()
    {
        StopAllCoroutines();
        playCoroutine = null;
    }

    IEnumerator PlayGifRoutine()
    {
        int index = 0;
        float waitTime = 1f / framesPerSecond;

        while (true)
        {
            if (spriteRenderer != null && gifFrames.Length > 0)
            {
                spriteRenderer.sprite = gifFrames[index];
                index = (index + 1) % gifFrames.Length;
            }
            yield return new WaitForSeconds(waitTime);
        }
    }

    public void SetGif(Sprite[] newFrames, float speed = 10.0f)
    {
        this.gifFrames = newFrames;
        this.framesPerSecond = speed;
        if (gameObject.activeInHierarchy)
        {
            if (autoFitSize) FitSpriteToSize();
            PlayAnimation();
        }
    }
}