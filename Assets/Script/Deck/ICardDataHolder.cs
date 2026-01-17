using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 데이터를 가지고 있고, 외부로 제공할 수 있는 모든 클래스가 구현해야 하는 인터페이스입니다.
/// 이 인터페이스를 구현하는 클래스는 CardDataFirebase 타입의 객체를 반환하는
/// GetCardData() 메소드를 반드시 가지고 있어야 합니다.
/// </summary>
public interface ICardDataHolder
{
    /// <summary>
    /// 이 컴포넌트가 가지고 있는 카드 데이터를 반환합니다.
    /// </summary>
    /// <returns>카드 원본 데이터</returns>
    CardData GetCardData();
}
