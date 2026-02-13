using UnityEngine;

/// <summary>
/// 에디터 테스트용: 게임 시작 시 특정 하수인을 필드에 미리 생성해둡니다.
/// </summary>
public class DummyEntitySetup : MonoBehaviour
{
    [Header("테스트 설정")]
    [Tooltip("체크하면 상대방 하수인, 해제하면 내 하수인으로 설정됩니다.")]
    public bool isEnemy = true; // 기본값을 true로 하여 바로 적 하수인으로 사용

    [Header("비주얼 데이터")]
    public CardData cardData; // 인스펙터에서 원하는 카드(SO)를 드래그앤드롭

    [Header("서버 데이터 (가짜)")]
    public int fakeEntityId = 999; // 실제 게임 ID와 겹치지 않게 큰 숫자로 설정 추천
    public int attack = 2;
    public int health = 10;
    [Range(0, 4)]
    public int position = 0; // 필드 몇 번째 칸에 있을지

    void Start()
    {
        GameCardDisplay display = GetComponent<GameCardDisplay>();

        if (display == null)
        {
            Debug.LogError("GameCardDisplay 컴포넌트가 없습니다!");
            return;
        }

        // 1. 소유자 UID 결정 로직
        // 실제 게임에서는 서버에서 받은 UID를 써야 하지만, 
        // 테스트 환경에서는 클라이언트가 '적'으로 인식하는 문자열을 넣어줘야 합니다.
        // (보통 GameManager에서 내 UID가 아니면 적으로 간주하므로, "Enemy_Test_UID" 등으로 설정)
        string ownerUid = isEnemy ? "Enemy_UID_For_Test" : "Player_UID_For_Test";

        // *만약 실제 서버 통신 테스트 중이라면, 내 UID는 로그인한 실제 UID여야 하고
        // 적 UID는 서버가 알고 있는 상대방 UID여야 합니다. (아래 2번 설명 참조)

        // 2. 가짜 엔티티 데이터 생성
        EntityData dummyEntity = new EntityData
        {
            entityId = fakeEntityId,
            cardId = cardData != null ? cardData.cardID : "dummy_card",
            ownerUid = ownerUid,
            attack = attack,
            health = health,
            maxHealth = health,
            canAttack = true, // 보통 소환된지 오래된 상태를 가정하므로 true
            hasAttacked = false,
            isMember = false,
            position = position,
        };

        // 3. 디스플레이 업데이트
        // (참고: isEnemy가 true라면, 이 오브젝트는 유니티 하이어라키 상에서
        // EnemyField 슬롯(부모) 밑에 위치해야 위치가 올바르게 보일 것입니다.)
        display.SetupEntity(dummyEntity, cardData);

        // 추가: 만약 EntityAttackManager 같은 곳에 이 더미를 등록해야 한다면 여기서 호출
        // Example: EntityAttackManager.Instance.RegisterDummy(fakeEntityId, display);
    }
}