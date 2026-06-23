using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.UI;

/// <summary>
/// 여러 장의 스프라이트(이미지)를 연속으로 보여줘서
/// 마치 GIF처럼 움직이는 그림을 만들어주는 스크립트입니다.
/// </summary>
public class SpriteGifPlayer : MonoBehaviour
{
    // 이미지를 어떻게 채울지 결정하는 옵션
    public enum ScaleType
    {
        Stretch,    // 찌그러져도 꽉 채움
        FitInside,  // 비율 유지하며 안에 쏙 (여백 생김 - 필드 카드용)
        Cover       // 비율 유지하며 꽉 채움 (잘림 - 손패 카드용)
    }

    [Header("설정")]
    public bool feildCard;
    public Sprite[] gifFrames; // 프레임 이미지들
    public float framesPerSecond = 10.0f; // 1초에 몇 장 보여줄지

    [Header("크기 자동 조절")]
    public bool autoFitSize = true;
    public ScaleType scaleType = ScaleType.Cover;
    public Vector2 targetSize = new Vector2(1.0f, 1.5f); // 목표 크기

    public Image image;  // ui용
    public SpriteRenderer spriteRenderer;  //  필드 용
    private Coroutine playCoroutine;

    void Awake()
    {
        // spriteRenderer = GetComponent<SpriteRenderer>(); // 필요하면 주석 해제
    }

    void OnEnable()
    {
        if (gifFrames != null && gifFrames.Length > 0)
        {
            // 프레임 수만큼 속도 자동 조절 (선택사항)
            framesPerSecond = gifFrames.Count();
            if (autoFitSize) FitSpriteToSize();
            if(!feildCard) PlayAnimation();
        }
    }

    void OnDisable()
    {
        StopAnimation();
    }

    // 이미지를 목표 크기에 맞게 스케일 조절
    void FitSpriteToSize()
    {
        if (spriteRenderer == null || gifFrames.Length == 0) return;

        // 첫 프레임 적용
        spriteRenderer.sprite = gifFrames[0];

        // 2. Sprite의 원본 픽셀 크기 가져오기
        Vector2 spriteSize = spriteRenderer.sprite.rect.size;

        // 원본 이미지가 비어있을 경우 오류 방지
        if (spriteSize.x == 0 || spriteSize.y == 0) return;

        float ratioX = targetSize.x / spriteSize.x;
        float ratioY = targetSize.y / spriteSize.y;
        float finalScaleX = 1f, finalScaleY = 1f;

        switch (scaleType)
        {
            case ScaleType.Stretch:
                finalScaleX = ratioX;
                finalScaleY = ratioY;
                break;
            case ScaleType.FitInside:
                float minRatio = Mathf.Min(ratioX, ratioY);
                finalScaleX = finalScaleY = minRatio;
                break;
            case ScaleType.Cover:
                float maxRatio = Mathf.Max(ratioX, ratioY);
                finalScaleX = finalScaleY = maxRatio;
                break;
        }

        // 3. UI에 맞게 Transform 대신 RectTransform의 sizeDelta(너비/높이) 조절
        RectTransform rectTransform = spriteRenderer.GetComponent<RectTransform>();

        // Scale은 1,1,1로 초기화 (UI 권장 사항)
        rectTransform.localScale = Vector3.one;

        // 최종 크기(원본 픽셀 크기 * 계산된 비율)를 RectTransform 사이즈에 적용
        rectTransform.sizeDelta = new Vector2(spriteSize.x * finalScaleX, spriteSize.y * finalScaleY);
    }

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

    // 이미지를 계속 교체하며 재생하는 루프
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

    // 외부에서 새로운 GIF를 설정할 때 사용
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

    public void SetSpriteRender(Sprite[] newImage)
    {
        spriteRenderer.sprite = newImage[0];
    }
}