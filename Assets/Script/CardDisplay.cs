using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    public CardData cardData;

    [Header("--- UI 컴포넌트 (핸드 카드용) ---")]
    public TextMeshProUGUI nameText_UI;
    public TextMeshProUGUI descriptionText_UI;
    public TextMeshProUGUI manaText_UI;
    public TextMeshProUGUI attackText_UI;
    public TextMeshProUGUI healthText_UI;
    public Image artworkImage_UI;

    [Header("--- 3D 컴포넌트 (필드 카드용) ---")]
    public TextMeshPro nameText_3D;
    public TextMeshPro descriptionText_3D;
    public TextMeshPro manaText_3D;
    public TextMeshPro attackText_3D;
    public TextMeshPro healthText_3D;
    public Renderer artworkRenderer_3D; // 이미지를 표시할 쿼드(Quad)의 Renderer

    void Start()
    {
        if (cardData != null)
        {
            ApplyCardData();
        }
    }

    // cardData의 정보를 연결된 컴포넌트에 채워 넣는 함수입니다.
    public void ApplyCardData()
    {
        if (cardData == null) return;

        // --- 이름 업데이트 ---
        // UI용 변수가 연결되어 있다면 UI 텍스트를 업데이트합니다.
        if (nameText_UI != null) nameText_UI.text = cardData.cardName;
        // 3D용 변수가 연결되어 있다면 3D 텍스트를 업데이트합니다.
        if (nameText_3D != null) nameText_3D.text = cardData.cardName;

        // --- 설명 업데이트 ---
        if (descriptionText_UI != null) descriptionText_UI.text = cardData.description;
        if (descriptionText_3D != null) descriptionText_3D.text = cardData.description;

        // --- 마나 비용 업데이트 ---
        if (manaText_UI != null) manaText_UI.text = cardData.manaCost.ToString();
        if (manaText_3D != null) manaText_3D.text = cardData.manaCost.ToString();

        // --- 공격력 업데이트 ---
        if (attackText_UI != null) attackText_UI.text = cardData.attack.ToString();
        if (attackText_3D != null) attackText_3D.text = cardData.attack.ToString();

        // --- 체력 업데이트 ---
        if (healthText_UI != null) healthText_UI.text = cardData.health.ToString();
        if (healthText_3D != null) healthText_3D.text = cardData.health.ToString();

        // --- 아트워크 업데이트 ---
        if (artworkImage_UI != null) artworkImage_UI.sprite = cardData.cardArtwork;
        // 3D 쿼드의 경우, 머티리얼의 메인 텍스처를 변경해 줍니다.
        if (artworkRenderer_3D != null) artworkRenderer_3D.material.mainTexture = cardData.cardArtwork.texture;
    }
}
