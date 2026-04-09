using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 큐에 들어갈 모든 연출 및 상태 업데이트의 기본 형태를 정의합니다.
/// </summary>
public interface ISequenceAction
{
    /// <summary>
    /// 액션을 실행합니다. 연출이 끝날 때까지 대기할 수 있도록 UniTask를 반환합니다.
    /// </summary>
    UniTask ExecuteAsync();
}

/// <summary>
/// 서버로부터 받은 명령(패킷)들을 즉시 실행하지 않고, 
/// 줄을 세워(Queue) 순차적으로 애니메이션을 재생하는 관제탑입니다.
/// </summary>
public class ActionQueueManager : MonoBehaviour
{
    // 싱글톤 패턴 (어디서든 ActionQueueManager.Instance로 접근 가능)
    public static ActionQueueManager Instance { get; private set; }

    // 실행을 기다리는 코루틴(연출)들의 대기열
    private Queue<IEnumerator> _actionQueue = new Queue<IEnumerator>();

    // 현재 큐가 작동(재생) 중인지 여부
    private bool _isPlaying = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 서버에서 패킷을 받을 때마다 이 함수를 호출하여 할 일을 추가합니다.
    /// </summary>
    /// <param name="actionCoroutine">실행할 연출 코루틴</param>
    public void EnqueueAction(IEnumerator actionCoroutine)
    {
        _actionQueue.Enqueue(actionCoroutine);

        // 만약 현재 쉬고 있다면, 즉시 큐 처리를 시작합니다.
        if (!_isPlaying)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    /// <summary>
    /// 큐에 쌓인 연출들을 하나씩 꺼내서 실행하는 핵심 루프입니다.
    /// </summary>
    private IEnumerator ProcessQueue()
    {
        _isPlaying = true;

        while (_actionQueue.Count > 0)
        {
            // 1. 대기열에서 가장 오래된 연출을 하나 꺼냅니다.
            IEnumerator currentAction = _actionQueue.Dequeue();

            // 2. 해당 연출을 실행하고, 완전히 끝날 때까지 여기서 대기(Wait)합니다.
            // (currentAction 내부에 있는 WaitForSeconds 시간만큼 알아서 기다려줍니다)
            yield return StartCoroutine(currentAction);
        }

        // 큐가 다 비워지면 다시 대기 상태로 들어갑니다.
        _isPlaying = false;
    }

    /// <summary>
    /// (선택사항) 게임이 강제로 종료되거나 리셋될 때 큐를 비우는 함수
    /// </summary>
    public void ClearQueue()
    {
        _actionQueue.Clear();
        StopAllCoroutines();
        _isPlaying = false;
    }
}