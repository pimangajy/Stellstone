using UnityEngine;
using UnityEngine.UI; // Image
using TMPro; // TextMeshPro
// using UnityEngine.AddressableAssets; // Addressables/AsyncImageLoader 사용 시
// using UnityEngine.ResourceManagement.AsyncOperations; // Addressables 사용 시

/// <summary>
/// 로비(MatchingManager) 씬의 덱 목록에 표시될 개별 카드 UI 항목입니다.
/// </summary>
public class LobbyDeckCardDisplay : MonoBehaviour
{
    [Header("UI 구성 요소")]
    [Tooltip("카드 이름을 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [Tooltip("카드 코스트를 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI costText;
    [Tooltip("카드 이미지를 표시할 Image")]
    [SerializeField] private Image cardImage;
    [Tooltip("중복 카드 개수(예: x2)를 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI countText;

    // private AsyncOperationHandle<Sprite> imageLoadHandle; // Addressables로 이미지 로드 시

    /// <summary>
    /// 카드 정보와 개수를 받아 UI를 설정합니다.
    /// </summary>
    public void Setup(CardDataFirebase card, int count)
    {
        if (card == null) return;

        // 1. 코스트와 이름 설정
        if (costText != null)
        {
            costText.text = card.cost.ToString();
        }
        if (cardNameText != null)
        {
            cardNameText.text = card.name;
        }

        // 2. 카드 개수 표시
        if (countText != null)
        {
            if (count > 1)
            {
                countText.text = $"x{count}";
            }
            else
            {
                // 1장이면 개수 텍스트를 숨깁니다.
                countText.text = "";
            }
        }

        // 3. 카드 이미지 로드 (TODO)
        if (cardImage != null && !string.IsNullOrEmpty(card.imageUrl))
        {
            // TODO: card.imageUrl (이미지 경로/주소)를 사용해 이미지를 비동기 로드해야 합니다.
            // 예: Addressables, AsyncImageLoader, Glide(Android), Kingfisher(iOS) 등
            //
            // (임시) 이미지가 준비되지 않았을 경우를 대비한 코드
            // cardImage.sprite = null; 
            // cardImage.color = Color.gray; // 기본 색상

            // (예시: Addressables 사용 시)
            // imageLoadHandle = Addressables.LoadAssetAsync<Sprite>(card.imageUrl);
            // imageLoadHandle.Completed += (handle) =>
            // {
            //     if (handle.Status == AsyncOperationStatus.Succeeded)
            //     {
            //         cardImage.sprite = handle.Result;
            //     }
            // };
        }
    }

    // (참고: Addressables 사용 시, 오브젝트가 파괴될 때 메모리를 해제해야 합니다)
    // private void OnDestroy()
    // {
    //     if (imageLoadHandle.IsValid())
    //     {
    //         Addressables.Release(imageLoadHandle);
    //     }
    // }
}