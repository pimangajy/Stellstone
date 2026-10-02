using UnityEngine;
using TMPro;

/// <summary>
/// 개별 3D 카드팩 오브젝트 제어 컴포넌트
/// - 상자 바디 및 뚜껑 3D 렌더러 참조
/// - 보유 수량 TextMeshPro 표시 및 갱신
/// - 팩 ID 및 보유 수량 데이터 관리
/// </summary>
public class CardPackItem : MonoBehaviour
{
    [Header("3D 파츠 참조")]
    [Tooltip("상자 바디 3D 렌더러")]
    [SerializeField] private Renderer boxRenderer;

    [Tooltip("상자 뚜껑 3D 렌더러 (개봉 연출 등에 활용)")]
    [SerializeField] private Renderer lidRenderer;

    [Header("수량 텍스트 참조")]
    [Tooltip("수량을 표시할 TextMeshPro 컴포넌트 (3D TMP / UGUI TMP 모두 지원)")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("수량 텍스트 포맷 (예: x{0} -> x3, {0}개 -> 3개)")]
    [SerializeField] private string countFormat = "x{0}";

    [Tooltip("수량이 1개 이하일 때 텍스트 숨김 여부 (false면 x1도 표시)")]
    [SerializeField] private bool hideIfSingle = false;

    // 데이터 프로퍼티
    public string PackId { get; private set; }
    public int Count { get; private set; }

    public Renderer BoxRenderer => boxRenderer;
    public Renderer LidRenderer => lidRenderer;

    /// <summary>
    /// 카드팩 생성 시 데이터 및 텍스트 초기화
    /// </summary>
    /// <param name="packId">카드팩 상품 ID</param>
    /// <param name="count">보유 수량</param>
    public void Setup(string packId, int count)
    {
        PackId = packId;
        UpdateCount(count);

        // 직업에 맞는 전용 머티리얼 자동 적용 (SRP Batcher 활성화)
        if (CraftManager.Instance != null)
        {
            Material mat = CraftManager.Instance.GetPackMaterial(packId);
            if (mat != null)
            {
                ApplyMaterial(mat);
            }
            else
            {
                // Fallback: 텍스처 직접 적용 시도
                Texture2D tex = CraftManager.Instance.GetPackTexture(packId);
                if (tex != null) ApplyTexture(tex);
            }
        }
    }

    /// <summary>
    /// 카드팩 상자 바디와 뚜껑에 공유 머티리얼을 일괄 적용합니다. (SRP Batcher 배칭 유지)
    /// </summary>
    public void ApplyMaterial(Material mat)
    {
        if (mat == null) return;

        if (boxRenderer != null)
        {
            boxRenderer.sharedMaterial = mat;
        }
        if (lidRenderer != null)
        {
            lidRenderer.sharedMaterial = mat;
        }
    }

    /// <summary>
    /// 카드팩 상자 바디와 뚜껑에 텍스처를 적용합니다.
    /// </summary>
    public void ApplyTexture(Texture2D texture)
    {
        if (texture == null) return;

        ApplyTextureToRenderer(boxRenderer, texture);
        ApplyTextureToRenderer(lidRenderer, texture);
    }

    private void ApplyTextureToRenderer(Renderer targetRenderer, Texture2D tex)
    {
        if (targetRenderer == null || tex == null) return;

        // 1. MaterialPropertyBlock을 통해 GPU 텍스처 바인딩
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture("_BaseMap", tex);
        propBlock.SetTexture("_MainTex", tex);
        targetRenderer.SetPropertyBlock(propBlock);

        // 2. 머티리얼 인스턴스 텍스처 속성도 동시 갱신 (URP Lit 및 에디터 뷰 갱신 보장)
        if (targetRenderer.material != null)
        {
            if (targetRenderer.material.HasProperty("_BaseMap"))
            {
                targetRenderer.material.SetTexture("_BaseMap", tex);
            }
            if (targetRenderer.material.HasProperty("_MainTex"))
            {
                targetRenderer.material.SetTexture("_MainTex", tex);
            }
        }
    }

    /// <summary>
    /// 보유 수량 갱신 및 텍스트 반영
    /// </summary>
    /// <param name="newCount">새로운 보유 수량</param>
    public void UpdateCount(int newCount)
    {
        Count = newCount;

        if (countText != null)
        {
            if (hideIfSingle && Count <= 1)
            {
                countText.gameObject.SetActive(false);
            }
            else
            {
                countText.gameObject.SetActive(true);
                countText.text = string.Format(countFormat, Count);
            }
        }
    }

    /// <summary>
    /// 수량 텍스트 활성화/비활성화 (중앙 복사본 등에 활용)
    /// </summary>
    public void SetTextActive(bool active)
    {
        if (countText != null)
        {
            countText.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// 상자와 뚜껑 머티리얼 일괄/개별 설정 편의 함수
    /// </summary>
    public void SetMaterials(Material boxMat, Material lidMat = null)
    {
        if (boxRenderer != null && boxMat != null)
        {
            boxRenderer.material = boxMat;
        }

        if (lidRenderer != null)
        {
            lidRenderer.material = (lidMat != null) ? lidMat : boxMat;
        }
    }
}
