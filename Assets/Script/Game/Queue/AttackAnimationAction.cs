// 1. 공격 연출을 담당하는 '지시서' 클래스를 정의합니다. (ISequenceAction 상속)
using System;
using Unity.VisualScripting;
using Cysharp.Threading.Tasks;
using static UnityEngine.GraphicsBuffer;

public class AttackAnimationAction : ISequenceAction
{
    private int _attackerId;
    private int _defenderId;

    // 생성자: 지시서를 만들 때 필요한 데이터만 받아둡니다.
    public AttackAnimationAction(int attackerId, int defenderId)
    {
        _attackerId = attackerId;
        _defenderId = defenderId;
    }

    // 큐 매니저가 이 지시서를 꺼내서 실행할 때 발동되는 부분
    
    public async UniTask ExecuteAsync()
    {
        // 여기서 실제 GameEntityManager의 연출 로직을 호출합니다! 
        // GameEntityManager.Instance.TestAttack(_attackerId, _defenderId);
        GameEntityManager.Instance.PerformAttack(_attackerId, _defenderId);

        // 연출 시간(예: 1초)만큼 큐를 멈춰둡니다.
        await UniTask.Delay(TimeSpan.FromSeconds(1f));
    
    }
    
}