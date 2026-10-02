using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// 여러 장의 스프라이트(이미지)를 연속으로 보여주어
/// 2D UI(Image) 또는 3D 필드(SpriteRenderer)에서 움직이는 GIF 연출을 처리하는 스크립트입니다.
/// </summary>
public class SpriteGifPlayer : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("필드 카드 여부 (체크 시 상시 애니메이션 정지)")]
    public bool feildCard;
    public Sprite[] gifFrames; // 프레임 이미지들
    public float framesPerSecond = 10.0f; // 1초에 몇 장 보여줄지

    [Header("렌더러 컴포넌트 연결")]
    public Image image;                   // UI용 (손패 카드 등)
    public SpriteRenderer spriteRenderer; // 필드용 (3D 하수인 등)
    private Coroutine playCoroutine;

    void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (gifFrames != null && gifFrames.Length > 0)
        {
            ApplyCurrentFrame(0);

            if (!feildCard && gifFrames.Length > 1)
            {
                PlayAnimation();
            }
        }
    }

    void OnDisable()
    {
        StopAnimation();
    }

    /// <summary>
    /// 지정된 인덱스의 프레임을 Image 및 SpriteRenderer에 적용합니다.
    /// </summary>
    private void ApplyCurrentFrame(int index)
    {
        if (gifFrames == null || gifFrames.Length == 0) return;

        if (index < 0 || index >= gifFrames.Length) index = 0;
        Sprite currentSprite = gifFrames[index];

        if (image != null)
        {
            image.sprite = currentSprite;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = currentSprite;
        }
    }

    void PlayAnimation()
    {
        StopAnimation();
        playCoroutine = StartCoroutine(PlayGifRoutine());
    }

    void StopAnimation()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
    }

    // 이미지를 계속 교체하며 재생하는 루프
    IEnumerator PlayGifRoutine()
    {
        int index = 0;
        float waitTime = framesPerSecond > 0 ? 1f / framesPerSecond : 0.1f;
        var wait = YieldInstructionCache.WaitForSeconds(waitTime);

        while (true)
        {
            if (gifFrames != null && gifFrames.Length > 0)
            {
                ApplyCurrentFrame(index);
                index = (index + 1) % gifFrames.Length;
            }
            yield return wait;
        }
    }

    /// <summary>
    /// 외부에서 새로운 애니메이션 프레임들을 설정할 때 사용합니다.
    /// 첫 번째 프레임을 메인 이미지로 즉시 적용합니다.
    /// </summary>
    public void SetGif(Sprite[] newFrames, float speed = 10.0f)
    {
        if (image == null) image = GetComponent<Image>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        this.gifFrames = newFrames;
        this.framesPerSecond = speed;

        if (gifFrames != null && gifFrames.Length > 0)
        {
            // 첫 번째 프레임을 메인 이미지로 즉시 적용
            ApplyCurrentFrame(0);

            if (gameObject.activeInHierarchy)
            {
                if (!feildCard && gifFrames.Length > 1)
                {
                    PlayAnimation();
                }
                else
                {
                    StopAnimation();
                }
            }
        }
    }

    /// <summary>
    /// 필드 하수인 등 단일 정지 이미지를 적용할 때 사용합니다.
    /// </summary>
    public void SetSpriteRender(Sprite[] newImage)
    {
        if (newImage != null && newImage.Length > 0)
        {
            this.gifFrames = newImage;
            ApplyCurrentFrame(0);
        }
    }
}
